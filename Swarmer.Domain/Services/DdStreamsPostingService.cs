global using Stream = TwitchLib.Api.Helix.Models.Streams.GetStreams.Stream;
using Discord;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Swarmer.Domain.Models;
using Swarmer.Domain.Models.Database;
using Swarmer.Domain.Models.Extensions;
using TwitchLib.Api.Helix.Models.Users.GetUsers;
using TwitchLib.Api.Interfaces;

namespace Swarmer.Domain.Services;

public sealed class DdStreamsPostingService : AbstractBackgroundService
{
	private readonly IServiceScopeFactory _serviceScopeFactory;
	private readonly ITwitchAPI _twitchApi;
	private readonly StreamProvider _streamProvider;
	private readonly DiscordService _discordService;
	private readonly string[] _bannedUserLogins;
	private readonly TimeSpan _maxLingeringTime = TimeSpan.FromMinutes(15);

	public DdStreamsPostingService(
		IConfiguration config,
		IServiceScopeFactory serviceScopeFactory,
		ITwitchAPI twitchApi,
		StreamProvider streamProvider,
		DiscordService discordService)
	{
		_serviceScopeFactory = serviceScopeFactory;
		_twitchApi = twitchApi;
		_streamProvider = streamProvider;
		_discordService = discordService;
		_bannedUserLogins = config.GetSection("BannedUserLogins").Get<string[]>() ?? Array.Empty<string>();
	}

	protected override TimeSpan Interval => TimeSpan.FromSeconds(15);

	protected override async Task ExecuteTaskAsync(CancellationToken stoppingToken)
	{
		// Provider hasn't initialised Streams yet or token is cancelled
		if (_streamProvider.Streams is null || stoppingToken.IsCancellationRequested)
		{
			return;
		}

		await using AsyncServiceScope scope = _serviceScopeFactory.CreateAsyncScope();
		SwarmerRepository repo = scope.ServiceProvider.GetRequiredService<SwarmerRepository>();

		await repo.UpdateLingeringStreamMessages(_maxLingeringTime);

		await PostCompletelyNewStreamsAndAddToDb(repo);

		await repo.HandleExistingStreamsAsync();
	}

	private async Task PostCompletelyNewStreamsAndAddToDb(SwarmerRepository repo)
	{
		// Provider hasn't initialised Streams yet
		if (_streamProvider.Streams is not { Length: > 0 })
		{
			return;
		}

		IEnumerable<StreamToPost> streamsToPost = repo.GetStreamsToPost();

		streamsToPost = streamsToPost.Where(stp => !_bannedUserLogins.Contains(stp.Stream.UserLogin));

		foreach (StreamToPost stp in streamsToPost)
		{
			User twitchUser = (await _twitchApi.Helix.Users.GetUsersAsync(ids: new() { stp.Stream.UserId })).Users[0];
			Embed newStreamEmbed = new EmbedBuilder().Online(stp.Stream, twitchUser.ProfileImageUrl);

			Result<IUserMessage> result = await _discordService.SendEmbedAsync(stp.Channel.StreamChannelId, newStreamEmbed);
			if (result.IsFailure)
			{
				continue;
			}

			StreamMessage newDbStreamMessage = new()
			{
				StreamId = stp.Stream.UserId,
				IsLive = true,
				ChannelId = stp.Channel.StreamChannelId,
				MessageId = result.Value.Id,
				AvatarUrl = string.IsNullOrWhiteSpace(twitchUser.ProfileImageUrl) ? null : twitchUser.ProfileImageUrl,
				OfflineThumbnailUrl = string.IsNullOrWhiteSpace(twitchUser.OfflineImageUrl) ? null : twitchUser.OfflineImageUrl,
				LingeringSinceUtc = DateTime.UtcNow,
			};

			await repo.InsertStreamMessage(newDbStreamMessage);

			await Task.Delay(TimeSpan.FromSeconds(1)); // Wait 1s between actions to not get rate-limited by Discord's API
		}

		await repo.SaveChangesAsync();
	}
}

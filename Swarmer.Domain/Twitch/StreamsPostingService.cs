global using Stream = TwitchLib.Api.Helix.Models.Streams.GetStreams.Stream;
using Discord;
using Microsoft.Extensions.DependencyInjection;
using Swarmer.Domain.Database;
using Swarmer.Domain.Discord;
using Swarmer.Domain.Extensions;
using Swarmer.Domain.Models;
using TwitchLib.Api.Helix.Models.Users.GetUsers;
using TwitchLib.Api.Interfaces;

namespace Swarmer.Domain.Twitch;

public sealed class StreamsPostingService : AbstractBackgroundService
{
	private readonly IServiceScopeFactory _serviceScopeFactory;
	private readonly ITwitchAPI _twitchApi;
	private readonly StreamProvider _streamProvider;
	private readonly IDiscordService _discordService;
	private readonly TimeSpan _maxLingeringTime = TimeSpan.FromMinutes(15);

	public StreamsPostingService(
		IServiceScopeFactory serviceScopeFactory,
		ITwitchAPI twitchApi,
		StreamProvider streamProvider,
		IDiscordService discordService)
	{
		_serviceScopeFactory = serviceScopeFactory;
		_twitchApi = twitchApi;
		_streamProvider = streamProvider;
		_discordService = discordService;
	}

	protected override TimeSpan Interval => TimeSpan.FromSeconds(30);

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

		foreach (StreamToPost stp in await repo.GetStreamsToPostAsync())
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

			await repo.InsertStreamMessageAsync(newDbStreamMessage);

			await Task.Delay(TimeSpan.FromSeconds(1)); // Wait 1s between actions to not get rate-limited by Discord's API
		}

		await repo.SaveChangesAsync();
	}
}

global using Stream = TwitchLib.Api.Helix.Models.Streams.GetStreams.Stream;
using Discord;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Swarmer.Domain.Models;
using Swarmer.Domain.Models.Database;
using Swarmer.Domain.Utils;
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.Users.GetUsers;

namespace Swarmer.Domain.Services;

public sealed class DdStreamsPostingService : AbstractBackgroundService
{
	private readonly TimeSpan _maxLingeringTime = TimeSpan.FromMinutes(15);
	private readonly StreamProvider _streamProvider;
	private readonly SwarmerDiscordClient _discordClient;
	private readonly IServiceScopeFactory _serviceScopeFactory;
	private readonly TwitchAPI _twitchApi;
	private string[] _bannedUserLogins = { "thedevildagger" };

	public DdStreamsPostingService(
		SwarmerDiscordClient discordClient,
		IServiceScopeFactory serviceScopeFactory,
		TwitchAPI twitchApi,
		StreamProvider streamProvider)
	{
		_discordClient = discordClient;
		_serviceScopeFactory = serviceScopeFactory;
		_twitchApi = twitchApi;
		_streamProvider = streamProvider;
	}

	protected override TimeSpan Interval => TimeSpan.FromSeconds(15);

	protected override async Task ExecuteTaskAsync(CancellationToken stoppingToken)
	{
		// Provider hasn't initialised Streams yet or token is cancelled
		if (_streamProvider.Streams is null || stoppingToken.IsCancellationRequested)
		{
			return;
		}

		using IServiceScope scope = _serviceScopeFactory.CreateScope();
		await using DbService db = scope.ServiceProvider.GetRequiredService<DbService>();

		await UpdateLingerStatus(db);

		await PostCompletelyNewStreamsAndAddToDb(db);

		await HandleStreamMessages(db);
	}

	private async Task UpdateLingerStatus(DbService db)
	{
		DateTime utcNow = DateTime.UtcNow;
		foreach (StreamMessage ddStream in db.StreamMessages)
		{
			bool hasLingeredForLongEnough = ddStream.LingeringSinceUtc.HasValue && utcNow - ddStream.LingeringSinceUtc >= _maxLingeringTime;
			if (hasLingeredForLongEnough)
			{
				ddStream.StopLingering();
			}
		}

		await db.SaveChangesAsync();
	}

	private async Task PostCompletelyNewStreamsAndAddToDb(DbService db)
	{
		// Provider hasn't initialised Streams yet
		if (_streamProvider.Streams is not { Length: > 0 })
		{
			return;
		}

		List<GameChannel> gameChannels = db.GameChannels.AsNoTracking().ToList();
		List<StreamMessage> ongoingStreams = db.StreamMessages.AsNoTracking().ToList();

		IEnumerable<StreamToPost> streamsToPost = _streamProvider.Streams
			.Join<Stream, GameChannel, string, StreamToPost>(inner: gameChannels,
				outerKeySelector: stream => stream.GameId,
				innerKeySelector: gameChannel => gameChannel.TwitchGameId.ToString(),
				resultSelector: (stream, channel) => new(stream, channel))
			.Where(stp => !_bannedUserLogins.Contains(stp.Stream.UserLogin))
			.Where(stp => !ongoingStreams.Exists(os => (os.StreamId, os.ChannelId) == (stp.Stream.UserId, stp.Channel.StreamChannelId)));

		foreach (StreamToPost stp in streamsToPost)
		{
			User twitchUser = (await _twitchApi.Helix.Users.GetUsersAsync(ids: new() { stp.Stream.UserId })).Users[0];
			Embed newStreamEmbed = StreamEmbed.Online(stp.Stream, twitchUser.ProfileImageUrl);

			if (await _discordClient.Client.GetChannelAsync(stp.Channel.StreamChannelId) is not ITextChannel channel)
			{
				Log.Error("Registered channel {@StreamChannel} doesn't exist", stp.Channel);
				continue;
			}

			bool canSendInChannel = (await channel.GetUserAsync(_discordClient.Client.CurrentUser.Id)).GetPermissions(channel).SendMessages;
			if (!canSendInChannel)
			{
				continue;
			}

			IUserMessage message = await channel.SendMessageAsync(embed: newStreamEmbed);
			StreamMessage newDbStreamMessage = new()
			{
				StreamId = stp.Stream.UserId,
				IsLive = true,
				ChannelId = channel.Id,
				MessageId = message.Id,
				AvatarUrl = twitchUser.ProfileImageUrl,
				OfflineThumbnailUrl = twitchUser.OfflineImageUrl,
				LingeringSinceUtc = DateTime.UtcNow,
			};

			await db.StreamMessages.AddAsync(newDbStreamMessage);
		}

		await db.SaveChangesAsync();
	}

	private async Task HandleStreamMessages(DbService db)
	{
		// Provider hasn't initialised Streams yet
		if (_streamProvider.Streams is null)
		{
			return;
		}

		foreach (StreamMessage streamMessage in db.StreamMessages)
		{
			Stream? ongoingStream = Array.Find(_streamProvider.Streams, s => s.UserId == streamMessage.StreamId);
			if (ongoingStream is not null) // Stream is live on Twitch
			{
				if (streamMessage.IsLive)
				{
					continue;
				}

				if (!streamMessage.IsLingering)
				{
					db.Remove(streamMessage);
					continue;
				}

				await GoOnlineAgainAsync(streamMessage, ongoingStream);
				streamMessage.IsLive = true;
				streamMessage.Linger();
			}
			else // Stream is offline on Twitch
			{
				if (streamMessage.IsLive) // The Discord message is live (stream just went offline)
				{
					await GoOfflineAsync(streamMessage);
					streamMessage.IsLive = false;
					streamMessage.Linger();
				}

				if (streamMessage.IsLingering) // The Discord message is offline, and it's lingering
				{
					continue;
				}

				db.Remove(streamMessage);
			}
		}

		await db.SaveChangesAsync();
	}

	private async Task GoOfflineAsync(StreamMessage streamMessage)
	{
		if (!streamMessage.IsLive ||
			await _discordClient.Client.GetChannelAsync(streamMessage.ChannelId) is not ITextChannel channel ||
			await channel.GetMessageAsync(streamMessage.MessageId) is not IUserMessage message)
		{
			return;
		}

		Embed newEmbed = StreamEmbed.Offline(message.Embeds.First(), streamMessage.OfflineThumbnailUrl);
		await message.ModifyAsync(m => m.Embeds = new(new[] { newEmbed }));
	}

	private async Task GoOnlineAgainAsync(StreamMessage streamMessage, Stream ongoingStream)
	{
		if (await _discordClient.Client.GetChannelAsync(streamMessage.ChannelId) is not ITextChannel channel ||
			await channel.GetMessageAsync(streamMessage.MessageId) is not IUserMessage message)
		{
			return;
		}

		Embed streamEmbed = StreamEmbed.Online(ongoingStream, streamMessage.AvatarUrl);
		await message.ModifyAsync(m => m.Embeds = new(new[] { streamEmbed }));
	}
}

internal record struct StreamToPost(Stream Stream, GameChannel Channel);

global using Stream = TwitchLib.Api.Helix.Models.Streams.GetStreams.Stream;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Swarmer.Models;
using Swarmer.Models.Database;
using Swarmer.Utils;
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.Users.GetUsers;

namespace Swarmer.Services;

public class DdStreamsPostingService : AbstractBackgroundService
{
	private readonly TimeSpan _maxLingeringTime = TimeSpan.FromMinutes(15);
	private readonly StreamProvider _streamProvider;
	private readonly DiscordSocketClient _discordClient;
	private readonly IServiceScopeFactory _serviceScopeFactory;
	private readonly TwitchAPI _twitchApi;

	public DdStreamsPostingService(
		DiscordSocketClient discordClient,
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
		if (_streamProvider.Streams is null) // Provider hasn't initialised Streams yet
			return;

		using IServiceScope scope = _serviceScopeFactory.CreateScope();
		await using DbService db = scope.ServiceProvider.GetRequiredService<DbService>();

		await UpdateLingerStatus(db);

		await PostCompletelyNewStreamsAndAddToDb(db);

		await HandleStreamMessages(db);
	}

	private async Task UpdateLingerStatus(DbService db)
	{
		DateTime utcNow = DateTime.UtcNow;
		foreach (StreamMessage ddStream in db.DdStreams)
		{
			bool hasLingeredForLongEnough = ddStream.LingeringSinceUtc.HasValue && utcNow - ddStream.LingeringSinceUtc >= _maxLingeringTime;
			if (hasLingeredForLongEnough)
				ddStream.StopLingering();
		}

		await db.SaveChangesAsync();
	}

	private async Task PostCompletelyNewStreamsAndAddToDb(DbService db)
	{
		foreach (Stream ongoingStream in _streamProvider.Streams!)
		{
			bool streamIsPosted = db.DdStreams.AsNoTracking().Any(s => s.StreamId == ongoingStream.Id);
			if (streamIsPosted)
				continue;

			User twitchUser = (await _twitchApi.Helix.Users.GetUsersAsync(ids: new() { ongoingStream.UserId })).Users[0];
			Embed newStreamEmbed = StreamEmbed.Online(ongoingStream, twitchUser.ProfileImageUrl);
			foreach (DdStreamChannel streamChannel in db.DdStreamChannels.AsNoTracking().ToList())
			{
				if (await _discordClient.GetChannelAsync(streamChannel.Id) is not ITextChannel channel)
				{
					Log.Warning("Registered channel {} doesn't exist", streamChannel);
					continue;
				}

				bool canSendInChannel = (await channel.GetUserAsync(_discordClient.CurrentUser.Id)).GetPermissions(channel).SendMessages;
				if (!canSendInChannel)
					continue;

				IUserMessage message = await channel.SendMessageAsync(embed: newStreamEmbed);
				StreamMessage newDbStreamMessage = new()
				{
					StreamId = ongoingStream.Id,
					IsLive = true,
					ChannelId = channel.Id,
					MessageId = message.Id,
					AvatarUrl = twitchUser.ProfileImageUrl,
					OfflineThumbnailUrl = twitchUser.OfflineImageUrl,
					LingeringSinceUtc = DateTime.UtcNow,
				};

				await db.DdStreams.AddAsync(newDbStreamMessage);
			}
		}

		await db.SaveChangesAsync();
	}

	private async Task HandleStreamMessages(DbService db)
	{
		foreach (StreamMessage streamMessage in db.DdStreams)
		{
			Stream? ongoingStream = _streamProvider.Streams!.FirstOrDefault(s => s.Id == streamMessage.StreamId);
			if (ongoingStream is not null) // Stream is live on Twith
			{
				if (streamMessage.IsLive)
					continue;

				bool messageIsLingering = streamMessage.LingeringSinceUtc.HasValue;
				if (messageIsLingering) // The Discord message is offline, and it's lingering (came online again within time limit)
				{
					streamMessage.IsLive = true;
					await GoOnlineAgainAsync(streamMessage, ongoingStream!);
				}
				else
				{
					db.Remove(streamMessage);
				}
			}
			else // Stream is offline on Twitch
			{
				if (streamMessage.IsLive) // The Discord message is live (stream just went offline)
				{
					streamMessage.IsLive = false;
					streamMessage.Linger();
					await GoOfflineAsync(streamMessage);
				}

				bool messageIsLingering = streamMessage.LingeringSinceUtc.HasValue;
				if (messageIsLingering) // The Discord message is offline, and it's lingering
					continue;

				db.Remove(streamMessage);
			}
		}

		await db.SaveChangesAsync();
	}

	private async Task GoOfflineAsync(StreamMessage streamMessage)
	{
		if (await _discordClient.GetChannelAsync(streamMessage.ChannelId) is not ITextChannel channel ||
			await channel.GetMessageAsync(streamMessage.MessageId) is not IUserMessage message)
			return;

		bool streamMessageIsOffline = message.Embeds.First().Description.StartsWith("⚫ Offline");
		if (!streamMessageIsOffline)
		{
			Embed newEmbed = StreamEmbed.Offline(message.Embeds.First(), streamMessage.OfflineThumbnailUrl);
			await message.ModifyAsync(m => m.Embeds = new(new[] { newEmbed }));
		}
	}

	private async Task GoOnlineAgainAsync(StreamMessage streamMessage, Stream ongoingStream)
	{
		if (await _discordClient.GetChannelAsync(streamMessage.ChannelId) is not ITextChannel channel ||
			await channel.GetMessageAsync(streamMessage.MessageId) is not IUserMessage message)
			return;

		bool streamMessageIsOnline = message.Embeds.First().Description.StartsWith("🔴 Live");
		if (!streamMessageIsOnline)
		{
			Embed streamEmbed = StreamEmbed.Online(ongoingStream, streamMessage.AvatarUrl);
			await message.ModifyAsync(m => m.Embeds = new(new[] { streamEmbed }));
		}
	}
}

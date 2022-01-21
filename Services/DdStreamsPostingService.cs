global using Stream = TwitchLib.Api.Helix.Models.Streams.GetStreams.Stream;
using Discord;
using Discord.WebSocket;
using Swarmer.Helpers;
using Swarmer.Models;
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.Users.GetUsers;

namespace Swarmer.Services;

public class DdStreamsPostingService : AbstractBackgroundService
{
	private readonly TimeSpan _maxBufferTime = TimeSpan.FromMinutes(10);
	private readonly SocketTextChannel _ddPalsNotifChannel;
	private readonly SocketTextChannel _ddInfoNotifChannel;
	private readonly DatabaseService _dbContext;
	private readonly TwitchAPI _api;
	private readonly List<ActiveStream> _activeStreams;
	private readonly List<BufferedStream> _streamBuffer = new();
	private readonly StreamCache _streamCache;

	public DdStreamsPostingService(
		DatabaseService dbContext,
		DiscordSocketClient client,
		TwitchAPI api,
		LoggingService loggingService,
		StreamCache streamCache)
		: base(loggingService)
	{
		_dbContext = dbContext;
		_api = api;
		_streamCache = streamCache;

		_activeStreams = _dbContext.ActiveDdStreams.ToList();
		foreach (ActiveStream stream in _activeStreams)
			_streamBuffer.Add(new(stream.StreamId, stream.DdpalsMessageId, stream.DdinfoMessageId, DateTime.UtcNow));

		_ddPalsNotifChannel = client.GetChannel(ulong.Parse(Environment.GetEnvironmentVariable("DdPalsNotifChannel")!)) as SocketTextChannel ?? throw new ArgumentException("DdPalsNotifChannel");
		_ddInfoNotifChannel = client.GetChannel(ulong.Parse(Environment.GetEnvironmentVariable("DdInfoNotifChannel")!)) as SocketTextChannel ?? throw new ArgumentException("DdInfoNotifChannel");
	}

	protected override TimeSpan Interval => TimeSpan.FromSeconds(30);

	protected override async Task ExecuteTaskAsync(CancellationToken stoppingToken)
	{
		Console.WriteLine($"Stream buffer @{DateTime.Now.ToLongTimeString()}:\n{string.Join('\n', _streamBuffer)}");
		CleanUpStreamBuffer();
		await CheckTwitchStreams();
	}

	private async Task CheckTwitchStreams()
	{
		if (_streamCache.Cache is null)
			return;

		bool changed = false;
		Stream[] twitchStreams = _streamCache.Cache;
		foreach (Stream stream in twitchStreams)
		{
			if (StreamStillActive(stream))
				continue;

			changed = true;
			User twitchUser = (await _api.Helix.Users.GetUsersAsync(ids: new() { stream.UserId })).Users[0];
			(ulong ddpalsMessageId, ulong ddinfoMessageId) = await PostOrUpdateStreamMessage(stream, twitchUser);
			ActiveStream newStream = new(stream.Id, stream.UserId, ddpalsMessageId, ddinfoMessageId, twitchUser.OfflineImageUrl);

			_activeStreams.Add(newStream);
			RefreshBufferedStreamOrCreateNew(stream.Id, ddpalsMessageId, ddinfoMessageId);
			await _dbContext.ActiveDdStreams.AddAsync(newStream);
		}

		for (int i = _activeStreams.Count - 1; i >= 0; i--)
		{
			ActiveStream activeStream = _activeStreams[i];
			Stream? matchingTwitchStream = Array.Find(twitchStreams, ts => ts.Id == activeStream.StreamId);
			if (matchingTwitchStream is not null)
				continue;

			IUserMessage? ddpalsStreamMsg = await _ddPalsNotifChannel.GetMessageAsync(activeStream.DdpalsMessageId) as IUserMessage;
			IUserMessage? ddinfoStreamMsg = await _ddInfoNotifChannel.GetMessageAsync(activeStream.DdinfoMessageId) as IUserMessage;
			await MakeStreamEmbedOfflineIfPossible(ddpalsStreamMsg, activeStream.OfflineThumbnailUrl);
			await MakeStreamEmbedOfflineIfPossible(ddinfoStreamMsg, activeStream.OfflineThumbnailUrl);

			_activeStreams.Remove(activeStream);
			RefreshBufferedStreamOrCreateNew(activeStream.StreamId, ddpalsStreamMsg?.Id, ddinfoStreamMsg?.Id);
			_dbContext.ActiveDdStreams.Remove(activeStream);
			changed = true;
		}

		if (changed)
			await _dbContext.SaveChangesAsync();
	}

	private void RefreshBufferedStreamOrCreateNew(string streamId, ulong? ddpalsMessageId, ulong? ddinfoMessageId)
	{
		if (_streamBuffer.Find(bs => bs.StreamId == streamId) is { StreamId: not null } bufferedStream)
			bufferedStream.DateAddedUtc = DateTime.UtcNow;
		else
			_streamBuffer.Add(new(streamId, ddpalsMessageId, ddinfoMessageId, DateTime.UtcNow));
	}

	private async Task<(ulong DdpalsMessageId, ulong DdinfoMessageId)> PostOrUpdateStreamMessage(Stream stream, User twitchUser)
	{
		Embed streamEmbed = EmbedHelper.GetOnlineStreamEmbed(
			stream.Title,
			stream.UserName,
			GetProperUrl(stream.ThumbnailUrl),
			GetProperUrl(twitchUser.ProfileImageUrl),
			"https://twitch.tv/" + stream.UserName);

		IUserMessage? ddpalsMsg = null;
		IUserMessage? ddinfoMsg = null;
		// Stream is active and in buffer
		if (_streamBuffer.Find(bs => bs.StreamId == stream.Id) is { StreamId: not null } bufferedStream)
		{
			if (bufferedStream.DdpalsMessageId is not null)
				ddpalsMsg = await _ddPalsNotifChannel.GetMessageAsync((ulong)bufferedStream.DdpalsMessageId) as IUserMessage;

			if (bufferedStream.DdinfoMessageId is not null)
				ddinfoMsg = await _ddInfoNotifChannel.GetMessageAsync((ulong)bufferedStream.DdinfoMessageId) as IUserMessage;

			await MakeStreamEmbedOnlineIfPossible(ddpalsMsg, streamEmbed);
			await MakeStreamEmbedOnlineIfPossible(ddinfoMsg, streamEmbed);
		}

		ddpalsMsg ??= await _ddPalsNotifChannel.SendMessageAsync(embed: streamEmbed);
		ddinfoMsg ??= await _ddInfoNotifChannel.SendMessageAsync(embed: streamEmbed);

		return (ddpalsMsg.Id, ddinfoMsg.Id);
	}

	private bool StreamStillActive(Stream stream) => _activeStreams.Exists(s => s.StreamId == stream.Id);

	private void CleanUpStreamBuffer()
	{
		DateTime utcNow = DateTime.UtcNow;
		_streamBuffer.RemoveAll(bs => utcNow - bs.DateAddedUtc >= _maxBufferTime);
	}

	private static async Task MakeStreamEmbedOnlineIfPossible(IUserMessage? streamMessage, Embed embed)
	{
		if (streamMessage is not null)
			await streamMessage.ModifyAsync(m => m.Embed = embed);
	}

	private static async Task MakeStreamEmbedOfflineIfPossible(IUserMessage? streamMessage, string offlineThumbnailUrl)
	{
		if (streamMessage is not null && !streamMessage.Embeds.First().Description.StartsWith("⚫ Offline"))
		{
			Embed newEmbed = EmbedHelper.GetOfflineEmbed(streamMessage.Embeds.First(), offlineThumbnailUrl);
			await streamMessage.ModifyAsync(m => m.Embed = newEmbed);
		}
	}

	private static string GetProperUrl(string url) => url.Replace("{height}", "1080").Replace("{width}", "1920");

	private record struct BufferedStream(string StreamId, ulong? DdpalsMessageId, ulong? DdinfoMessageId, DateTime DateAddedUtc);
}

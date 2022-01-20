global using Stream = TwitchLib.Api.Helix.Models.Streams.GetStreams.Stream;
using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Swarmer.Helpers;
using Swarmer.Models;
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.Users.GetUsers;

namespace Swarmer.Services;

public class DdStreamsPostingService : AbstractBackgroundService
{
	private readonly SocketTextChannel _ddPalsNotifChannel;
	private readonly SocketTextChannel _ddInfoNotifChannel;
	private readonly DatabaseService _dbContext;
	private readonly TwitchAPI _api;
	private readonly List<ActiveStream> _activeStreams;
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
		_ddPalsNotifChannel = client.GetChannel(ulong.Parse(Environment.GetEnvironmentVariable("DdPalsNotifChannel")!)) as SocketTextChannel ?? throw new ArgumentException("DdPalsNotifChannel");
		_ddInfoNotifChannel = client.GetChannel(ulong.Parse(Environment.GetEnvironmentVariable("DdInfoNotifChannel")!)) as SocketTextChannel ?? throw new ArgumentException("DdInfoNotifChannel");
	}

	protected override TimeSpan Interval => TimeSpan.FromSeconds(30);

	protected override async Task ExecuteTaskAsync(CancellationToken stoppingToken)
	{
		await CheckTwitchStreams();
	}

	private async Task CheckTwitchStreams()
	{
		bool changed = false;
		Stream[] twitchStreams = _streamCache.Cache;
		foreach (Stream stream in twitchStreams)
		{
			if (_activeStreams.Exists(s => s.StreamId == stream.Id))
				continue;

			changed = true;
			User twitchUser = (await _api.Helix.Users.GetUsersAsync(ids: new() { stream.UserId })).Users[0];
			Embed streamEmbed = EmbedHelper.GetOnlineStreamEmbed(
				stream.Title,
				stream.UserName,
				GetProperUrl(stream.ThumbnailUrl),
				GetProperUrl(twitchUser.ProfileImageUrl),
				"https://twitch.tv/" + stream.UserName);

			RestUserMessage ddpalsMessage = await _ddPalsNotifChannel.SendMessageAsync(embed: streamEmbed);
			RestUserMessage ddinfoMessage = await _ddInfoNotifChannel.SendMessageAsync(embed: streamEmbed);

			ActiveStream newStream = new(stream.Id, stream.UserId, ddpalsMessage.Id, ddinfoMessage.Id, twitchUser.OfflineImageUrl);
			_activeStreams.Add(newStream);
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
			_dbContext.ActiveDdStreams.Remove(activeStream);
			changed = true;
		}

		if (changed)
			await _dbContext.SaveChangesAsync();
	}

	private static async Task MakeStreamEmbedOfflineIfPossible(IUserMessage? streamMessage, string offlineThumbnailUrl)
	{
		if (streamMessage is not null && !streamMessage.Embeds.First().Description.StartsWith("⚫ Offline"))
		{
			Embed newEmbed = EmbedHelper.GetOfflineEmbed(streamMessage.Embeds.First(), offlineThumbnailUrl);
			await streamMessage.ModifyAsync(m => m.Embed = newEmbed);
		}
	}

	private static string GetProperUrl(string url)
		=> url.Replace("{height}", "1080").Replace("{width}", "1920");
}

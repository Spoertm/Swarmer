using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Swarmer.Helpers;
using Swarmer.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.Streams.GetStreams;
using TwitchLib.Api.Helix.Models.Users.GetUsers;

namespace Swarmer.Services;

public class DdStreamsPostingService : AbstractBackgroundService
{
	private readonly string _devilDaggersId;
	private readonly SocketTextChannel _ddPalsNotifChannel;
	private readonly SocketTextChannel _ddInfoNotifChannel;
	private readonly DatabaseService _dbContext;
	private readonly TwitchAPI _api;
	private readonly List<ActiveStream> _activeStreams;

	public DdStreamsPostingService(
		DatabaseService dbContext,
		DiscordSocketClient client,
		TwitchAPI api,
		LoggingService loggingService)
		: base(loggingService)
	{
		_dbContext = dbContext;
		_api = api;
		_devilDaggersId = Environment.GetEnvironmentVariable("DdTwitchGameId")!;

		_activeStreams = _dbContext.ActiveDdStreams.ToList();
		_ddPalsNotifChannel = client.GetChannel(ulong.Parse(Environment.GetEnvironmentVariable("DdPalsNotifChannel")!)) as SocketTextChannel ?? throw new ArgumentException("DdPalsNotifChannel");
		_ddInfoNotifChannel = client.GetChannel(ulong.Parse(Environment.GetEnvironmentVariable("DdInfoNotifChannel")!)) as SocketTextChannel ?? throw new ArgumentException("DdInfoNotifChannel");
	}

	protected override TimeSpan Interval => TimeSpan.FromMinutes(2);

	protected override async Task ExecuteTaskAsync(CancellationToken stoppingToken)
	{
		await CheckTwitchStreams();
	}

	// TODO: Implement ValidateBotToken(stoppingToken)
	/*
	private async Task ValidateBotToken(CancellationToken stoppingToken)
	{
		const string _tokenReqUrl = "https://id.twitch.tv/oauth2/token";
		ValidateAccessTokenResponse? tokenResponse = await _api.Auth.ValidateAccessTokenAsync();
		if (tokenResponse is null || tokenResponse.ExpiresIn < TimeSpan.FromMinutes(15).TotalSeconds)
		{
#pragma warning disable 8714
			Dictionary<string?, string?> postValues = new()
#pragma warning restore 8714
			{
				{ "client_id", _config["ClientId"] }, { "client_secret", _config["ClientSecret"] }, { "grant_type", "client_credentials" },
			};

			FormUrlEncodedContent content = new(postValues);
			HttpResponseMessage response = await _httpClient.PostAsync(_tokenReqUrl, content, stoppingToken);
			string responseString = await response.Content.ReadAsStringAsync(stoppingToken);
			JObject dynamicResponse = JsonConvert.DeserializeObject<JObject>(responseString)!;
			string newToken = dynamicResponse.Property("access_token")!.Value.ToString();
			_config["AccessToken"] = newToken;
			//RefreshResponse refreshResponse = await _api.Auth.RefreshAuthTokenAsync(newToken, _config["ClientSecret"], _config["ClientId"]);
			//await File.WriteAllTextAsync(_config.ConfigPath, JsonConvert.SerializeObject(_config, Formatting.Indented), stoppingToken);
		}
	}
	*/

	private async Task CheckTwitchStreams()
	{
		bool changed = false;
		Stream[] twitchStreams = (await _api.Helix.Streams.GetStreamsAsync(first: 50, gameIds: new() { _devilDaggersId }))
			.Streams;

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

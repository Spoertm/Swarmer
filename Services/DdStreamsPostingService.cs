using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Swarmer.Helpers;
using Swarmer.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Api;
using TwitchLib.Api.Auth;
using TwitchLib.Api.Helix.Models.Users.GetUsers;
using Stream = TwitchLib.Api.Helix.Models.Streams.GetStreams.Stream;

namespace Swarmer.Services;

public class DdStreamsPostingService : AbstractBackgroundService
{
	private const string _devilDaggersId = "490905";
	private const string _tokenReqUrl = "https://id.twitch.tv/oauth2/token";
	private readonly SocketTextChannel _ddPalsNotifChannel;
	private readonly SocketTextChannel _ddInfoNotifChannel;
	private readonly Config _config;
	private readonly DiscordHelper _discordHelper;
	private readonly TwitchAPI _api;
	private readonly List<ActiveStream> _activeStreams;
	private readonly HttpClient _httpClient = new();

	public DdStreamsPostingService(
		Config config,
		DiscordHelper discordHelper,
		TwitchAPI api,
		LoggingService loggingService)
		: base(loggingService)
	{
		_config = config;
		_discordHelper = discordHelper;
		_api = api;
		_api.Settings.ClientId = config.ClientId;
		_api.Settings.AccessToken = config.AccessToken;

		_activeStreams = _discordHelper.DeserializeActiveStreams().Result;
		_ddPalsNotifChannel = _discordHelper.GetTextChannel(config.DdPalsNotifChannel);
		_ddInfoNotifChannel = _discordHelper.GetTextChannel(config.DdInfoNotifChannel);
	}

	protected override TimeSpan Interval => TimeSpan.FromMinutes(2);

	protected override async Task ExecuteTaskAsync(CancellationToken stoppingToken)
	{
		// TODO: Implement ValidateBotToken(stoppingToken)
		bool twitchStreamsChanged = await CheckTwitchStreams();
		if (twitchStreamsChanged)
			await _discordHelper.UpdateActiveStreams(_activeStreams);
	}

	private async Task ValidateBotToken(CancellationToken stoppingToken)
	{
		ValidateAccessTokenResponse? tokenResponse = await _api.Auth.ValidateAccessTokenAsync();
		if (tokenResponse is null || tokenResponse.ExpiresIn < TimeSpan.FromMinutes(15).TotalSeconds)
		{
#pragma warning disable 8714
			Dictionary<string?, string?> postValues = new()
#pragma warning restore 8714
			{
				{ "client_id", _config.ClientId }, { "client_secret", _config.ClientSecret }, { "grant_type", "client_credentials" },
			};

			FormUrlEncodedContent content = new(postValues);
			HttpResponseMessage response = await _httpClient.PostAsync(_tokenReqUrl, content, stoppingToken);
			string responseString = await response.Content.ReadAsStringAsync(stoppingToken);
			JObject dynamicResponse = JsonConvert.DeserializeObject<JObject>(responseString)!;
			string newToken = dynamicResponse.Property("access_token")!.Value.ToString();
			_config.AccessToken = newToken;
			RefreshResponse refreshResponse = await _api.Auth.RefreshAuthTokenAsync(newToken, _config.ClientSecret, _config.ClientId);
			await File.WriteAllTextAsync(Config.ConfigPath, JsonConvert.SerializeObject(_config, Formatting.Indented), stoppingToken);
		}
	}

	private async Task<bool> CheckTwitchStreams()
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

			_activeStreams.Add(new(stream.Id, stream.UserId, ddpalsMessage.Id, ddinfoMessage.Id, twitchUser.OfflineImageUrl));
		}

		for (int i = _activeStreams.Count - 1; i >= 0; i--)
		{
			ActiveStream activeStream = _activeStreams[i];
			Stream? matchingTwitchStream = Array.Find(twitchStreams, ts => ts.Id == activeStream.StreamId);
			if (matchingTwitchStream is not null)
				continue;

			IUserMessage? ddpalsStreamMsg = await _ddPalsNotifChannel.GetMessageAsync(activeStream.DdPalsMessageId) as IUserMessage;
			IUserMessage? ddinfoStreamMsg = await _ddInfoNotifChannel.GetMessageAsync(activeStream.DdInfoMessageId) as IUserMessage;

			await MakeStreamEmbedOfflineIfPossible(ddpalsStreamMsg, activeStream.OfflineThumbnailUrl);
			await MakeStreamEmbedOfflineIfPossible(ddinfoStreamMsg, activeStream.OfflineThumbnailUrl);

			_activeStreams.Remove(activeStream);
			changed = true;
		}

		return changed;
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

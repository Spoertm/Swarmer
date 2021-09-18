using Discord;
using Discord.Rest;
using Discord.WebSocket;
using OpenQA.Selenium;
using Swarmer.Helpers;
using Swarmer.Models;
using Swarmer.Models.DTOs;
using Swarmer.Models.Enums;
using Swarmer.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Api;
using TwitchLib.Api.Helix.Models.Streams.GetStreams;
using TwitchLib.Api.Helix.Models.Users.GetUsers;

namespace Swarmer.Services
{
	public class DdStreamsPostingService : AbstractBackgroundService
	{
		private const string _devilDaggersId = "490905";
		private readonly Dictionary<ulong, SocketTextChannel> _notifChannels = new();
		private readonly DiscordHelper _discordHelper;
		private readonly TwitchAPI _api;
		private readonly DiscordSocketClient _client;
		private readonly YoutubeStreamsFetcher _youtubeStreamsFetcher;
		private readonly List<ActiveStream> _activeStreams;

		public DdStreamsPostingService(
			Config config,
			DiscordSocketClient client,
			DiscordHelper discordHelper,
			TwitchAPI api,
			LoggingService loggingService,
			IWebDriver driver)
			: base(loggingService)
		{
			_discordHelper = discordHelper;
			_api = api;
			_api.Settings.ClientId = config.ClientId;
			_api.Settings.AccessToken = config.AccessToken;
			_client = client;
			_youtubeStreamsFetcher = new(driver);

			_activeStreams = _discordHelper.DeserializeActiveStreams().Result;

			foreach (ulong notifChannelId in config.NotifChannelIds)
				_notifChannels.Add(notifChannelId, _discordHelper.GetTextChannel(notifChannelId));
		}

		protected override TimeSpan Interval => TimeSpan.FromMinutes(2);

		protected override async Task ExecuteTaskAsync(CancellationToken stoppingToken)
		{
			bool twitchStreamsChanged = await CheckTwitchStreams();
			bool youtubeStreamsChanged = await CheckYoutubeStreams();

			if ((twitchStreamsChanged || youtubeStreamsChanged) && _activeStreams.Count > 0)
				await _discordHelper.SerializeAndUpdateActiveStreams(_activeStreams);
		}

		private async Task<bool> CheckYoutubeStreams()
		{
			bool changed = false;
			YoutubeStream[] youtubeStreams = _youtubeStreamsFetcher.GetStreams();

			for (int i = 0; i < youtubeStreams.Length; i++)
			{
				YoutubeStream stream = youtubeStreams[i];
				if (_activeStreams.Exists(s => s.StreamId == stream.Username))
					continue;

				changed = true;
				foreach (SocketTextChannel channel in _notifChannels.Values)
				{
					RestUserMessage msg = await channel.SendMessageAsync(embed: EmbedHelper.GetOnlineStreamEmbed(
						stream.Title,
						stream.Username,
						stream.ThumbnailUrl,
						stream.AvatarUrl,
						stream.StreamUrl,
						StreamingPlatform.YouTube));

					_activeStreams.Add(new(channel.Id, stream.Username, stream.Username, msg.Id, StreamingPlatform.YouTube));
				}
			}

			for (int i = _activeStreams.Count - 1; i >= 0; i--)
			{
				ActiveStream activeStream = _activeStreams[i];
				if (activeStream.Platform != StreamingPlatform.YouTube)
					continue;

				YoutubeStream? matchingYoutubeStream = Array.Find(youtubeStreams, ts => ts.Username == activeStream.StreamId);
				if (matchingYoutubeStream is not null)
					continue;

				if (_notifChannels.ContainsKey(activeStream.DiscordChannelId) &&
					_client.GetChannel(activeStream.DiscordChannelId) is not null &&
					await _notifChannels[activeStream.DiscordChannelId].GetMessageAsync(activeStream.DiscordMessageId) is IUserMessage msgToBeEdited &&
					!msgToBeEdited.Embeds.First().Description.StartsWith("⚫ Offline"))
				{
					Embed newEmbed = EmbedHelper.GetOfflineEmbed(msgToBeEdited.Embeds.First());
					await msgToBeEdited.ModifyAsync(m => m.Embed = newEmbed);
				}

				_activeStreams.Remove(activeStream);
				changed = true;
			}

			return changed;
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
				foreach (SocketTextChannel channel in _notifChannels.Values)
				{
					RestUserMessage msg = await channel.SendMessageAsync(embed: EmbedHelper.GetOnlineStreamEmbed(
						stream.Title,
						stream.UserName,
						GetProperUrl(stream.ThumbnailUrl),
						GetProperUrl(await GetTwitchUserProperty(stream.UserId, u => u.ProfileImageUrl)),
						"https://twitch.tv/" + stream.UserName,
						StreamingPlatform.Twitch));

					_activeStreams.Add(new(channel.Id, stream.Id, stream.UserId, msg.Id, StreamingPlatform.Twitch));
				}
			}

			for (int i = _activeStreams.Count - 1; i >= 0; i--)
			{
				ActiveStream activeStream = _activeStreams[i];
				if (activeStream.Platform != StreamingPlatform.Twitch)
					continue;

				Stream? matchingTwitchStream = Array.Find(twitchStreams, ts => ts.Id == activeStream.StreamId);
				if (matchingTwitchStream is not null)
					continue;

				if (_notifChannels.ContainsKey(activeStream.DiscordChannelId) &&
					_client.GetChannel(activeStream.DiscordChannelId) is not null &&
					await _notifChannels[activeStream.DiscordChannelId].GetMessageAsync(activeStream.DiscordMessageId) is IUserMessage msgToBeEdited &&
					!msgToBeEdited.Embeds.First().Description.StartsWith("⚫ Offline"))
				{
					Embed newEmbed = EmbedHelper.GetOfflineEmbed(msgToBeEdited.Embeds.First(), await GetTwitchUserProperty(activeStream.UserId, u => u.OfflineImageUrl));
					await msgToBeEdited.ModifyAsync(m => m.Embed = newEmbed);
				}

				_activeStreams.Remove(activeStream);
				changed = true;
			}

			return changed;
		}

		private async Task<string> GetTwitchUserProperty(string userId, Func<User, string> propertySelector)
		{
			User user = (await _api.Helix.Users.GetUsersAsync(ids: new() { userId }))
				.Users[0];

			return propertySelector(user);
		}

		private static string GetProperUrl(string url)
			=> url.Replace("{height}", "1080").Replace("{width}", "1920");
	}
}

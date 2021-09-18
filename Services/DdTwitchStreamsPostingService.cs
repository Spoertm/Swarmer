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

namespace Swarmer.Services
{
	public class DdTwitchStreamsPostingService : AbstractBackgroundService
	{
		private const string _devilDaggersId = "490905";
		private readonly Dictionary<ulong, SocketTextChannel> _notifChannels = new();
		private readonly Helper _helper;
		private readonly TwitchAPI _api;
		private readonly DiscordSocketClient _client;
		private readonly List<ActiveStream> _activeStreams;

		public DdTwitchStreamsPostingService(Config config, DiscordSocketClient client, Helper helper, TwitchAPI api, LoggingService loggingService)
			: base(loggingService)
		{
			_helper = helper;
			_api = api;
			_api.Settings.ClientId = config.ClientId;
			_api.Settings.AccessToken = config.AccessToken;
			_client = client;

			_activeStreams = _helper.DeserializeActiveStreams().Result;

			foreach (ulong notifChannelId in config.NotifChannelIds)
			{
				if (client.GetChannel(notifChannelId) is not SocketTextChannel socketTextChannel)
				{
					loggingService.LogAsync(new(LogSeverity.Error, "EmbedUpdateBackgroundService constructor", $"Notif channel with ID {notifChannelId} is null.")).GetAwaiter().GetResult();
					throw new($"Notif channel with ID {notifChannelId} is null.");
				}

				_notifChannels.Add(notifChannelId, socketTextChannel);
			}
		}

		protected override TimeSpan Interval => TimeSpan.FromMinutes(2);

		protected override async Task ExecuteTaskAsync(CancellationToken stoppingToken)
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
						GetProperUrl(await GetProfileImageUrl(stream)),
						"https://twitch.tv/" + stream.UserName,
						StreamingPlatform.Twitch));

					_activeStreams.Add(new(channel.Id, stream.Id, stream.UserId, msg.Id));
				}
			}

			for (int i = _activeStreams.Count - 1; i >= 0; i--)
			{
				ActiveStream activeStream = _activeStreams[i];
				Stream? matchingTwitchStream = Array.Find(twitchStreams, ts => ts.Id == activeStream.StreamId);
				if (matchingTwitchStream is not null)
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

			if (changed && _activeStreams.Count > 0)
				await _helper.SerializeAndUpdateActiveStreams(_activeStreams);
		}

		private async Task<string> GetProfileImageUrl(Stream stream)
		{
			return (await _api.Helix.Users.GetUsersAsync(ids: new() { stream.UserId }))
				.Users[0]
				.ProfileImageUrl;
		}

		private static string GetProperUrl(string url)
			=> url.Replace("{height}", "1080").Replace("{width}", "1920");
	}
}

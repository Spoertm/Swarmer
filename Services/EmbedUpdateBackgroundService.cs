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
	public class EmbedUpdateBackgroundService : AbstractBackgroundService
	{
		private const string _devilDaggersId = "490905";
		private readonly Dictionary<ulong, SocketTextChannel> _notifChannels = new();
		private readonly Helper _helper;
		private readonly EmbedHelper _embedHelper;
		private readonly TwitchAPI _api;
		private readonly DiscordSocketClient _client;
		private readonly List<ActiveStream> _activeStreams;

		public EmbedUpdateBackgroundService(Config config, DiscordSocketClient client, Helper helper, EmbedHelper embedHelper, TwitchAPI api, LoggingService loggingService)
			: base(loggingService)
		{
			_helper = helper;
			_embedHelper = embedHelper;
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
			Stream[] twitchStreams = (await _api.Helix.Streams.GetStreamsAsync(first: 50, gameIds: new()
				{
					_devilDaggersId,
				}))
				.Streams;

			foreach (Stream stream in twitchStreams)
			{
				if (_activeStreams.Exists(s => s.StreamId == stream.Id))
					continue;

				foreach (SocketTextChannel channel in _notifChannels.Values)
				{
					RestUserMessage msg = await channel.SendMessageAsync(embed: await _embedHelper.GetOnlineStreamEmbedAsync(stream));
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
					await _notifChannels[activeStream.DiscordChannelId].GetMessageAsync(activeStream.DiscordMessageId) is IUserMessage msgToBeEdited)
				{
					Embed newEmbed = await _embedHelper.GetOfflineEmbedAsync(msgToBeEdited.Embeds.First(), activeStream.UserId);
					await msgToBeEdited.ModifyAsync(m => m.Embed = newEmbed);
				}

				_activeStreams.Remove(activeStream);
			}

			await _helper.SerializeAndUpdateActiveStreams(_activeStreams);
		}
	}
}

using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using Swarmer.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Swarmer.Helpers
{
	public class DiscordHelper
	{
		private readonly string _activeStreamsFilePath = Path.Combine(AppContext.BaseDirectory, "Models", "ActiveStreams.json");
		private readonly SocketTextChannel _activeTwitchStreamsChannel;
		private readonly HttpClient _httpClient = new();
		private readonly DiscordSocketClient _socketClient;

		public DiscordHelper(Config config, DiscordSocketClient client)
		{
			_socketClient = client;
			_activeTwitchStreamsChannel = client.GetChannel(config.SwarmerActiveStreamsChannelId) as SocketTextChannel ?? throw new("ActiveStreams channel is null.");
		}

		public async Task<List<ActiveStream>> DeserializeActiveStreams()
		{
			IAttachment? latestAttachment = (await _activeTwitchStreamsChannel.GetMessagesAsync(1).FlattenAsync())
				.FirstOrDefault()?
				.Attachments
				.FirstOrDefault();

			if (latestAttachment is null)
				return new();

			string activeStreamsJson = await _httpClient.GetStringAsync(latestAttachment.Url);
			try
			{
				await File.WriteAllTextAsync(_activeStreamsFilePath, activeStreamsJson);
				List<ActiveStream>? activeStreams = JsonConvert.DeserializeObject<List<ActiveStream>>(activeStreamsJson);
				return activeStreams ?? new();
			}
			catch
			{
				return new();
			}
		}

		public async Task SerializeAndUpdateActiveStreams(List<ActiveStream> activeStreams)
		{
			await File.WriteAllTextAsync(_activeStreamsFilePath, JsonConvert.SerializeObject(activeStreams, Formatting.Indented));
			await _activeTwitchStreamsChannel.SendFileAsync(_activeStreamsFilePath, string.Empty);
		}

		public SocketTextChannel GetTextChannel(ulong channelId)
		{
			SocketTextChannel channel = _socketClient.GetChannel(channelId) as SocketTextChannel ?? throw new($"Discord channel with ID {channelId} doesn't exist.");
			return channel;
		}
	}
}

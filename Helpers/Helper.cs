using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using Swarmer.Models;
using Swarmer.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Swarmer.Helpers
{
	public class Helper
	{
		private readonly string _activeStreamsFilePath = Path.Combine(AppContext.BaseDirectory, "Models", "ActiveStreams.json");
		private readonly SocketTextChannel _swarmerActiveStreamsChannel;
		private readonly LoggingService _loggingService;

		public Helper(Config config, DiscordSocketClient client, LoggingService loggingService)
		{
			SocketTextChannel? activeStreamsChannel = client.GetChannel(config.SwarmerActiveStreamsChannelId) as SocketTextChannel;
			_swarmerActiveStreamsChannel = activeStreamsChannel ?? throw new("ActiveStreams channel is null.");
			_loggingService = loggingService;
		}

		public async Task<List<ActiveStream>> DeserializeActiveStreams()
		{
			IAttachment? latestAttachment = (await _swarmerActiveStreamsChannel.GetMessagesAsync(1).FlattenAsync())
				.FirstOrDefault()?
				.Attachments
				.FirstOrDefault();

			if (latestAttachment is null)
				return new();

			using HttpClient httpClient = new();
			string activeStreamsJson = await httpClient.GetStringAsync(latestAttachment.Url);
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
			await _swarmerActiveStreamsChannel.SendFileAsync(_activeStreamsFilePath, string.Empty);
		}
	}
}

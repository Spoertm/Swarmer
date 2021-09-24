using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using Swarmer.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Swarmer.Helpers
{
	public class DiscordHelper
	{
		private readonly SocketTextChannel _activeTwitchStreamsChannel;
		private readonly DiscordSocketClient _socketClient;
		private readonly IUserMessage _latestActiveStreamsMessage;

		public DiscordHelper(Config config, DiscordSocketClient client)
		{
			_socketClient = client;
			_activeTwitchStreamsChannel = client.GetChannel(config.SwarmerActiveStreamsChannelId) as SocketTextChannel ?? throw new("ActiveStreams channel is null.");
			if (_activeTwitchStreamsChannel.GetMessagesAsync(1).FlattenAsync().Result.FirstOrDefault() is IUserMessage latestActiveStreamsMessage)
				_latestActiveStreamsMessage = latestActiveStreamsMessage;
			else
				_latestActiveStreamsMessage = _activeTwitchStreamsChannel.SendMessageAsync(embed: EmbedHelper.ActiveStreamsEmbed("Active DD streams", Format.Code("[]", "json"))).Result;
		}

		public async Task<List<ActiveStream>> DeserializeActiveStreams()
		{
			string? activeStreamsJson = (await _activeTwitchStreamsChannel.GetMessagesAsync(1).FlattenAsync())
				.FirstOrDefault()?
				.Embeds
				.FirstOrDefault()?
				.Description[8..^4];

			if (string.IsNullOrWhiteSpace(activeStreamsJson))
				return new();

			try
			{
				List<ActiveStream>? activeStreams = JsonConvert.DeserializeObject<List<ActiveStream>>(activeStreamsJson);
				return activeStreams ?? new();
			}
			catch
			{
				return new();
			}
		}

		public async Task UpdateActiveStreams(List<ActiveStream> activeStreams)
		{
			string serialisedStreams = JsonConvert.SerializeObject(activeStreams, Formatting.Indented);
			Embed newEmbed = EmbedHelper.ActiveStreamsEmbed("Active DD streams", Format.Code(serialisedStreams, "json"));
			await _latestActiveStreamsMessage.ModifyAsync(m => m.Embed = newEmbed);
		}

		public SocketTextChannel GetTextChannel(ulong channelId)
			=> _socketClient.GetChannel(channelId) as SocketTextChannel ?? throw new($"Discord channel with ID {channelId} doesn't exist.");
	}
}

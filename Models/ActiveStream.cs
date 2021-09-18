using Swarmer.Models.Enums;

namespace Swarmer.Models
{
	public class ActiveStream
	{
		public ActiveStream(ulong discordChannelId, string streamId, string userId, ulong discordMessageId, StreamingPlatform platform)
		{
			DiscordChannelId = discordChannelId;
			StreamId = streamId;
			UserId = userId;
			DiscordMessageId = discordMessageId;
			Platform = platform;
		}

		public ulong DiscordChannelId { get; }
		public string StreamId { get; }
		public string UserId { get; set; }
		public ulong DiscordMessageId { get; }
		public StreamingPlatform Platform { get; }
	}
}

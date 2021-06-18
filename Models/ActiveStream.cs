namespace Swarmer.Models
{
	public class ActiveStream
	{
		public ActiveStream(ulong discordChannelId, string streamId, string userId, ulong discordMessageId)
		{
			DiscordChannelId = discordChannelId;
			StreamId = streamId;
			UserId = userId;
			DiscordMessageId = discordMessageId;
		}

		public ulong DiscordChannelId { get; }
		public string StreamId { get; }
		public string UserId { get; }
		public ulong DiscordMessageId { get; }
	}
}

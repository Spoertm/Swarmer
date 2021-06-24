namespace Swarmer.Models
{
	public class Config
	{
		public string BotToken { get; set; } = null!;
		public string ClientId { get; set; } = null!;
		public string AccessToken { get; set; } = null!;
		public ulong[] NotifChannelIds { get; set; } = null!;
		public string Prefix { get; set; } = null!;
		public string ReactionEmote { get; set; } = null!;
		public ulong SwarmerInfoChannelId { get; set; }
		public ulong SwarmerActiveStreamsChannelId { get; set; }
	}
}

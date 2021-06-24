namespace Swarmer.Models
{
	public class Config
	{
		public string BotToken { get; set; }
		public string ClientId { get; set; }
		public string AccessToken { get; set; }
		public ulong[] NotifChannelIds { get; set; }
		public string Prefix { get; set; }
		public string ReactionEmote { get; set; }
		public ulong SwarmerInfoChannelId { get; set; }
		public ulong SwarmerActiveStreamsChannelId { get; set; }
	}
}

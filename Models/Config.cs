namespace Swarmer.Models
{
	public class Config
	{
		public string BotToken { get; set; } = null!;
		public string ClientId { get; set; } = null!;
		public string ClientSecret { get; set; } = null!;
		public string AccessToken { get; set; } = null!;
		public ulong DdPalsNotifChannel { get; set; }
		public ulong DdInfoNotifChannel { get; set; }
		public string Prefix { get; set; } = null!;
		public string ReactionEmote { get; set; } = null!;
		public ulong SwarmerInfoChannelId { get; set; }
		public ulong SwarmerActiveStreamsChannelId { get; set; }
	}
}

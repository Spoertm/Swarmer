using System.ComponentModel.DataAnnotations.Schema;

namespace Swarmer.Domain.Models.Database;

[Table("GameChannels")]
public class GameChannel
{
	public int TwitchGameId { get; set; }

	public ulong StreamChannelId { get; set; }
}

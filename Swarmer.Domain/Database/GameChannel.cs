using System.ComponentModel.DataAnnotations.Schema;

namespace Swarmer.Domain.Database;

[Table("GameChannels")]
public class GameChannel
{
	public int TwitchGameId { get; init; }

	public ulong StreamChannelId { get; init; }
}

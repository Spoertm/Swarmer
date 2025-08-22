namespace Swarmer.Domain.Database;

public sealed class GameChannel
{
	public int TwitchGameId { get; init; }

	public ulong StreamChannelId { get; init; }
}

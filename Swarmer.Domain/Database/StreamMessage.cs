namespace Swarmer.Domain.Database;

public sealed class StreamMessage
{
	public int Id { get; init; }

	public ulong MessageId { get; init; }

	public ulong ChannelId { get; init; }

	public bool IsLive { get; set; }

	public required string StreamId { get; init; }

	public string? OfflineThumbnailUrl { get; init; }

	public string? AvatarUrl { get; init; }

	public DateTimeOffset? LingeringSinceUtc { get; set; }

	public bool IsLingering => LingeringSinceUtc.HasValue;

	public void Linger() => LingeringSinceUtc = DateTimeOffset.UtcNow;

	public void StopLingering() => LingeringSinceUtc = null;
}

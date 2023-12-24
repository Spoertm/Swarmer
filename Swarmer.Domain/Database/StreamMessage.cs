using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Swarmer.Domain.Database;

[Table("StreamMessages")]
public sealed class StreamMessage
{
	[Key]
	public int Id { get; init; }

	public ulong MessageId { get; init; }

	public ulong ChannelId { get; init; }

	public bool IsLive { get; set; }

	[MaxLength(20)]
	public required string StreamId { get; init; }

	[MaxLength(200)]
	public string? OfflineThumbnailUrl { get; init; }

	[MaxLength(200)]
	public string? AvatarUrl { get; init; }

	public DateTimeOffset? LingeringSinceUtc { get; set; }

	public void Linger() => LingeringSinceUtc = DateTimeOffset.UtcNow;

	public void StopLingering() => LingeringSinceUtc = null;

	public bool IsLingering => LingeringSinceUtc.HasValue;
}

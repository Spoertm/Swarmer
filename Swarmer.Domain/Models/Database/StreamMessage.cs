using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Swarmer.Domain.Models.Database;

[Table("DdStreams")]
public sealed class StreamMessage
{
	[Key]
	public int Id { get; init; }

	public bool IsLive { get; set; }

	public ulong ChannelId { get; set; }

	public ulong MessageId { get; set; }

	public string StreamId { get; set; } = null!;

	public string? OfflineThumbnailUrl { get; set; }

	public string? AvatarUrl { get; set; }

	public DateTimeOffset? LingeringSinceUtc { get; set; }

	public void Linger() => LingeringSinceUtc = DateTimeOffset.UtcNow;

	public void StopLingering() => LingeringSinceUtc = null;

	public bool IsLingering => LingeringSinceUtc.HasValue;
}

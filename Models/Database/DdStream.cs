using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Swarmer.Models.Database;

public class DdStream
{
	[Key]
	public int Id { get; init; }

	[ForeignKey(nameof(ChannelId))]
	public ulong ChannelId { get; set; }

	public ulong MessageId { get; set; }

	public string StreamId { get; set; } = null!;

	public string? OfflineThumbnailUrl { get; set; }

	public DateTime? StartedLingering { get; set; }
}

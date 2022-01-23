using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Swarmer.Models;

public class DdStream
{
	[Key]
	public int Id { get; init; }

	[ForeignKey(nameof(ChannelId))]
	public ulong ChannelId { get; set; }

	public ulong MessageId { get; set; }

	public int StreamId { get; set; }

	public string? OfflineThumbnailUrl { get; set; }
}

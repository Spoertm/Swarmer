using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Swarmer.Domain.Models.Database;

[Table("DdStreamChannels")]
public sealed class DdStreamChannel
{
	[Key]
	public ulong Id { get; set; }
}

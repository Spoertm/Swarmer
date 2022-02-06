using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Swarmer.Models.Database;

[Table("DdStreamChannels")]
public class DdStreamChannel
{
	[Key]
	public ulong Id { get; set; }
}

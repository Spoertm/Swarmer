using System.ComponentModel.DataAnnotations;

namespace Swarmer.Models.Database;

public class DdStreamChannel
{
	[Key]
	public ulong Id { get; set; }
}

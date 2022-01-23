using System.ComponentModel.DataAnnotations;

namespace Swarmer.Models;

public class DdStreamChannel
{
	[Key]
	public ulong Id { get; set; }
}

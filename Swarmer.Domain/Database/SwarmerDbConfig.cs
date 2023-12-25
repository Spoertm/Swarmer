using System.ComponentModel.DataAnnotations;

namespace Swarmer.Domain.Database;

public sealed class SwarmerDbConfig
{
	[Key]
	public int Id { get; init; }

	[MaxLength(1000)]
	public required string JsonConfig { get; set; }
}

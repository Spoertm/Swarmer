using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Swarmer.Domain.Database;

[Keyless]
public sealed class SwarmerDbConfig
{
	[MaxLength(1000)]
	public required string JsonConfig { get; init; }
}

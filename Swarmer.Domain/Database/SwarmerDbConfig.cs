using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Swarmer.Domain.Database;

[Keyless]
public sealed class SwarmerDbConfig
{
	[Column(TypeName = "jsonb")]
	public required string JsonConfig { get; init; }
}

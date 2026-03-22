using System.ComponentModel.DataAnnotations;

namespace Swarmer.Domain.Models;

public sealed class SwarmerConfig
{
	[Required]
	public required string BotToken { get; init; }

	[Required]
	public required string ClientId { get; init; }

	[Required]
	public required string ClientSecret { get; init; }
}

using System.ComponentModel.DataAnnotations;

namespace Swarmer.Domain.Models;

public class SwarmerConfig
{
	[Required]
	public required string BotToken { get; init; }

	[Required]
	public required string ClientId { get; init; }

	[Required]
	public required string AccessToken { get; init; }

	[Required]
	public required string ClientSecret { get; init; }

	public required string[] BannedUserLogins { get; init; } = Array.Empty<string>();
}

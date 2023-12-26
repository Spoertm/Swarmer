using System.ComponentModel.DataAnnotations;

namespace Swarmer.Domain.Models;

public record SwarmerConfig
{
	[Required]
	public required string BotToken { get; init; }

	[Required]
	public required string ClientId { get; init; }

	[Required]
	public required string AccessToken { get; set; }

	[Required]
	public required string ClientSecret { get; init; }

	public string[] BannedUserLogins { get; init; } = Array.Empty<string>();

	public SwarmerConfig Copy() => new()
	{
		BotToken = BotToken,
		ClientId = ClientId,
		AccessToken = AccessToken,
		ClientSecret = ClientSecret,
		BannedUserLogins = BannedUserLogins.ToArray(),
	};
}

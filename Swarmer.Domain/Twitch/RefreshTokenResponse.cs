using System.Text.Json.Serialization;

namespace Swarmer.Domain.Twitch;

public record RefreshTokenResponse
{
	[JsonPropertyName("access_token")]
	public required string AccessToken { get; init; }

	[JsonPropertyName("expires_in")]
	public required int ExpiresIn { get; init; }

	[JsonPropertyName("token_type")]
	public required string TokenType { get; init; }
}

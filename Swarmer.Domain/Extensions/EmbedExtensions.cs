using Discord;

namespace Swarmer.Domain.Extensions;

public static class EmbedExtensions
{
	public static Embed Online(this EmbedBuilder builder, Stream stream, string? avatarUrl) => builder
		.WithDescription("🔴 Live| " + Format.Sanitize(stream.Title))
		.WithThumbnailUrl(stream.ThumbnailUrl.FormatDimensions())
		.WithAuthor(stream.UserName, avatarUrl, $"https://twitch.tv/{stream.UserLogin}")
		.WithColor(6570404)
		.Build();

	public static Embed Offline(this EmbedBuilder builder, IEmbed oldEmbed, string? newThumbnailUrl = null) => builder
		.WithDescription("⚫ Offline| " + Format.Sanitize(oldEmbed.Description?.Length >= 9 ? oldEmbed.Description[9..] : default))
		.WithThumbnailUrl(newThumbnailUrl?.FormatDimensions() ?? oldEmbed.Thumbnail?.Url ?? default)
		.WithAuthor(oldEmbed.Author?.Name ?? "NaN", oldEmbed.Author?.IconUrl, oldEmbed.Author?.Url)
		.WithColor(1)
		.Build();
}

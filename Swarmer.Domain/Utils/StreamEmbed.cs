using Discord;
using Swarmer.Domain.Models.Extensions;

namespace Swarmer.Domain.Utils;

public static class StreamEmbed
{
	public static Embed Online(Stream stream, string? avatarUrl) => new EmbedBuilder()
		.WithDescription("🔴 Live| " + stream.Title)
		.WithThumbnailUrl(stream.ThumbnailUrl.FormatDimensions())
		.WithAuthor(stream.UserName, avatarUrl, $"https://twitch.tv/{stream.UserLogin}")
		.WithColor(6570404)
		.Build();

	public static Embed Offline(IEmbed oldEmbed, string? newThumbnailUrl = null) => new EmbedBuilder()
		.WithDescription("⚫ Offline| " + (oldEmbed.Description?.Length >= 9 ? oldEmbed.Description[9..] : string.Empty))
		.WithThumbnailUrl(newThumbnailUrl?.FormatDimensions() ?? oldEmbed.Thumbnail?.Url ?? string.Empty)
		.WithAuthor(oldEmbed.Author?.Name ?? "NaN", oldEmbed.Author?.IconUrl, oldEmbed.Author?.Url)
		.WithColor(1)
		.Build();
}

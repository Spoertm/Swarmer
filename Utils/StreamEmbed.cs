using Discord;

namespace Swarmer.Utils;

public static class StreamEmbed
{
	public static Embed Online(
		string title,
		string username,
		string thumbnailUrl,
		string avatarUrl,
		string streamUrl)
		=> new EmbedBuilder()
			.WithDescription("🔴 Live| " + title)
			.WithThumbnailUrl(thumbnailUrl)
			.WithAuthor(username, avatarUrl, streamUrl)
			.WithColor(6570404)
			.Build();

	public static Embed Offline(IEmbed oldEmbed, string? newThumbnailUrl = null)
		=> new EmbedBuilder()
			.WithDescription("⚫ Offline| " + (oldEmbed.Description?.Length >= 9 ? oldEmbed.Description[9..] : string.Empty))
			.WithThumbnailUrl(newThumbnailUrl ?? oldEmbed.Thumbnail?.Url ?? string.Empty)
			.WithAuthor(oldEmbed.Author?.Name ?? "NaN", oldEmbed.Author?.IconUrl, oldEmbed.Author?.Url)
			.WithColor(1)
			.Build();
}

using Discord;

namespace Swarmer.Helpers;

public static class EmbedHelper
{
	public static Embed GetOnlineStreamEmbed(
		string title,
		string username,
		string thumbnailUrl,
		string avatarUrl,
		string streamUrl)
	{
		return new EmbedBuilder()
			.WithDescription("🔴 Live| " + title)
			.WithThumbnailUrl(thumbnailUrl)
			.WithAuthor(username, avatarUrl, streamUrl)
			.WithColor(6570404)
			.Build();
	}

	public static Embed GetOfflineEmbed(IEmbed oldEmbed, string? newThumbnailUrl = null)
	{
		return new EmbedBuilder()
			.WithDescription("⚫ Offline| " + (oldEmbed.Description.Length >= 9 ? oldEmbed.Description[9..] : string.Empty))
			.WithThumbnailUrl(newThumbnailUrl ?? oldEmbed.Thumbnail?.Url ?? string.Empty)
			.WithAuthor(oldEmbed.Author!.Value.Name, oldEmbed.Author.Value.IconUrl, oldEmbed.Author.Value.Url)
			.WithColor(1)
			.Build();
	}
}

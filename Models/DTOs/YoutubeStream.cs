#pragma warning disable SA1313
namespace Swarmer.Models.DTOs
{
	public record YoutubeStream(
		string Title,
		string Username,
		string StreamUrl,
		string AvatarUrl,
		string ThumbnailUrl);
}

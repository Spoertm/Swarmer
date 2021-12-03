namespace Swarmer.Models;
#pragma warning disable SA1313
public record ActiveStream(
	string StreamId,
	string UserId,
	ulong DdPalsMessageId,
	ulong DdInfoMessageId,
	string OfflineThumbnailUrl);
#pragma warning restore SA1313
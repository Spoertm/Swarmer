namespace Swarmer.Models;

public record ActiveStream(
	string StreamId,
	string UserId,
	ulong DdpalsMessageId,
	ulong DdinfoMessageId,
	string OfflineThumbnailUrl);

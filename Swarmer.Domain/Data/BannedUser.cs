namespace Swarmer.Domain.Data;

public sealed class BannedUser
{
	public int Id { get; init; }

	public required string UserLogin { get; init; }
}

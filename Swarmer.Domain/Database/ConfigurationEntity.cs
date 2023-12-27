namespace Swarmer.Domain.Database;

public sealed class ConfigurationEntity
{
	public int Id { get; init; }

	public required string BotName { get; init; }

	public required string JsonConfig { get; set; }
}

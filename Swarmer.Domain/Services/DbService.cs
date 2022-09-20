using Microsoft.EntityFrameworkCore;
using Swarmer.Domain.Models.Database;

namespace Swarmer.Domain.Services;

public sealed class DbService : DbContext
{
	public DbSet<GameChannel> GameChannels => Set<GameChannel>();
	public DbSet<StreamMessage> StreamMessages => Set<StreamMessage>();
	public DbSet<SwarmerDbConfig> SwarmerConfig => Set<SwarmerDbConfig>();

	protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
	{
		optionsBuilder.UseNpgsql(Environment.GetEnvironmentVariable("PostgresConnectionString") ?? throw new ArgumentException("Envvar PostgresConnectionString not found."));
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<GameChannel>().HasKey(gc => new { gc.StreamChannelId, gc.TwitchGameId });
	}
}

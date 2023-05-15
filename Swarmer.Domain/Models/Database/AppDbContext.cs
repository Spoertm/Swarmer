using Microsoft.EntityFrameworkCore;

namespace Swarmer.Domain.Models.Database;

public sealed class AppDbContext : DbContext
{
	public AppDbContext()
	{
	}

	public AppDbContext(DbContextOptions options) : base(options)
	{
	}

	public DbSet<GameChannel> GameChannels => Set<GameChannel>();
	public DbSet<StreamMessage> StreamMessages => Set<StreamMessage>();
	public DbSet<SwarmerDbConfig> SwarmerConfig => Set<SwarmerDbConfig>();

	protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
	{
		if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") != "Test")
		{
			optionsBuilder.UseNpgsql(Environment.GetEnvironmentVariable("PostgresConnectionString") ?? throw new ArgumentException("Envvar PostgresConnectionString not found."));
		}
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<GameChannel>().HasKey(gc => new { gc.StreamChannelId, gc.TwitchGameId });
	}
}

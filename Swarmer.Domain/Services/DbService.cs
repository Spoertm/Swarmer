using Microsoft.EntityFrameworkCore;
using Swarmer.Domain.Models.Database;

namespace Swarmer.Domain.Services;

public class DbService : DbContext
{
	public DbSet<DdStreamChannel> DdStreamChannels => Set<DdStreamChannel>();
	public DbSet<StreamMessage> DdStreams => Set<StreamMessage>();
	public DbSet<SwarmerDbConfig> SwarmerConfig => Set<SwarmerDbConfig>();

	protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
	{
		optionsBuilder.UseNpgsql(Environment.GetEnvironmentVariable("PostgresConnectionString") ?? throw new ArgumentException("Envvar PostgresConnectionString not found."));
	}
}

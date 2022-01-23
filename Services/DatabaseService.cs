using Microsoft.EntityFrameworkCore;
using Swarmer.Models;

namespace Swarmer.Services;

public class DatabaseService : DbContext
{
	public DbSet<DdStreamChannel> DdStreamChannels => Set<DdStreamChannel>();
	public DbSet<DdStream> DdStreams => Set<DdStream>();

	protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
		=> optionsBuilder
			.UseNpgsql(Environment.GetEnvironmentVariable("PostgresConnectionString") ?? throw new ArgumentException("Envvar PostgresConnectionString not found."));
}

using Microsoft.EntityFrameworkCore;
using Swarmer.Models;

namespace Swarmer.Services;

public class DatabaseService : DbContext
{
	public DbSet<ActiveStream> ActiveDdStreams { get; set; } = null!;

	protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
		=> optionsBuilder
			.UseNpgsql(Environment.GetEnvironmentVariable("PostgresConnectionString")!)
			.UseSnakeCaseNamingConvention();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
		=> modelBuilder.Entity<ActiveStream>().HasKey(stream => stream.StreamId);
}

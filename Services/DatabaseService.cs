using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Swarmer.Models;

namespace Swarmer.Services;

public class DatabaseService : DbContext
{
	private readonly IConfiguration _config;
	public DbSet<ActiveStream> ActiveDdStreams { get; set; } = null!;

	public DatabaseService(IConfiguration config) => _config = config;

	protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
		=> optionsBuilder
			.UseNpgsql(_config["PostgresConnectionString"])
			.UseSnakeCaseNamingConvention();

	protected override void OnModelCreating(ModelBuilder modelBuilder)
		=> modelBuilder.Entity<ActiveStream>().HasKey(stream => stream.StreamId);
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Swarmer.Domain.Data;

public class AppDbContext : DbContext
{
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions options) : base(options)
    {
    }

    public DbSet<GameChannel> GameChannels => Set<GameChannel>();

    public DbSet<StreamMessage> StreamMessages => Set<StreamMessage>();

    public DbSet<BannedUser> BannedUsers => Set<BannedUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("swarmer");

        ConfigureGameChannel(modelBuilder.Entity<GameChannel>());
        ConfigureStreamMessage(modelBuilder.Entity<StreamMessage>());
        ConfigureBannedUser(modelBuilder.Entity<BannedUser>());
    }

    private static void ConfigureBannedUser(EntityTypeBuilder<BannedUser> builder)
    {
        builder.ToTable("BannedUsers");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedOnAdd();

        builder.Property(b => b.UserLogin)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(b => b.UserLogin).IsUnique();
    }

    private static void ConfigureGameChannel(EntityTypeBuilder<GameChannel> builder)
    {
        builder.ToTable("GameChannels");
        builder.HasKey(gc => new { gc.StreamChannelId, gc.TwitchGameId });
    }

    private static void ConfigureStreamMessage(EntityTypeBuilder<StreamMessage> builder)
    {
        builder.ToTable("StreamMessages");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedOnAdd();
        builder.Property(s => s.MessageId).IsRequired();
        builder.Property(s => s.ChannelId).IsRequired();
        builder.Property(s => s.IsLive).IsRequired();

        builder.Property(s => s.StreamId)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(s => s.OfflineThumbnailUrl).HasMaxLength(200);
        builder.Property(s => s.AvatarUrl).HasMaxLength(200);
    }
}

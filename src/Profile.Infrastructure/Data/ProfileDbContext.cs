using BuildingBlocks.Messaging.Inbox;
using Microsoft.EntityFrameworkCore;
using Profile.Application.Interfaces;
using Profile.Domain.Entities;

namespace Profile.Infrastructure.Data;

public class ProfileDbContext : DbContext, IProfileDbContext
{
    public ProfileDbContext(DbContextOptions<ProfileDbContext> options) : base(options) { }

    public DbSet<LearnerProfile> Profiles => Set<LearnerProfile>();
    public DbSet<LearnerPreferences> Preferences => Set<LearnerPreferences>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<LearnerProfile>(b =>
        {
            b.ToTable("LearnerProfiles");
            b.HasKey(p => p.Id);
            b.Property(p => p.Email).IsRequired().HasMaxLength(256);
            b.Property(p => p.DisplayName).IsRequired().HasMaxLength(100);
            b.Property(p => p.NativeLanguage).IsRequired().HasMaxLength(10);
            b.Property(p => p.TargetLanguage).IsRequired().HasMaxLength(10);
            b.Property(p => p.TargetCefrLevel).IsRequired().HasMaxLength(10);

            b.HasOne(p => p.Preferences)
             .WithOne(pr => pr.Profile)
             .HasForeignKey<LearnerPreferences>(pr => pr.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LearnerPreferences>(b =>
        {
            b.ToTable("LearnerPreferences");
            b.HasKey(pr => pr.UserId);
            b.Property(pr => pr.PreferredDifficulty).IsRequired().HasMaxLength(50);
        });

        modelBuilder.Entity<InboxMessage>(b =>
        {
            b.ToTable("InboxMessages");
            b.HasKey(i => new { i.EventId, i.ConsumerName });
            b.Property(i => i.ConsumerName).HasMaxLength(100);
        });
    }
}

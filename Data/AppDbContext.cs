using SFD.Models;
using Microsoft.EntityFrameworkCore;

namespace SFD.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<TeamMember> Members => Set<TeamMember>();
    public DbSet<GpsPoint> GpsPoints => Set<GpsPoint>();
    public DbSet<PlannedRoute> Routes => Set<PlannedRoute>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(254);
            e.Property(x => x.Name).HasMaxLength(100);
        });

        b.Entity<TeamMember>(e =>
        {
            e.HasIndex(x => x.OwnerId);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Area).HasMaxLength(100);
            e.Property(x => x.Color).HasMaxLength(20);
            e.Property(x => x.Phone).HasMaxLength(30);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<GpsPoint>(e =>
        {
            e.HasIndex(x => new { x.OwnerId, x.RecordedAtUtc });
            e.HasIndex(x => x.MemberId);
            e.HasOne<TeamMember>().WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PlannedRoute>(e =>
        {
            e.HasIndex(x => x.OwnerId);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Area).HasMaxLength(100);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}

using SFD.Models;
using Microsoft.EntityFrameworkCore;

namespace SFD.Data;

public class SFDAppDbContext(DbContextOptions<SFDAppDbContext> options) : DbContext(options)
{
    public DbSet<SFDAppUser> Users => Set<SFDAppUser>();
    public DbSet<SFDTeamMember> Members => Set<SFDTeamMember>();
    public DbSet<SFDGpsPoint> GpsPoints => Set<SFDGpsPoint>();
    public DbSet<SFDPlannedRoute> Routes => Set<SFDPlannedRoute>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<SFDAppUser>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(254);
            e.Property(x => x.Name).HasMaxLength(100);
        });

        b.Entity<SFDTeamMember>(e =>
        {
            e.HasIndex(x => x.OwnerId);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Area).HasMaxLength(100);
            e.Property(x => x.Color).HasMaxLength(20);
            e.Property(x => x.Phone).HasMaxLength(30);
            e.HasOne<SFDAppUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<SFDGpsPoint>(e =>
        {
            e.HasIndex(x => new { x.OwnerId, x.RecordedAtUtc });
            e.HasIndex(x => x.MemberId);
            e.HasOne<SFDTeamMember>().WithMany().HasForeignKey(x => x.MemberId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<SFDAppUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<SFDPlannedRoute>(e =>
        {
            e.HasIndex(x => x.OwnerId);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Area).HasMaxLength(100);
            e.HasOne<SFDAppUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}

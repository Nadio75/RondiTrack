namespace RondiTrack.Data;

using Microsoft.EntityFrameworkCore;
using RondiTrack.Models;

public class RondiTrackDbContext : DbContext
{
    public RondiTrackDbContext(DbContextOptions<RondiTrackDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Stokvel> Stokvels => Set<Stokvel>();
    public DbSet<StokvelMember> StokvelMembers => Set<StokvelMember>();
    public DbSet<ContributionCycle> ContributionCycles => Set<ContributionCycle>();
    public DbSet<Contribution> Contributions => Set<Contribution>();
    public DbSet<Payout> Payouts => Set<Payout>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Stokvel.MemberIds is a computed read-only view over a private field.
        // It cannot map to a column or a navigation, so it's explicitly ignored;
        // StokvelMember is the real relational source of truth for membership.
        // Every other property below is configured explicitly, not left to
        // convention discovery — see README for why.
        modelBuilder.Entity<Stokvel>(b =>
        {
            b.HasKey(s => s.Id);
            b.Ignore(s => s.MemberIds);
            b.Property(s => s.Id);
            b.Property(s => s.Name).IsRequired().HasMaxLength(100);
            b.Property(s => s.ContributionAmount).HasPrecision(18, 2);
        });

        modelBuilder.Entity<StokvelMember>(b =>
        {
            b.HasKey(m => m.Id);
            b.Property(m => m.Id);
            b.Property(m => m.StokvelId);
            b.Property(m => m.UserId);
            b.Property(m => m.JoinedAt);
            b.HasIndex(m => new { m.StokvelId, m.UserId }).IsUnique();
        });

        modelBuilder.Entity<User>(b =>
        {
            b.HasKey(u => u.Id);
            b.Property(u => u.Id);
            b.Property(u => u.Name).IsRequired().HasMaxLength(100);
            b.Property(u => u.ContactNumber).IsRequired();
        });

        modelBuilder.Entity<ContributionCycle>(b =>
        {
            b.HasKey(c => c.Id);
            b.Property(c => c.Id);
            b.Property(c => c.StokvelId);
            b.Property(c => c.Period).IsRequired();
            b.Property(c => c.TargetAmount).HasPrecision(18, 2);
            b.Property(c => c.CreatedAt);
            b.Property(c => c.Status).IsRequired();
            b.HasIndex(c => new { c.StokvelId, c.Period }).IsUnique();
        });

        modelBuilder.Entity<Contribution>(b =>
        {
            b.HasKey(c => c.Id);
            b.Property(c => c.Id);
            b.Property(c => c.StokvelId);
            b.Property(c => c.UserId);
            b.Property(c => c.Amount).HasPrecision(18, 2);
            b.Property(c => c.ContributionCycleId);
            b.Property(c => c.CreatedAt);
        });

        modelBuilder.Entity<Payout>(b =>
        {
            b.HasKey(p => p.Id);
            b.Property(p => p.Id);
            b.Property(p => p.StokvelId);
            b.Property(p => p.ContributionCycleId);
            b.Property(p => p.RecipientUserId);
            b.Property(p => p.Amount).HasPrecision(18, 2);
            b.Property(p => p.ProcessedAt);
        });
    }
}
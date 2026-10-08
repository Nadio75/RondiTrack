namespace RondiTrack.Infrastructure;

using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;

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
       modelBuilder.Entity<StokvelMember>(b =>
        {
            // Natural composite key: membership IS the pair (UserId, StokvelId).
            // A surrogate Guid here would be an id nobody asked for, hiding the
            // fact that a user can only belong to a given stokvel once.
            b.HasKey(m => new { m.UserId, m.StokvelId });
            b.Property(m => m.UserId);
            b.Property(m => m.StokvelId);
            b.Property(m => m.Role).IsRequired().HasMaxLength(50).HasDefaultValue("Member");
            b.Property(m => m.JoinedAtUtc);

            b.HasOne(m => m.User)
                .WithMany(u => u.Memberships)
                .HasForeignKey(m => m.UserId);

            b.HasOne(m => m.Stokvel)
                .WithMany(s => s.Memberships)
                .HasForeignKey(m => m.StokvelId);
        });

                modelBuilder.Entity<Stokvel>(b =>
        {
            b.HasKey(s => s.Id);
            b.Ignore(s => s.MemberIds);
            b.Property(s => s.Id);
            b.Property(s => s.Name).IsRequired().HasMaxLength(100);
            b.Property(s => s.ContributionAmount).HasPrecision(18, 2);
            b.Property(p => p.xmin)
    .IsRowVersion();
        });

        modelBuilder.Entity<User>(b =>
        {
            b.HasKey(u => u.Id);
            b.Property(u => u.Id);
            b.Property(u => u.Name).IsRequired().HasMaxLength(100);
            b.Property(u => u.ContactNumber).IsRequired();
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
            b.HasIndex(c => new { c.StokvelId, c.ContributionCycleId, c.CreatedAt, c.Id });
            // One contribution per member per cycle (already enforced in C# service layer)
            b.HasIndex(c => new { c.UserId, c.StokvelId, c.ContributionCycleId })
                .IsUnique();
            b.HasOne(c => c.Member)
                .WithMany()
                .HasForeignKey(c => new { c.UserId, c.StokvelId })
                .HasPrincipalKey(m => new { m.UserId, m.StokvelId });

            // The composite FK the FK question above resolved to: Contribution
            // already stored (UserId, StokvelId) — now it's a real, enforced
            // reference to a specific membership, not two coincidentally
            // matching columns.
           
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
           b.Property(p => p.xmin)
    .IsRowVersion();
            b.HasIndex(c => new { c.StokvelId, c.Period }).IsUnique();
            b.HasOne(c => c.Stokvel)
                .WithMany(s => s.ContributionCycles)
                .HasForeignKey(c => c.StokvelId);
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
            b.Property(p => p.xmin)
    .IsRowVersion();

            // One payout per cycle (matches the business rule that a cycle is paid out once)
            b.HasIndex(p => p.ContributionCycleId)
             .IsUnique();

            b.HasOne<StokvelMember>()
                .WithMany()
                .HasForeignKey(p => new { p.RecipientUserId, p.StokvelId })
                .HasPrincipalKey(m => new { m.UserId, m.StokvelId });
                
        });
        
    }
}

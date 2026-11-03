
namespace RondiTrack.Infrastructure;

using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;

public class RondiTrackDbContext : DbContext
{
    public RondiTrackDbContext(
        DbContextOptions<RondiTrackDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Stokvel> Stokvels => Set<Stokvel>();
    public DbSet<StokvelMember> StokvelMembers => Set<StokvelMember>();
    public DbSet<StokvelInvitation> StokvelInvitations => Set<StokvelInvitation>();
    public DbSet<ContributionCycle> ContributionCycles => Set<ContributionCycle>();
    public DbSet<Contribution> Contributions => Set<Contribution>();
    public DbSet<ContributionProof> ContributionProofs => Set<ContributionProof>();
    public DbSet<Payout> Payouts => Set<Payout>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ContributionArrear> ContributionArrears => Set<ContributionArrear>();
    public DbSet<CycleClosingRequest> CycleClosingRequests => Set<CycleClosingRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CycleClosingRequest>(b =>
        {
            b.HasKey(r => r.Id);

            b.Property(r => r.StokvelId).IsRequired();
            b.Property(r => r.CycleId).IsRequired();
            b.Property(r => r.RequestedByUserId).IsRequired();

            b.Property(r => r.IdempotencyKey)
                .IsRequired()
                .HasMaxLength(128);

            b.Property(r => r.SnapshotToken)
                .IsRequired()
                .HasMaxLength(64);

            b.Property(r => r.CompletedAtUtc).IsRequired();
            b.Property(r => r.TotalArrearsCents).IsRequired();

            b.HasIndex(r => new
            {
                r.StokvelId,
                r.RequestedByUserId,
                r.IdempotencyKey
            }).IsUnique();

            b.HasOne<Stokvel>()
                .WithMany()
                .HasForeignKey(r => r.StokvelId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne<ContributionCycle>()
                .WithMany()
                .HasForeignKey(r => r.CycleId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne<User>()
                .WithMany()
                .HasForeignKey(r => r.RequestedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        // Membership is represented by the StokvelMember table.
        // Stokvel.MemberIds is only a read-only view and is not mapped.
modelBuilder.Entity<ContributionProof>(b =>
{
    b.HasKey(p => p.Id);

    b.HasIndex(p => p.ContributionId)
        .IsUnique();

    b.Property(p => p.FileName)
        .IsRequired()
        .HasMaxLength(255);

    b.Property(p => p.ContentType)
        .IsRequired()
        .HasMaxLength(100);

    b.Property(p => p.FileData)
        .IsRequired()
        .HasColumnType("bytea");

    b.Property(p => p.UploadedAtUtc)
        .IsRequired();

    b.HasOne(p => p.Contribution)
        .WithMany()
        .HasForeignKey(p => p.ContributionId)
        .OnDelete(DeleteBehavior.Cascade);

    b.HasOne<User>()
        .WithMany()
        .HasForeignKey(p => p.UploadedByUserId)
        .OnDelete(DeleteBehavior.Restrict);
});

        modelBuilder.Entity<StokvelInvitation>(b =>
        {

            b.HasKey(i => i.Id);

            b.Property(i => i.CodeHash)
                .IsRequired()
                .HasMaxLength(64);

            b.HasIndex(i => i.CodeHash)
                .IsUnique();

            b.HasIndex(i => i.StokvelId);

            b.Property(i => i.CreatedAtUtc).IsRequired();
            b.Property(i => i.ExpiresAtUtc).IsRequired();

            b.HasOne(i => i.Stokvel)
                .WithMany()
                .HasForeignKey(i => i.StokvelId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne<User>()
                .WithMany()
                .HasForeignKey(i => i.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // -------------------------------------------------------
        // StokvelMember
        // -------------------------------------------------------

        modelBuilder.Entity<StokvelMember>(b =>
        {
            // A user can belong to a particular stokvel only once.
            b.HasKey(m => new { m.UserId, m.StokvelId });

            b.Property(m => m.UserId);
            b.Property(m => m.StokvelId);

            b.Property(m => m.Role)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue("Member");

            b.Property(m => m.JoinedAtUtc);

            b.HasOne(m => m.User)
                .WithMany(u => u.Memberships)
                .HasForeignKey(m => m.UserId);

            b.HasOne(m => m.Stokvel)
                .WithMany(s => s.Memberships)
                .HasForeignKey(m => m.StokvelId);
        });

        // -------------------------------------------------------
        // Stokvel
        // -------------------------------------------------------

        modelBuilder.Entity<Stokvel>(b =>
        {
	    b.ToTable(t => t.HasCheckConstraint(
    "CK_Stokvels_ClosingDayOfMonth",
    "\"ClosingDayOfMonth\" BETWEEN 1 AND 31"));
            b.HasKey(s => s.Id);

            b.Ignore(s => s.MemberIds);

            b.Property(s => s.Id);

            b.Property(s => s.Name)
                .IsRequired()
                .HasMaxLength(100);

            // Decimal is used for financial precision.
            b.Property(s => s.Rules)
                  .IsRequired()
                  .HasMaxLength(4000)
                  .HasDefaultValue("");

              b.Property(s => s.ContributionAmount)
                .HasPrecision(18, 2);

            // NEW: The day of the month when this stokvel
            // closes its contribution cycle.
            // Existing stokvels will default to day 25.
            b.Property(s => s.ClosingDayOfMonth)
                .IsRequired()
                .HasDefaultValue(25);

            // PostgreSQL optimistic concurrency token.
            b.Property(s => s.xmin)
                .IsRowVersion();
        });

        // -------------------------------------------------------
        // User
        // -------------------------------------------------------

        modelBuilder.Entity<User>(b =>
        {
            b.HasKey(u => u.Id);

            b.Property(u => u.Id);

            b.Property(u => u.Name)
                .IsRequired()
                .HasMaxLength(100);

            b.Property(u => u.ContactNumber)
                .IsRequired();

            b.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(320);

            b.Property(u => u.PasswordHash)
                .IsRequired();

            b.HasIndex(u => u.Email)
                .IsUnique();
        });

        // -------------------------------------------------------
        // Contribution
        // -------------------------------------------------------

        modelBuilder.Entity<Contribution>(b =>
        {
            b.HasKey(c => c.Id);

            b.Property(c => c.Id);
            b.Property(c => c.StokvelId);
            b.Property(c => c.UserId);

            b.Property(c => c.Amount)
                .HasPrecision(18, 2);

            b.Property(c => c.ContributionCycleId);
            b.Property(c => c.CreatedAt);

            // Supports efficient contribution pagination.
            b.HasIndex(c => new
            {
                c.StokvelId,
                c.ContributionCycleId,
                c.CreatedAt,
                c.Id
            });

            // One contribution per member per cycle.
            b.HasIndex(c => new
            {
                c.UserId,
                c.StokvelId,
                c.ContributionCycleId
            }).IsUnique();

            // A contribution must belong to a real membership.
            b.HasOne(c => c.Member)
                .WithMany()
                .HasForeignKey(c => new
                {
                    c.UserId,
                    c.StokvelId
                })
                .HasPrincipalKey(m => new
                {
                    m.UserId,
                    m.StokvelId
                });
        });

        // -------------------------------------------------------
        // ContributionCycle
        // -------------------------------------------------------

        modelBuilder.Entity<ContributionCycle>(b =>
        {
            b.HasKey(c => c.Id);

            b.Property(c => c.Id);
            b.Property(c => c.StokvelId);

            b.Property(c => c.Period)
                .IsRequired();

            b.Property(c => c.TargetAmount)
                .HasPrecision(18, 2);

            b.Property(c => c.CreatedAt);

            b.Property(c => c.Status)
                .IsRequired();

            // A closed cycle cannot accept new contributions.
            b.Property(c => c.IsClosed)
                .IsRequired()
                .HasDefaultValue(false);

            // UTC timestamp when the cycle was closed.
            // Null means the cycle is still open.
            b.Property(c => c.ClosedAtUtc)
                .HasColumnType("timestamp with time zone");

            // PostgreSQL optimistic concurrency token.
            b.Property(c => c.xmin)
                .IsRowVersion();

            // Only one cycle per stokvel per period.
            b.HasIndex(c => new
            {
                c.StokvelId,
                c.Period
            }).IsUnique();

            b.HasOne(c => c.Stokvel)
                .WithMany(s => s.ContributionCycles)
                .HasForeignKey(c => c.StokvelId);
        });

        // -------------------------------------------------------
        // Payout
        // -------------------------------------------------------

        modelBuilder.Entity<Payout>(b =>
        {
            b.HasKey(p => p.Id);

            b.Property(p => p.Id);
            b.Property(p => p.StokvelId);
            b.Property(p => p.ContributionCycleId);
            b.Property(p => p.RecipientUserId);

            b.Property(p => p.Amount)
                .HasPrecision(18, 2);

            b.Property(p => p.ProcessedAt);

            // PostgreSQL optimistic concurrency token.
            b.Property(p => p.xmin)
                .IsRowVersion();

            // One payout per contribution cycle.
            b.HasIndex(p => p.ContributionCycleId)
                .IsUnique();

            // The payout recipient must be a stokvel member.
            b.HasOne<StokvelMember>()
                .WithMany()
                .HasForeignKey(p => new
                {
                    p.RecipientUserId,
                    p.StokvelId
                })
                .HasPrincipalKey(m => new
                {
                    m.UserId,
                    m.StokvelId
                });
        });

        // -------------------------------------------------------
        // RefreshToken
        // -------------------------------------------------------

        modelBuilder.Entity<RefreshToken>(b =>
        {
            b.HasKey(t => t.Id);

            // Detect simultaneous refresh-token updates.
            b.Property(t => t.xmin)
                .IsRowVersion();

            b.Property(t => t.TokenHash)
                .IsRequired()
                .HasMaxLength(64);

            // A refresh token hash must be unique.
            b.HasIndex(t => t.TokenHash)
                .IsUnique();

            // Helps locate tokens belonging to one token family.
            b.HasIndex(t => t.FamilyId);

            b.Property(t => t.CreatedAtUtc)
                .IsRequired();

            b.Property(t => t.ExpiresAtUtc)
                .IsRequired();

            b.HasOne<User>()
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // -------------------------------------------------------
        // ContributionArrear
        // -------------------------------------------------------

        modelBuilder.Entity<ContributionArrear>(b =>
        {
            b.HasKey(a => a.Id);

            b.Property(a => a.StokvelId)
                .IsRequired();

            b.Property(a => a.ContributionCycleId)
                .IsRequired();

            b.Property(a => a.UserId)
                .IsRequired();

            // Store arrears as whole cents to avoid rounding errors.
            b.Property(a => a.AmountCents)
                .IsRequired();

            b.Property(a => a.RecordedAtUtc)
                .IsRequired();

            // A member can have only one arrears record
            // for a particular contribution cycle.
            b.HasIndex(a => new
            {
                a.ContributionCycleId,
                a.UserId
            }).IsUnique();

            // Every arrears record must belong to a real cycle.
            b.HasOne<ContributionCycle>()
                .WithMany()
                .HasForeignKey(a => a.ContributionCycleId)
                .OnDelete(DeleteBehavior.Restrict);

            // The member must belong to the referenced stokvel.
            b.HasOne<StokvelMember>()
                .WithMany()
                .HasForeignKey(a => new
                {
                    a.UserId,
                    a.StokvelId
                })
                .HasPrincipalKey(m => new
                {
                    m.UserId,
                    m.StokvelId
                })
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}


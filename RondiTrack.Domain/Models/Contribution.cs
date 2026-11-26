// Models/Contribution.cs
namespace RondiTrack.Domain;

public class Contribution
{
    public Guid Id { get; }
    public Guid StokvelId { get; }
    public Guid UserId { get; }
    public decimal Amount { get; }
    public Guid ContributionCycleId { get; }
    public DateTime CreatedAt { get; }

    public DateTime? ConfirmedAtUtc { get; private set; }

    public Guid? ConfirmedByUserId { get; private set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool IsConfirmed => ConfirmedAtUtc.HasValue;

    public bool Confirm(Guid treasurerId)
    {
        if (treasurerId == Guid.Empty)
            throw new ArgumentException(
                "A valid treasurer is required.",
                nameof(treasurerId));

        if (IsConfirmed)
            return false;

        ConfirmedByUserId = treasurerId;
        ConfirmedAtUtc = DateTime.UtcNow;

        return true;
    }

    private Contribution() { } // EF Core materializes instances via this, writing straight to the backing fields
    public StokvelMember Member { get; private set; } = null!;

    public Contribution(Guid stokvelId, Guid userId, decimal amount, Guid contributionCycleId)
    {
        if (stokvelId == Guid.Empty)
            throw new ArgumentException("A contribution must reference a valid stokvel.", nameof(stokvelId));
        if (userId == Guid.Empty)
            throw new ArgumentException("A contribution must reference a valid user.", nameof(userId));
        if (contributionCycleId == Guid.Empty)
            throw new ArgumentException("A contribution must reference a valid contribution cycle.", nameof(contributionCycleId));
        if (amount <= 0)
            throw new ArgumentException("Contribution amount must be greater than zero.", nameof(amount));

        Id = Guid.NewGuid();
        StokvelId = stokvelId;
        UserId = userId;
        Amount = amount;
        ContributionCycleId = contributionCycleId;
        CreatedAt = DateTime.UtcNow;
    }
}


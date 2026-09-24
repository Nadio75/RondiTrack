// Models/Contribution.cs
namespace RondiTrack.Models;

public class Contribution
{
    public Guid Id { get; }
    public Guid StokvelId { get; }
    public Guid UserId { get; }
    public decimal Amount { get; }
    public Guid ContributionCycleId { get; }
    public DateTime CreatedAt { get; }

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
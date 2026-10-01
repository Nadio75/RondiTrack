namespace RondiTrack.Models;

public class Payout
{
    public Guid Id { get; private set; }
    public Guid StokvelId { get; private set; }
    public Guid ContributionCycleId { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime ProcessedAt { get; private set; }

    private Payout() { }

    public Payout(Guid stokvelId, Guid contributionCycleId, Guid recipientUserId, decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Payout amount must be greater than zero.", nameof(amount));

        Id = Guid.NewGuid();
        StokvelId = stokvelId;
        ContributionCycleId = contributionCycleId;
        RecipientUserId = recipientUserId;
        Amount = amount;
        ProcessedAt = DateTime.UtcNow;
    }
}
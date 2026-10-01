// Models/ContributionCycle.cs
namespace RondiTrack.Models;

public class ContributionCycle
{
    private ContributionCycle() { } // EF Core materializes instances via this, writing straight to the backing fields
    public Guid Id { get; }
    public Guid StokvelId { get; }
    public string Period { get; private set; }
    public decimal TargetAmount { get; private set; }
    public DateTime CreatedAt { get; }
    public string Status { get; private set; } = "Open";


    public ContributionCycle(Guid stokvelId, string period, decimal targetAmount)
    {
        if (stokvelId == Guid.Empty)
            throw new ArgumentException("A contribution cycle must reference a valid stokvel.", nameof(stokvelId));
        if (string.IsNullOrWhiteSpace(period))
            throw new ArgumentException("A contribution cycle must specify a period.", nameof(period));
        if (targetAmount <= 0)
            throw new ArgumentException("Target amount must be greater than zero.", nameof(targetAmount));

        Id = Guid.NewGuid();
        StokvelId = stokvelId;
        Period = period;
        TargetAmount = targetAmount;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdatePeriod(string period)
    {
        if (string.IsNullOrWhiteSpace(period))
            throw new ArgumentException("A contribution cycle must specify a period.", nameof(period));
        Period = period;
    }

    public void UpdateTargetAmount(decimal targetAmount)
    {
        if (targetAmount <= 0)
            throw new ArgumentException("Target amount must be greater than zero.", nameof(targetAmount));
        TargetAmount = targetAmount;
    }
        public void MarkPaidOut()
    {
        if (Status == "PaidOut")
            throw new InvalidOperationException("This cycle has already been paid out.");
        Status = "PaidOut";
    }
}
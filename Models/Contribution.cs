namespace RondiTrack.Models;

public class Contribution
{
    public Guid Id { get; }
    public Guid StokvelId { get; }
    public Guid UserId { get; }
    public decimal Amount { get; }
    public string CycleMonth { get; }
    public DateTime CreatedAt { get; }

    public Contribution(Guid stokvelId, Guid userId, decimal amount, string cycleMonth)
    {
        // Guards: a contribution is meaningless without a real stokvel, a real user, and a cycle label.
        if (stokvelId == Guid.Empty)
        {
            throw new ArgumentException("A contribution must reference a valid stokvel.", nameof(stokvelId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A contribution must reference a valid user.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(cycleMonth))
        {
            throw new ArgumentException("A contribution must specify a cycle month.", nameof(cycleMonth));
        }

        if (amount <= 0)
        {
            throw new ArgumentException("Contribution amount must be greater than zero.", nameof(amount));
        }

        // Only once everything is valid do we actually build the object.

        Id = Guid.NewGuid();
        StokvelId = stokvelId;
        UserId = userId;
        Amount = amount;
        CycleMonth = cycleMonth;
        CreatedAt = DateTime.UtcNow;// the system decides when this happened, not the caller
    }
    }

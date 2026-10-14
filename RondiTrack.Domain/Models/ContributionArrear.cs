namespace RondiTrack.Domain;

public class ContributionArrear
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid StokvelId { get; private set; }

    public Guid ContributionCycleId { get; private set; }

    public Guid UserId { get; private set; }

    // Outstanding amount stored as whole cents.
    public long AmountCents { get; private set; }

    public DateTime RecordedAtUtc { get; private set; }

    private ContributionArrear() { }

    public ContributionArrear(
        Guid stokvelId,
        Guid contributionCycleId,
        Guid userId,
        long amountCents,
        DateTime recordedAtUtc)
    {
        if (stokvelId == Guid.Empty)
            throw new ArgumentException("A stokvel is required.");

        if (contributionCycleId == Guid.Empty)
            throw new ArgumentException("A contribution cycle is required.");

        if (userId == Guid.Empty)
            throw new ArgumentException("A member is required.");

        if (amountCents <= 0)
            throw new ArgumentException(
                "Arrears must be greater than zero cents.");

        if (recordedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException(
                "The recording time must be UTC.");

        StokvelId = stokvelId;
        ContributionCycleId = contributionCycleId;
        UserId = userId;
        AmountCents = amountCents;
        RecordedAtUtc = recordedAtUtc;
    }
}
namespace RondiTrack.Domain;

public sealed class CycleClosingRequest
{
    public Guid Id { get; private set; }
    public Guid StokvelId { get; private set; }
    public Guid CycleId { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string SnapshotToken { get; private set; } = string.Empty;
    public DateTime CompletedAtUtc { get; private set; }
    public long TotalArrearsCents { get; private set; }

    private CycleClosingRequest() { }

    public CycleClosingRequest(
        Guid stokvelId,
        Guid cycleId,
        Guid requestedByUserId,
        string idempotencyKey,
        string snapshotToken,
        DateTime completedAtUtc,
        long totalArrearsCents)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException(
                "An idempotency key is required.",
                nameof(idempotencyKey));

        if (string.IsNullOrWhiteSpace(snapshotToken))
            throw new ArgumentException(
                "A snapshot token is required.",
                nameof(snapshotToken));

        Id = Guid.NewGuid();
        StokvelId = stokvelId;
        CycleId = cycleId;
        RequestedByUserId = requestedByUserId;
        IdempotencyKey = idempotencyKey;
        SnapshotToken = snapshotToken;
        CompletedAtUtc = completedAtUtc;
        TotalArrearsCents = totalArrearsCents;
    }
}

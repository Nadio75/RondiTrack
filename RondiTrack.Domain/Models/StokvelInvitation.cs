namespace RondiTrack.Domain;

public sealed class StokvelInvitation
{
    private StokvelInvitation() { }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid StokvelId { get; private set; }

    // EF Core uses this to navigate from an invitation to its stokvel.
    public Stokvel Stokvel { get; private set; } = null!;

    public Guid CreatedByUserId { get; private set; }

    public string CodeHash { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public StokvelInvitation(
        Guid stokvelId,
        Guid createdByUserId,
        string codeHash,
        DateTime createdAtUtc,
        DateTime expiresAtUtc)
    {
        if (stokvelId == Guid.Empty)
            throw new ArgumentException("A valid stokvel is required.");

        if (createdByUserId == Guid.Empty)
            throw new ArgumentException("A valid creator is required.");

        if (string.IsNullOrWhiteSpace(codeHash))
            throw new ArgumentException("An invitation code hash is required.");

        if (createdAtUtc.Kind != DateTimeKind.Utc ||
            expiresAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Invitation dates must be UTC.");

        if (expiresAtUtc <= createdAtUtc)
            throw new ArgumentException("Invitation expiry must be in the future.");

        StokvelId = stokvelId;
        CreatedByUserId = createdByUserId;
        CodeHash = codeHash;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public bool IsExpired(DateTime utcNow) =>
        utcNow >= ExpiresAtUtc;
}

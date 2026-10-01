namespace RondiTrack.Models;

// A relational record of one user's membership in one stokvel.
// This exists so membership can be queried and constrained at the database
// level (a UNIQUE index on StokvelId+UserId), separately from how the
// Stokvel domain object exposes MemberIds to the rest of the app.
public class StokvelMember
{
    public Guid Id { get; private set; }
    public Guid StokvelId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime JoinedAt { get; private set; }

    private StokvelMember() { } // EF Core needs this

    public StokvelMember(Guid stokvelId, Guid userId)
    {
        Id = Guid.NewGuid();
        StokvelId = stokvelId;
        UserId = userId;
        JoinedAt = DateTime.UtcNow;
    }
}
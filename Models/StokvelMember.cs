namespace RondiTrack.Models;

// No surrogate Id: this entity's identity IS the pair (UserId, StokvelId).
// Membership isn't a bare link — it carries data of its own (Role, when it
// started), which is exactly why it has to be a real entity, not an
// implicit many-to-many EF Core would build silently if asked to.
public class StokvelMember
{
    public Guid UserId { get; private set; }
    public Guid StokvelId { get; private set; }
    public string Role { get; private set; } = "Member";
    public DateTime JoinedAtUtc { get; private set; }

    public User User { get; private set; } = null!;
    public Stokvel Stokvel { get; private set; } = null!;

    private StokvelMember() { } // EF Core materializes instances via this

    public StokvelMember(Guid stokvelId, Guid userId, string role = "Member")
    {
        StokvelId = stokvelId;
        UserId = userId;
        Role = role;
        JoinedAtUtc = DateTime.UtcNow;
    }
}
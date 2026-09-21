namespace RondiTrack.Models;

public class Stokvel
{
    public Guid Id { get; }
    public string Name { get; private set; }

    // decimal, not float or double, because this is money. 
    public decimal ContributionAmount { get; private set; }

    // This is the REAL list of member user IDs, kept private.
    // "Private" here is doing important work: no outside code can reach in
    // and do stokvel._memberIds.Add(...) or .Clear() directly, bypassing
    // our rules entirely. The list can ONLY be changed through AddMember/
    // RemoveMember below, where we enforce the "no duplicate members" rule.
    private readonly List<Guid> _memberIds = new();

    // This is what outside code actually sees when it reads Stokvel.MemberIds.
    // IReadOnlyCollection<Guid> means: you can look at what's in here,
    // loop over it, count it — but you cannot call .Add() or .Remove() on it.
    // It's a "read-only window" onto the private list above.
    public IReadOnlyCollection<Guid> MemberIds => _memberIds.AsReadOnly();

    public Stokvel(string name, decimal contributionAmount)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A stokvel must have a name.", nameof(name));
        }

        // A contribution amount of zero or less makes no real-world sense —
        // you can't have a savings group where nobody contributes anything,
        // or where the number is negative. We stop that at the door.
        if (contributionAmount <= 0)
        {
            throw new ArgumentException(
                "Contribution amount must be greater than zero.",
                nameof(contributionAmount));
        }

        Id = Guid.NewGuid();
        Name = name;
        ContributionAmount = contributionAmount;

        // Note: we do NOT add any members here. A brand-new stokvel starts
        // with zero members, and that's fine — it becomes valid membership-wise
        // as people join, which is handled by AddMember below.
    }

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            throw new ArgumentException("A stokvel's name cannot be blank.", nameof(newName));
        }

        Name = newName;
    }

    //This is the part where memebers are being added to the stokvel. We check for duplicates and throw an exception if the user is already a member. 
    public void AddMember(Guid userId)
    {
        // Contains() checks the private list — if this userId is already
        // in there, we refuse the operation instead of silently allowing
        // a duplicate (which would be meaningless — you can't be "double"
        // a member of the same group).
        if (_memberIds.Contains(userId))
        {
            throw new InvalidOperationException(
                "This user is already a member of this stokvel.");
        }

        _memberIds.Add(userId);
    }

    // The reverse operation: removing a member. We check they're actually
    // IN the group first, so we don't pretend a no-op succeeded.
    public void RemoveMember(Guid userId)
    {
        if (!_memberIds.Contains(userId))
        {
            throw new InvalidOperationException(
                "This user is not a member of this stokvel.");
        }

        _memberIds.Remove(userId);
    }

    public void UpdateContributionAmount(decimal newAmount)
    {
        if (newAmount <= 0)
        {
            throw new ArgumentException(
                "Contribution amount must be greater than zero.",
                nameof(newAmount));
        }

        ContributionAmount = newAmount;
    }
}
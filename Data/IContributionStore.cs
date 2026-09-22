namespace RondiTrack.Data;

using RondiTrack.Models;

public interface IContributionStore
{
    Task<IEnumerable<Contribution>> GetAllAsync();
    // Used by the duplicate-payment check: does this user already have a contribution for this stokvel+cycle?
    Task<Contribution?> FindAsync(Guid stokvelId, Guid userId, string cycleMonth);
    Task AddAsync(Contribution contribution);
}
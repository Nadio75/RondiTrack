// Data/IContributionStore.cs
namespace RondiTrack.Domain;


public interface IContributionStore
{
    Task<IEnumerable<Contribution>> GetAllAsync();
    // Now matches on ContributionCycleId instead of a raw CycleMonth string.
    Task<Contribution?> FindAsync(Guid stokvelId, Guid userId, Guid contributionCycleId);
    Task AddAsync(Contribution contribution);
}

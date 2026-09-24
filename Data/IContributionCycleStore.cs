// Data/IContributionCycleStore.cs
namespace RondiTrack.Data;

using RondiTrack.Models;

public interface IContributionCycleStore
{
    Task<IEnumerable<ContributionCycle>> GetAllByStokvelAsync(Guid stokvelId);
    Task<ContributionCycle?> GetByIdAsync(Guid id);
    // Used by the duplicate-period check on create.
    Task<ContributionCycle?> FindByStokvelAndPeriodAsync(Guid stokvelId, string period);
    Task AddAsync(ContributionCycle cycle);
    Task<bool> DeleteAsync(Guid id);
}
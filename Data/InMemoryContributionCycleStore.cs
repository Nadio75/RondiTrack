// Data/InMemoryContributionCycleStore.cs
namespace RondiTrack.Data;

using RondiTrack.Models;

public class InMemoryContributionCycleStore : IContributionCycleStore
{
    private readonly List<ContributionCycle> _cycles = new();

    public Task<IEnumerable<ContributionCycle>> GetAllByStokvelAsync(Guid stokvelId) =>
        Task.FromResult<IEnumerable<ContributionCycle>>(_cycles.Where(c => c.StokvelId == stokvelId));

    public Task<ContributionCycle?> GetByIdAsync(Guid id) =>
        Task.FromResult(_cycles.FirstOrDefault(c => c.Id == id));

    public Task<ContributionCycle?> FindByStokvelAndPeriodAsync(Guid stokvelId, string period) =>
        Task.FromResult(_cycles.FirstOrDefault(c => c.StokvelId == stokvelId && c.Period == period));

    public Task AddAsync(ContributionCycle cycle)
    {
        _cycles.Add(cycle);
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(Guid id)
    {
        var cycle = _cycles.FirstOrDefault(c => c.Id == id);
        if (cycle is null) return Task.FromResult(false);
        _cycles.Remove(cycle);
        return Task.FromResult(true);
    }
}
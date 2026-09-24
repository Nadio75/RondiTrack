namespace RondiTrack.Data;

using RondiTrack.Models;

public class InMemoryContributionStore : IContributionStore
{
    private readonly List<Contribution> _contributions = new();

    public Task<IEnumerable<Contribution>> GetAllAsync() =>
        Task.FromResult<IEnumerable<Contribution>>(_contributions);

    public Task<Contribution?> FindAsync(Guid stokvelId, Guid userId, Guid contributionCycleId)
    {
        var match = _contributions.FirstOrDefault(c =>
            c.StokvelId == stokvelId && c.UserId == userId && c.ContributionCycleId == contributionCycleId);
        return Task.FromResult(match);
    }

    public Task AddAsync(Contribution contribution)
    {
        _contributions.Add(contribution);
        return Task.CompletedTask;
    }
}
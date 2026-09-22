namespace RondiTrack.Data;

using RondiTrack.Models;

public class InMemoryContributionStore : IContributionStore
{
    // Our in-memory "table" of every recorded contribution.
    private readonly List<Contribution> _contributions = new();

    public Task<IEnumerable<Contribution>> GetAllAsync() =>
        Task.FromResult<IEnumerable<Contribution>>(_contributions);

    public Task<Contribution?> FindAsync(Guid stokvelId, Guid userId, string cycleMonth)
    {
        // Matches on all three fields — this IS the duplicate-payment check.
        var match = _contributions.FirstOrDefault(c =>
            c.StokvelId == stokvelId && c.UserId == userId && c.CycleMonth == cycleMonth);
        return Task.FromResult(match);
    }

    public Task AddAsync(Contribution contribution)
    {
        _contributions.Add(contribution);
        return Task.CompletedTask;
    }
}
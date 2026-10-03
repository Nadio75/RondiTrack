// Data/EfContributionCycleStore.cs
namespace RondiTrack.Data;

using Microsoft.EntityFrameworkCore;
using RondiTrack.Models;

public class EfContributionCycleStore : IContributionCycleStore
{
    private readonly RondiTrackDbContext _db;

    public EfContributionCycleStore(RondiTrackDbContext db) => _db = db;

    public async Task<IEnumerable<ContributionCycle>> GetAllByStokvelAsync(Guid stokvelId) =>
        await _db.ContributionCycles.Where(c => c.StokvelId == stokvelId).ToListAsync();

    public async Task<ContributionCycle?> GetByIdAsync(Guid id) =>
        await _db.ContributionCycles.FindAsync(id);

    public async Task<ContributionCycle?> FindByStokvelAndPeriodAsync(Guid stokvelId, string period) =>
        await _db.ContributionCycles.FirstOrDefaultAsync(c => c.StokvelId == stokvelId && c.Period == period);

    public async Task AddAsync(ContributionCycle cycle)
    {
        _db.ContributionCycles.Add(cycle);
        await _db.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var cycle = await _db.ContributionCycles.FindAsync(id);
        if (cycle is null) return false;
        _db.ContributionCycles.Remove(cycle);
        await _db.SaveChangesAsync();
        return true;
    }
        public async Task<ContributionCycle?> GetByIdReadOnlyAsync(Guid id) =>
        await _db.ContributionCycles.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
}
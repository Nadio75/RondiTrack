using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;

namespace RondiTrack.Infrastructure.Data;

public sealed class EfContributionStore : IContributionStore
{
    private readonly RondiTrackDbContext _db;

    public EfContributionStore(RondiTrackDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<Contribution>> GetAllAsync()
    {
        return await _db.Contributions
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Contribution?> FindAsync(
        Guid stokvelId,
        Guid userId,
        Guid contributionCycleId)
    {
        return await _db.Contributions
            .AsNoTracking()
            .FirstOrDefaultAsync(c =>
                c.StokvelId == stokvelId &&
                c.UserId == userId &&
                c.ContributionCycleId == contributionCycleId);
    }

    public async Task AddAsync(Contribution contribution)
    {
        _db.Contributions.Add(contribution);
        await _db.SaveChangesAsync();
    }
}

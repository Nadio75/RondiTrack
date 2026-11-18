using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;

namespace RondiTrack.Infrastructure.Data;

public sealed class EfIdempotencyStore : IIdempotencyStore
{
    private readonly RondiTrackDbContext _db;

    public EfIdempotencyStore(RondiTrackDbContext db)
    {
        _db = db;
    }

    public async Task<IdempotencyRecord?> FindAsync(string key)
    {
        return await _db.IdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(record => record.Key == key);
    }

    public async Task SaveAsync(IdempotencyRecord record)
    {
        _db.IdempotencyRecords.Add(record);
        await _db.SaveChangesAsync();
    }
}

namespace RondiTrack.Data;

public class InMemoryIdempotencyStore : IIdempotencyStore
{
    // Keyed dictionary for fast lookup by the Idempotency-Key header value.
    private readonly Dictionary<string, IdempotencyRecord> _records = new();

    public Task<IdempotencyRecord?> FindAsync(string key)
    {
        _records.TryGetValue(key, out var record);
        return Task.FromResult(record);
    }

    public Task SaveAsync(IdempotencyRecord record)
    {
        _records[record.Key] = record;
        return Task.CompletedTask;
    }
}
namespace RondiTrack.Data;

public interface IIdempotencyStore
{
    Task<IdempotencyRecord?> FindAsync(string key);
    Task SaveAsync(IdempotencyRecord record);
}
// Data/IdempotencyRecord.cs
namespace RondiTrack.Data;

// What we remember about a previously-handled request, so a retry can return the exact same result.
public class IdempotencyRecord
{
    public required string Key { get; init; }
    public required string RequestHash { get; init; }
    public required int ResponseStatusCode { get; init; }
    public required string ResponseBodyJson { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
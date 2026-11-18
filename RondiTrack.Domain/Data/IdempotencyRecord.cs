namespace RondiTrack.Domain;

public class IdempotencyRecord
{
    public required string Key { get; init; }

    public required string RequestHash { get; init; }

    public required int ResponseStatusCode { get; init; }

    public required string ResponseBodyJson { get; init; }

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public DateTime ExpiresAtUtc { get; init; } =
        DateTime.UtcNow.AddDays(30);
}

namespace RondiTrack.Models.Dtos;

public record PayoutResponse(
    Guid Id,
    Guid StokvelId,
    Guid ContributionCycleId,
    Guid RecipientUserId,
    decimal Amount,
    DateTime ProcessedAt,
    uint Version);
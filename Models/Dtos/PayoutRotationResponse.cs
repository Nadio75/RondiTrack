namespace RondiTrack.Models.Dtos;

/// <summary>
/// One member's server-assigned position in the payout rotation.
/// </summary>
public sealed record PayoutRotationEntryResponse(
    Guid MemberId,
    string MemberName,
    int Position,
    long AmountCents,
    DateOnly? ScheduledDate,
    string Status,
    bool IsNext
);

/// <summary>
/// Server-owned payout rotation for a contribution cycle.
/// Entries must be returned in rotation order.
/// </summary>
public sealed record PayoutRotationResponse(
    Guid StokvelId,
    Guid CycleId,
    IReadOnlyList<PayoutRotationEntryResponse> Entries
);

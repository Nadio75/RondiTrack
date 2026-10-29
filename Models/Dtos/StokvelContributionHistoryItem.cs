namespace RondiTrack.Models.Dtos;

/// <summary>
/// One contribution shown in a stokvel's contribution history.
/// </summary>
public sealed record StokvelContributionHistoryItem(
    Guid Id,
    Guid StokvelId,
    Guid ContributionCycleId,
    Guid MemberId,
    string MemberName,
    decimal Amount,
    DateTime CreatedAt
);

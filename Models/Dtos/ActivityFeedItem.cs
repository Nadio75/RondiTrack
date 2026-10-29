namespace RondiTrack.Models.Dtos;

/// <summary>
/// One event in the authenticated member's activity feed.
/// </summary>
public sealed record ActivityFeedItem(
    Guid Id,
    Guid StokvelId,
    string StokvelName,
    string ActivityType,
    DateTime OccurredAt,
    Guid? MemberId,
    string? MemberName,
    decimal? Amount
);

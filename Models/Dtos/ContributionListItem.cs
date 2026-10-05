namespace RondiTrack.Models.Dtos;

public record ContributionListItem(
    Guid Id,
    decimal Amount,
    string MemberName,
    DateTime CreatedAt
);
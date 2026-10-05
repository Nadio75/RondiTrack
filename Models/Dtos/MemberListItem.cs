namespace RondiTrack.Models.Dtos;

public record MemberListItem(
    Guid UserId,
    string Name,
    string Role,
    DateTime JoinedAtUtc
);
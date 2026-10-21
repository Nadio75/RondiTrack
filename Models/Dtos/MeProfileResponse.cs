namespace RondiTrack.Models.Dtos;

public sealed record MeProfileResponse(
    Guid Id,
    string Name,
    string Email);
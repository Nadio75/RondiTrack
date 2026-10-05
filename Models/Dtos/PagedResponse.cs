namespace RondiTrack.Models.Dtos;

public record PagedResponse<T>(
    IReadOnlyList<T> Items,
    string? NextPageToken
);
namespace RondiTrack.Models.Dtos;

public record UpdatePayoutRequest(
    decimal Amount,
    uint Version);
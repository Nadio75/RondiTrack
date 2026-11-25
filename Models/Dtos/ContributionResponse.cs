using RondiTrack.Domain;

namespace RondiTrack.Models.Dtos;

public record ContributionResponse(
    Guid Id,
    Guid StokvelId,
    Guid UserId,
    decimal Amount,
    Guid ContributionCycleId,
    DateTime CreatedAt)
{
    public bool IsConfirmed { get; init; }

    public DateTime? ConfirmedAtUtc { get; init; }

    public static ContributionResponse FromContribution(
        Contribution contribution) =>
        new(
            contribution.Id,
            contribution.StokvelId,
            contribution.UserId,
            contribution.Amount,
            contribution.ContributionCycleId,
            contribution.CreatedAt)
        {
            IsConfirmed = contribution.IsConfirmed,
            ConfirmedAtUtc = contribution.ConfirmedAtUtc
        };
}

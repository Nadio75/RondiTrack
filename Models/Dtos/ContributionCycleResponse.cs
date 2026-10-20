
using RondiTrack.Domain;

namespace RondiTrack.Models.Dtos;

public record ContributionCycleResponse(
    Guid Id,
    Guid StokvelId,
    string Period,
    decimal TargetAmount,
    DateTime CreatedAt)
{
    public string Status { get; init; } = "Open";

    public bool IsClosed { get; init; }

    public DateTime? ClosedAtUtc { get; init; }


public DateTime? DueAtUtc { get; init; }

public long TotalCollectedCents { get; init; }

public long AmountDueCents { get; init; }


    public static ContributionCycleResponse FromContributionCycle(
        ContributionCycle cycle) =>
        new(
            cycle.Id,
            cycle.StokvelId,
            cycle.Period,
            cycle.TargetAmount,
            cycle.CreatedAt)
        {
            Status = cycle.IsClosed && cycle.Status == "Open"
                ? "Closed"
                : cycle.Status,

            IsClosed = cycle.IsClosed,
            ClosedAtUtc = cycle.ClosedAtUtc
        };
}

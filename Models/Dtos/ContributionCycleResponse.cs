// Models/Dtos/ContributionCycleResponse.cs
namespace RondiTrack.Models;

public record ContributionCycleResponse(Guid Id, Guid StokvelId, string Period, decimal TargetAmount, DateTime CreatedAt)
{
    public static ContributionCycleResponse FromContributionCycle(ContributionCycle cycle) =>
        new(cycle.Id, cycle.StokvelId, cycle.Period, cycle.TargetAmount, cycle.CreatedAt);
}
// Models/Dtos/ContributionResponse.cs
namespace RondiTrack.Models;

public record ContributionResponse(Guid Id, Guid StokvelId, Guid UserId, decimal Amount, Guid ContributionCycleId, DateTime CreatedAt)
{
    public static ContributionResponse FromContribution(Contribution contribution) =>
        new(contribution.Id, contribution.StokvelId, contribution.UserId, contribution.Amount, contribution.ContributionCycleId, contribution.CreatedAt);
}
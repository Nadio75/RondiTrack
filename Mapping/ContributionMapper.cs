// Mapping/ContributionMapper.cs
namespace RondiTrack.Mapping;

using RondiTrack.Models;

public static class ContributionMapper
{
    public static ContributionResponse ToResponse(Contribution contribution) => ContributionResponse.FromContribution(contribution);
}
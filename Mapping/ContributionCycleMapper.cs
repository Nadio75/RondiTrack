using RondiTrack.Models.Dtos;
using RondiTrack.Domain;
// Mapping/ContributionCycleMapper.cs
namespace RondiTrack.Mapping;

using RondiTrack.Models;

public static class ContributionCycleMapper
{
    public static ContributionCycleResponse ToResponse(ContributionCycle cycle) =>
        ContributionCycleResponse.FromContributionCycle(cycle);
}

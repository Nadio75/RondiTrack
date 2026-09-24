// Models/Dtos/CreateContributionCycleRequest.cs
namespace RondiTrack.Models;

// StokvelId comes from the route, not this body — same pattern as AddMemberRequest.
public record CreateContributionCycleRequest(string Period, decimal TargetAmount);
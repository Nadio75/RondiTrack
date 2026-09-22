namespace RondiTrack.Models;

// A 'record' is a lightweight class built for holding data, with built-in
// equality and a compact syntax. Perfect for "just carry these values."
public record CreateUserRequest(string Name, string ContactNumber);
public record CreateStokvelRequest(string Name, decimal ContributionAmount);
public record AddMemberRequest(Guid UserId);
public record UpdateContributionRequest(decimal ContributionAmount);

// What a caller sends in to record a contribution — StokvelId comes from the route.
public record ContributionRequest(Guid UserId, decimal Amount, string CycleMonth);
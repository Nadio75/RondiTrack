
namespace RondiTrack.Models;

public record CreateStokvelRequest(
    string Name,
    decimal ContributionAmount
);

public record AddMemberRequest(Guid UserId);

public record UpdateContributionRequest(
    decimal ContributionAmount
);

public record ContributionRequest(
    Guid UserId,
    decimal Amount,
    Guid ContributionCycleId
);

// Assignment 7.4: request body for the new cycle-specific endpoint.
public record RecordCycleContributionRequest(
    Guid UserId,
    int AmountInCents
);

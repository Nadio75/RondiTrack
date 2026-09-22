namespace RondiTrack.Models;

public record ContributionResponse(
    Guid Id,
    Guid StokvelId,
    Guid UserId,
    decimal Amount,
    string CycleMonth,
    DateTime CreatedAt)
{
    // Converts a Contribution entity into the shape returned over HTTP.
    public static ContributionResponse FromContribution(Contribution contribution) =>
        new(
            contribution.Id,
            contribution.StokvelId,
            contribution.UserId,
            contribution.Amount,
            contribution.CycleMonth,
            contribution.CreatedAt);
}
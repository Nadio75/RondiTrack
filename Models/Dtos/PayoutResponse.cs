namespace RondiTrack.Models.Dtos;

public record PayoutResponse(Guid Id, Guid StokvelId, Guid ContributionCycleId, Guid RecipientUserId, decimal Amount, DateTime ProcessedAt)
{
    public static PayoutResponse FromPayout(Payout payout) =>
        new(payout.Id, payout.StokvelId, payout.ContributionCycleId, payout.RecipientUserId, payout.Amount, payout.ProcessedAt);
}
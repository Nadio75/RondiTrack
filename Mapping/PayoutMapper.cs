namespace RondiTrack.Mapping;

using RondiTrack.Models;
using RondiTrack.Models.Dtos;

public static class PayoutMapper
{
    public static PayoutResponse ToResponse(Payout payout) =>
        new(
            payout.Id,
            payout.StokvelId,
            payout.ContributionCycleId,
            payout.RecipientUserId,
            payout.Amount,
            payout.ProcessedAt,
            payout.xmin
        );
}
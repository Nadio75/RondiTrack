namespace RondiTrack.Mapping;

using RondiTrack.Models;
using RondiTrack.Models.Dtos;

public static class PayoutMapper
{
    public static PayoutResponse ToResponse(Payout payout) => PayoutResponse.FromPayout(payout);
}
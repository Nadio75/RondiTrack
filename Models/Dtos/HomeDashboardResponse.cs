
namespace RondiTrack.Models.Dtos;

public sealed record HomeDashboardResponse(
    IReadOnlyList<HomeStokvelResponse> Stokvels);

public sealed record HomeStokvelResponse(
    Guid StokvelId,
    string Name,
    string Role,
    string ContributionAmount,
    HomeCycleResponse? CurrentCycle)
{
    public long ContributionAmountCents { get; init; }
}

public sealed record HomeCycleResponse(
    Guid CycleId,
    string Period,
    string Status,
    bool HasPaid,
    string AmountOwed,
    DateTime? NextPayoutDate)
{
    public long PaidCents { get; init; }
    public long OwedCents { get; init; }
    public DateTime? DueAtUtc { get; init; }
}

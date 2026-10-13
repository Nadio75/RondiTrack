namespace RondiTrack.Models.Dtos;

public sealed record HomeDashboardResponse(
    IReadOnlyList<HomeStokvelResponse> Stokvels);

public sealed record HomeStokvelResponse(
    Guid StokvelId,
    string Name,
    string Role,
    string ContributionAmount,
    HomeCycleResponse? CurrentCycle);

public sealed record HomeCycleResponse(
    Guid CycleId,
    string Period,
    string Status,
    bool HasPaid,
    string AmountOwed,
    DateTime? NextPayoutDate);
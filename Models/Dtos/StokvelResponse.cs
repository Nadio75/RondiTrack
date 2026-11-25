using RondiTrack.Domain;

namespace RondiTrack.Models.Dtos;

public record StokvelResponse(
    Guid Id,
    string Name,
    decimal ContributionAmount,
    int MemberCount)
{
    public string Rules { get; init; } = string.Empty;

    public int ClosingDayOfMonth { get; init; } = 25;

    public static StokvelResponse FromStokvel(Stokvel stokvel) =>
        new(
            stokvel.Id,
            stokvel.Name,
            stokvel.ContributionAmount,
            stokvel.MemberIds.Count)
        {
            Rules = stokvel.Rules,
            ClosingDayOfMonth = stokvel.ClosingDayOfMonth
        };
}

namespace RondiTrack.Models;

public record StokvelResponse(
    Guid Id,
    string Name,
    decimal ContributionAmount,
    int MemberCount)
{
    // Converts a Stokvel entity into a response, showing a member count instead of the raw internal id list.
    public static StokvelResponse FromStokvel(Stokvel stokvel) =>
        new(stokvel.Id, stokvel.Name, stokvel.ContributionAmount, stokvel.MemberIds.Count);
}
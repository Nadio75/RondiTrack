namespace RondiTrack.Mapping;

using RondiTrack.Models;

public static class StokvelMapper
{
    public static StokvelResponse ToResponse(Stokvel stokvel) => StokvelResponse.FromStokvel(stokvel);
}
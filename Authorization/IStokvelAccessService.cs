using System.Security.Claims;

namespace RondiTrack.Authorization;

public enum StokvelAccessResult
{
    Allowed,
    NotFound,
    Forbidden
}

public interface IStokvelAccessService
{
    Task<StokvelAccessResult> CheckAsync(
        ClaimsPrincipal user,
        Guid stokvelId,
        string requiredRole);
}
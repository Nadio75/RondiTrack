using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Infrastructure;

namespace RondiTrack.Authorization;

public sealed class StokvelAccessService : IStokvelAccessService
{
    private readonly IAuthorizationService _authorization;
    private readonly RondiTrackDbContext _db;

    public StokvelAccessService(
        IAuthorizationService authorization,
        RondiTrackDbContext db)
    {
        _authorization = authorization;
        _db = db;
    }

    public async Task<StokvelAccessResult> CheckAsync(
        ClaimsPrincipal user,
        Guid stokvelId,
        string requiredRole)
    {
        var resource = new StokvelAccessResource(stokvelId, requiredRole);

        var policyName = requiredRole == "Member"
            ? "StokvelMember"
            : requiredRole == "TreasurerOrChairperson"
                ? "StokvelTreasurerOrChairperson"
                : "Stokvel" + requiredRole;

        var result = await _authorization.AuthorizeAsync(
            user,
            resource,
            policyName);

        if (result.Succeeded)
            return StokvelAccessResult.Allowed;

        var exists = await _db.Stokvels
            .AsNoTracking()
            .AnyAsync(s => s.Id == stokvelId);

        if (!exists)
            return StokvelAccessResult.NotFound;

        var userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");

        if (!Guid.TryParse(userIdValue, out var userId))
            return StokvelAccessResult.NotFound;

        var isMember = await _db.StokvelMembers
            .AsNoTracking()
            .AnyAsync(m =>
                m.StokvelId == stokvelId &&
                m.UserId == userId);

        return isMember
            ? StokvelAccessResult.Forbidden
            : StokvelAccessResult.NotFound;
    }
}
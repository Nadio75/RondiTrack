using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;
using RondiTrack.Infrastructure;

namespace RondiTrack.Authorization;

public sealed class StokvelRoleHandler
    : AuthorizationHandler<StokvelRoleRequirement, StokvelAccessResource>
{
    private readonly RondiTrackDbContext _db;

    public StokvelRoleHandler(RondiTrackDbContext db)
    {
        _db = db;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        StokvelRoleRequirement requirement,
        StokvelAccessResource resource)
    {
        var userIdValue = context.User.FindFirstValue(
            ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");

        if (!Guid.TryParse(userIdValue, out var userId))
            return;

        var membership = await _db.StokvelMembers
            .AsNoTracking()
            .FirstOrDefaultAsync(m =>
                m.UserId == userId &&
                m.StokvelId == resource.StokvelId);

        if (membership is null)
            return;

        var role = membership.Role.Trim();

        if (requirement.RequiredRole == "Member")
        {
            context.Succeed(requirement);
            return;
        }

        if (requirement.RequiredRole == "TreasurerOrChairperson" &&
            (role.Equals("Treasurer", StringComparison.OrdinalIgnoreCase) ||
             role.Equals("Chairperson", StringComparison.OrdinalIgnoreCase)))
        {
            context.Succeed(requirement);
            return;
        }

        if (role.Equals(
            requirement.RequiredRole,
            StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
        }
    }
}
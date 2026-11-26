using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Infrastructure;

namespace RondiTrack.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/stokvels/{stokvelId:guid}/members")]
public sealed class MembershipRolesController : ControllerBase
{
    private readonly RondiTrackDbContext _db;

    public MembershipRolesController(RondiTrackDbContext db)
    {
        _db = db;
    }

    public sealed record ChangeRoleRequest(string Role);

    [HttpPut("{memberId:guid}/role")]
    [EndpointSummary("Assign a stokvel membership role")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeRole(
        Guid stokvelId,
        Guid memberId,
        ChangeRoleRequest request,
        CancellationToken cancellationToken)
    {
        var callerValue =
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(callerValue, out var callerId))
            return Unauthorized();

        if (request.Role != "Member" &&
            request.Role != "Treasurer")
        {
            return BadRequest(new ProblemDetails
            {
                Status = 400,
                Detail = "Role must be Member or Treasurer."
            });
        }

        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync<IActionResult>(
            async () =>
            {
                _db.ChangeTracker.Clear();

                await using var transaction =
                    await _db.Database.BeginTransactionAsync(
                        cancellationToken);

                await _db.Database.ExecuteSqlRawAsync(
                    """
                    LOCK TABLE "StokvelMembers"
                    IN SHARE ROW EXCLUSIVE MODE
                    """,
                    cancellationToken);

                var caller = await _db.StokvelMembers
                    .SingleOrDefaultAsync(
                        m => m.StokvelId == stokvelId &&
                             m.UserId == callerId,
                        cancellationToken);

                if (caller is null)
                    return NotFound();

                if (caller.Role != "Chairperson")
                    return StatusCode(403);

                var member = await _db.StokvelMembers
                    .SingleOrDefaultAsync(
                        m => m.StokvelId == stokvelId &&
                             m.UserId == memberId,
                        cancellationToken);

                if (member is null)
                    return NotFound();

                if (member.UserId == callerId ||
                    member.Role == "Chairperson")
                {
                    return Conflict(new ProblemDetails
                    {
                        Status = 409,
                        Detail = "The chairperson role is protected."
                    });
                }

                member.ChangeRole(request.Role);

                await _db.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return NoContent();
            });
    }
}

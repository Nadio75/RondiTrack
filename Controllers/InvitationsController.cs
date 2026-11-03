using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Authorization;
using RondiTrack.Common;
using RondiTrack.Domain;
using RondiTrack.Helpers;
using RondiTrack.Infrastructure;

namespace RondiTrack.Controllers;

[ApiController]
[Authorize]
[Route(ApiRoutes.V1)]
public sealed class InvitationsController : ControllerBase
{
    private readonly RondiTrackDbContext _db;
    private readonly IStokvelAccessService _access;
    private readonly TimeProvider _clock;

    public InvitationsController(
        RondiTrackDbContext db,
        IStokvelAccessService access,
        TimeProvider clock)
    {
        _db = db;
        _access = access;
        _clock = clock;
    }

    [HttpPost("stokvels/{stokvelId:guid}/invites")]
    public async Task<IActionResult> Create(
        Guid stokvelId,
        CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(
            User, stokvelId, "TreasurerOrChairperson");

        if (access == StokvelAccessResult.NotFound)
            return NotFound();

        if (access == StokvelAccessResult.Forbidden)
            return Forbid();

        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var code = InvitationCodeHelper.GenerateCode();
        var now = _clock.GetUtcNow().UtcDateTime;
        var expires = now.AddDays(7);

        var invitation = new StokvelInvitation(
            stokvelId,
            userId,
            InvitationCodeHelper.HashCode(code),
            now,
            expires);

        _db.StokvelInvitations.Add(invitation);
        await _db.SaveChangesAsync(cancellationToken);

        return Created(
            $"/api/v1/invites/{code}",
            new
            {
                code,
                expiresAtUtc = expires
            });
    }

    [HttpGet("invites/{code}")]
    public async Task<IActionResult> Preview(
        string code,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 128)
            return NotFound();

        var hash = InvitationCodeHelper.HashCode(code);
        var now = _clock.GetUtcNow().UtcDateTime;

        var invitation = await _db.StokvelInvitations
            .AsNoTracking()
            .Where(i => i.CodeHash == hash && i.ExpiresAtUtc > now)
            .Select(i => new
            {
                i.StokvelId,
                i.Stokvel.Name,
                i.Stokvel.Rules,
                i.Stokvel.ContributionAmount,
                i.Stokvel.ClosingDayOfMonth
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (invitation is null)
            return NotFound();

        var memberCount = await _db.StokvelMembers
            .AsNoTracking()
            .CountAsync(
                m => m.StokvelId == invitation.StokvelId,
                cancellationToken);

        return Ok(new
        {
            stokvelId = invitation.StokvelId,
            name = invitation.Name,
            rules = invitation.Rules,
            contributionAmount = invitation.ContributionAmount,
            closingDayOfMonth = invitation.ClosingDayOfMonth,
            memberCount
        });
    }

    [HttpPost("invites/{code}/join")]
    public async Task<IActionResult> Join(
        string code,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(code) || code.Length > 128)
            return NotFound();

        var hash = InvitationCodeHelper.HashCode(code);
        var now = _clock.GetUtcNow().UtcDateTime;

        var invitation = await _db.StokvelInvitations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                i => i.CodeHash == hash && i.ExpiresAtUtc > now,
                cancellationToken);

        if (invitation is null)
            return NotFound();

        var alreadyMember = await _db.StokvelMembers
            .AnyAsync(
                m => m.StokvelId == invitation.StokvelId
                     && m.UserId == userId,
                cancellationToken);

        if (alreadyMember)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Already a member",
                Detail = "You already belong to this stokvel."
            });
        }

        var membership = new StokvelMember(
            invitation.StokvelId,
            userId,
            "Member");

        _db.StokvelMembers.Add(membership);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is Npgsql.PostgresException pg
                  && pg.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation
                  && pg.ConstraintName == "PK_StokvelMembers")
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Already a member",
                Detail = "You already belong to this stokvel."
            });
        }

        return Ok(new
        {
            stokvelId = invitation.StokvelId,
            message = "You have successfully joined the stokvel."
        });
    }
    private bool TryGetUserId(out Guid userId)
    {
        var value =
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(value, out userId)
            && userId != Guid.Empty;
    }
}

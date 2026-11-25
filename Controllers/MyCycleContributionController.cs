using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Infrastructure;

namespace RondiTrack.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/stokvels/{stokvelId:guid}/cycles/{cycleId:guid}/my-contribution")]
public sealed class MyCycleContributionController : ControllerBase
{
    private readonly RondiTrackDbContext _db;

    public MyCycleContributionController(RondiTrackDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        Guid stokvelId,
        Guid cycleId,
        CancellationToken cancellationToken)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(claim, out var userId))
            return Unauthorized();

        var isMember = await _db.StokvelMembers
            .AsNoTracking()
            .AnyAsync(
                x => x.StokvelId == stokvelId &&
                     x.UserId == userId,
                cancellationToken);

        if (!isMember)
            return NotFound();

        var cycleExists = await _db.ContributionCycles
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == cycleId &&
                     x.StokvelId == stokvelId,
                cancellationToken);

        if (!cycleExists)
            return NotFound();

        var contribution = await _db.Contributions
            .AsNoTracking()
            .Where(x =>
                x.StokvelId == stokvelId &&
                x.ContributionCycleId == cycleId &&
                x.UserId == userId)
            .Select(x => new
            {
                x.Id,
                x.Amount,
                x.CreatedAt,
                x.ConfirmedAtUtc,
                IsConfirmed = x.ConfirmedAtUtc.HasValue
            })
            .SingleOrDefaultAsync(cancellationToken);

        Response.Headers["Cache-Control"] = "no-store";

        return Ok(new { contribution });
    }
}

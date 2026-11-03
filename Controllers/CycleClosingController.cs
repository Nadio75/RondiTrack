using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Authorization;
using RondiTrack.Common;
using RondiTrack.Infrastructure;
using RondiTrack.Models;
using RondiTrack.Services;

namespace RondiTrack.Controllers;

[ApiController]
[Authorize]
[Route(ApiRoutes.V1)]
public sealed class CycleClosingController : ControllerBase
{
    private readonly IStokvelAccessService _access;
    private readonly CycleClosingPreviewService _preview;
    private readonly RondiTrackDbContext _db;
    private readonly TimeProvider _clock;
    private readonly ConfirmedCycleClosingService _closing;

    public CycleClosingController(
        IStokvelAccessService access,
        CycleClosingPreviewService preview,
        RondiTrackDbContext db,
        TimeProvider clock,
        ConfirmedCycleClosingService closing)
    {
        _access = access;
        _preview = preview;
        _db = db;
        _clock = clock;
        _closing = closing;
    }

    [HttpGet("stokvels/{stokvelId:guid}/cycles/{cycleId:guid}/closing-preview")]
    public async Task<IActionResult> Preview(
        Guid stokvelId,
        Guid cycleId,
        CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(
            User, stokvelId, "Treasurer");

        if (access == StokvelAccessResult.NotFound)
            return NotFound();

        if (access == StokvelAccessResult.Forbidden)
            return Forbid();

        var cycle = await _db.ContributionCycles
            .AsNoTracking()
            .Where(c =>
                c.StokvelId == stokvelId &&
                c.Id == cycleId)
            .Select(c => new
            {
                c.IsClosed
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (cycle is null)
            return NotFound();

        if (cycle.IsClosed)
        {
            return Conflict(new
            {
                message = "This cycle is already closed."
            });
        }

        var preview = await _preview.CalculateAsync(
            stokvelId,
            cycleId,
            cancellationToken);

        if (preview is null)
            return NotFound();

        var now = _clock.GetUtcNow().UtcDateTime;

        return Ok(new
        {
            preview.StokvelId,
            preview.CycleId,
            preview.Period,
            preview.ClosingAtUtc,
            preview.Version,
            preview.TargetAmountCents,
            preview.TotalArrearsCents,
            preview.Arrears,preview.SnapshotToken,IsDue = now >= preview.ClosingAtUtc
        });
    }

    [HttpPost("stokvels/{stokvelId:guid}/cycles/{cycleId:guid}/close")]
    public async Task<IActionResult> ConfirmClose(
        Guid stokvelId,
        Guid cycleId,
        [FromBody] ConfirmCycleClosingRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var access = await _access.CheckAsync(
            User, stokvelId, "Treasurer");

        if (access == StokvelAccessResult.NotFound)
            return NotFound();

        if (access == StokvelAccessResult.Forbidden)
            return Forbid();

        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(claim, out var userId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(idempotencyKey) ||
            idempotencyKey.Length > 128)
        {
            return BadRequest(new
            {
                message = "A valid Idempotency-Key header is required."
            });
        }

        if (request is null ||
            string.IsNullOrWhiteSpace(request.SnapshotToken) ||
            request.SnapshotToken.Length != 64)
        {
            return BadRequest(new
            {
                message = "A valid closing snapshot token is required."
            });
        }

        var result = await _closing.CloseAsync(
            stokvelId,
            cycleId,
            userId,
            idempotencyKey,
            request.SnapshotToken,
            cancellationToken);

        return Ok(result);
    }
}




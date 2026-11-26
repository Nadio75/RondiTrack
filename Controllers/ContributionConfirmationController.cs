using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Authorization;
using RondiTrack.Infrastructure;
using RondiTrack.Services.Notifications;

namespace RondiTrack.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/stokvels/{stokvelId:guid}/contributions/{contributionId:guid}/confirm")]
public sealed class ContributionConfirmationController : ControllerBase
{
    private readonly RondiTrackDbContext _db;
    private readonly IStokvelAccessService _access;
    private readonly ContributionNotificationService _notifications;

    public ContributionConfirmationController(
        RondiTrackDbContext db,
        IStokvelAccessService access,
        ContributionNotificationService notifications)
    {
        _db = db;
        _access = access;
        _notifications = notifications;
    }

    [HttpPost]
    [EndpointSummary("Confirm a recorded contribution")]
    [EndpointDescription(
        "Treasurer only. Saves confirmation before sending a privacy-safe notification. Repeated confirmations do not send another notification.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Confirm(
        Guid stokvelId,
        Guid contributionId,
        CancellationToken cancellationToken)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(claim, out var treasurerId) ||
            treasurerId == Guid.Empty)
            return Unauthorized();

        var access = await _access.CheckAsync(
            User, stokvelId, "Treasurer");

        if (access == StokvelAccessResult.NotFound)
            return NotFound();

        if (access != StokvelAccessResult.Allowed)
            return Forbid();

        Guid? memberId = null;
        Guid? cycleId = null;

        var strategy = _db.Database.CreateExecutionStrategy();

        var result = await strategy.ExecuteAsync<IActionResult>(
            async () =>
            {
                memberId = null;
                cycleId = null;

                _db.ChangeTracker.Clear();

                await using var transaction =
                    await _db.Database.BeginTransactionAsync(
                        IsolationLevel.ReadCommitted,
                        cancellationToken);

                await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock(hashtext({stokvelId.ToString()}))",
                    cancellationToken);

                var contribution = await _db.Contributions
                    .SingleOrDefaultAsync(c =>
                        c.Id == contributionId &&
                        c.StokvelId == stokvelId,
                        cancellationToken);

                if (contribution is null)
                    return NotFound();

                if (contribution.IsConfirmed)
                    return NoContent();

                var cycle = await _db.ContributionCycles
                    .AsNoTracking()
                    .SingleOrDefaultAsync(c =>
                        c.Id == contribution.ContributionCycleId &&
                        c.StokvelId == stokvelId,
                        cancellationToken);

                if (cycle is null)
                    return NotFound();

                if (cycle.IsClosed)
                    return Conflict(
                        "This contribution cycle is closed.");

                contribution.Confirm(treasurerId);

                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                memberId = contribution.UserId;
                cycleId = contribution.ContributionCycleId;

                return NoContent();
            });

        // Outside the database transaction and retry delegate.
        // Delivery failure cannot roll back the confirmation.
        if (memberId.HasValue && cycleId.HasValue)
        {
            await _notifications.NotifyConfirmedAsync(
                memberId.Value,
                stokvelId,
                cycleId.Value);
        }

        return result;
    }
}

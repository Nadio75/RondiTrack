using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Common;
using RondiTrack.Infrastructure;

namespace RondiTrack.Controllers;

public sealed class RegisterDeviceRequest
{
    [Required]
    [StringLength(256, MinimumLength = 1)]
    [RegularExpression(@"^[A-Za-z0-9_-]+$")]
    public string InstallationId { get; init; } = string.Empty;
}

[ApiController]
[Authorize]
[Route(ApiRoutes.V1 + "/me/devices")]
public sealed class DevicesController : ControllerBase
{
    private readonly RondiTrackDbContext _db;
    private readonly TimeProvider _clock;

    public DevicesController(
        RondiTrackDbContext db,
        TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    private Guid? CurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(claim, out var id)
            && id != Guid.Empty ? id : null;
    }

    [HttpPost]
    [EndpointSummary("Register the current installation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterDeviceRequest request,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();

        if (userId is null)
            return Unauthorized();

        if (!await _db.Users.AnyAsync(
            u => u.Id == userId.Value, cancellationToken))
            return Unauthorized();

        var now = _clock.GetUtcNow().UtcDateTime;

        // Atomic upsert: one installation belongs to one user.
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "DeviceRegistrations"
                ("InstallationId", "UserId", "UpdatedAtUtc")
            VALUES
                ({request.InstallationId}, {userId.Value}, {now})
            ON CONFLICT ("InstallationId")
            DO UPDATE SET
                "UserId" = EXCLUDED."UserId",
                "UpdatedAtUtc" = EXCLUDED."UpdatedAtUtc"
            """, cancellationToken);

        return NoContent();
    }

    [HttpDelete("{installationId}")]
    [EndpointSummary("Remove the current user's installation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Remove(
        [StringLength(256, MinimumLength = 1)]
        [RegularExpression(@"^[A-Za-z0-9_-]+$")]
        string installationId,
        CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();

        if (userId is null)
            return Unauthorized();

        // A previous owner cannot delete another user's registration.
        await _db.DeviceRegistrations
            .Where(d => d.InstallationId == installationId
                && d.UserId == userId.Value)
            .ExecuteDeleteAsync(cancellationToken);

        return NoContent();
    }
}

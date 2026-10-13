using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Common;
using RondiTrack.Domain;
using RondiTrack.Infrastructure;
using RondiTrack.Models.Dtos;

namespace RondiTrack.Controllers;

[ApiController]
[Authorize]
[Route(ApiRoutes.V1 + "/me")]
public sealed class MeController : ControllerBase
{
    private readonly RondiTrackDbContext _db;
    private readonly IContributionStore _contributions;
    private readonly TimeProvider _timeProvider;

    public MeController(
        RondiTrackDbContext db,
        IContributionStore contributions,
        TimeProvider timeProvider)
    {
        _db = db;
        _contributions = contributions;
        _timeProvider = timeProvider;
    }

    [HttpGet("home")]
    [EndpointSummary("Get the authenticated user's home dashboard")]
    [ProducesResponseType<HomeDashboardResponse>(
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<HomeDashboardResponse>> GetHome(
        CancellationToken cancellationToken)
    {
        var userIdValue =
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Unauthorized();
        }

        // Query only memberships belonging to the caller.
        var memberships = await _db.StokvelMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => new
            {
                m.StokvelId,
                m.Role,
                Name = m.Stokvel.Name,
                m.Stokvel.ContributionAmount
            })
            .OrderBy(m => m.Name)
            .ThenBy(m => m.StokvelId)
            .ToListAsync(cancellationToken);

        if (memberships.Count == 0)
        {
            return Ok(new HomeDashboardResponse(
                Array.Empty<HomeStokvelResponse>()));
        }

        var stokvelIds = memberships
            .Select(m => m.StokvelId)
            .ToArray();

        // Fetch open cycles for only these stokvels.
        var cycles = await _db.ContributionCycles
            .AsNoTracking()
            .Where(c =>
                stokvelIds.Contains(c.StokvelId) &&
                c.Status == "Open")
            .Select(c => new
            {
                c.Id,
                c.StokvelId,
                c.Period,
                c.Status,
                c.CreatedAt
            })
            .ToListAsync(cancellationToken);

        // Select the most recent open cycle per stokvel.
        var currentCycles = cycles
            .GroupBy(c => c.StokvelId)
            .ToDictionary(
                g => g.Key,
                g => g
                    .OrderByDescending(c => c.Period)
                    .ThenByDescending(c => c.CreatedAt)
                    .ThenByDescending(c => c.Id)
                    .First());

        // Contributions currently live in the in-memory store.
        // Read once, then keep only the caller's contributions.
        var allContributions =
            await _contributions.GetAllAsync();

        var callerContributions = allContributions
            .Where(c =>
                c.UserId == userId &&
                stokvelIds.Contains(c.StokvelId))
            .ToList();

        var results = new List<HomeStokvelResponse>();

        foreach (var membership in memberships)
        {
            HomeCycleResponse? currentCycle = null;

            if (currentCycles.TryGetValue(
                membership.StokvelId,
                out var cycle))
            {
                var paidAmount = callerContributions
                    .Where(c =>
                        c.StokvelId == membership.StokvelId &&
                        c.ContributionCycleId == cycle.Id)
                    .Sum(c => c.Amount);

                var hasPaid = paidAmount > 0;

                var amountOwed = Math.Max(
                    0m,
                    membership.ContributionAmount - paidAmount);

                currentCycle = new HomeCycleResponse(
                    cycle.Id,
                    cycle.Period,
                    cycle.Status,
                    hasPaid,
                    amountOwed.ToString(
                        "0.00",
                        CultureInfo.InvariantCulture),
                    null);
            }

            results.Add(new HomeStokvelResponse(
                membership.StokvelId,
                membership.Name,
                membership.Role,
                membership.ContributionAmount.ToString(
                    "0.00",
                    CultureInfo.InvariantCulture),
                currentCycle));
        }

        return Ok(new HomeDashboardResponse(results));
    }
}

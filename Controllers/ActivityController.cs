using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Common;
using RondiTrack.Helpers;
using RondiTrack.Infrastructure;
using RondiTrack.Models.Dtos;

namespace RondiTrack.Controllers;

[ApiController]
[Authorize]
[Route(ApiRoutes.V1 + "/activity")]
public sealed class ActivityController : ControllerBase
{
    private readonly RondiTrackDbContext _db;
    private readonly PageTokenProtector _tokenProtector;

    public ActivityController(
        RondiTrackDbContext db,
        PageTokenProtector tokenProtector)
    {
        _db = db;
        _tokenProtector = tokenProtector;
    }

    [HttpGet]
    public async Task<IActionResult> GetActivity(
        [FromQuery] int pageSize = 20,
        [FromQuery] string? pageToken = null,
        CancellationToken cancellationToken = default)
    {
        var userIdValue =
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(userIdValue, out var userId) ||
            userId == Guid.Empty)
        {
            return Unauthorized();
        }

        if (pageSize < 1)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid pageSize",
                Detail = "pageSize must be greater than zero.",
                Status = 400
            });
        }

        pageSize = Math.Min(pageSize, 100);

        DateTime? afterDate = null;
        string? afterType = null;
        Guid? afterId = null;

        if (!string.IsNullOrWhiteSpace(pageToken))
        {
            if (!_tokenProtector.TryUnprotectActivity(
                    pageToken,
                    out var originalToken) ||
                !ActivityPageToken.TryDecode(
                originalToken,
                userId,
                out var decodedDate,
                out var decodedType,
                out var decodedId))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid pageToken",
                    Detail = "The activity bookmark is invalid.",
                    Status = 400
                });
            }

            afterDate = decodedDate;
            afterType = decodedType;
            afterId = decodedId;
        }

        // Only the caller's current stokvel memberships are considered.
        var myStokvelIds = _db.StokvelMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => m.StokvelId);

        var contributions =
            from contribution in _db.Contributions.AsNoTracking()
            join stokvel in _db.Stokvels.AsNoTracking()
                on contribution.StokvelId equals stokvel.Id
            join member in _db.Users.AsNoTracking()
                on contribution.UserId equals member.Id
            where contribution.UserId == userId
                && myStokvelIds.Contains(contribution.StokvelId)
            select new
            {
                contribution.Id,
                contribution.StokvelId,
                StokvelName = stokvel.Name,
                ActivityType = "contribution",
                OccurredAt = contribution.CreatedAt,
                MemberId = (Guid?)contribution.UserId,
                MemberName = member.Name,
                Amount = (decimal?)contribution.Amount
            };

        var payouts =
            from payout in _db.Payouts.AsNoTracking()
            join stokvel in _db.Stokvels.AsNoTracking()
                on payout.StokvelId equals stokvel.Id
            join member in _db.Users.AsNoTracking()
                on payout.RecipientUserId equals member.Id
            where payout.RecipientUserId == userId
                && myStokvelIds.Contains(payout.StokvelId)
            select new
            {
                payout.Id,
                payout.StokvelId,
                StokvelName = stokvel.Name,
                ActivityType = "payout",
                OccurredAt = payout.ProcessedAt,
                MemberId = (Guid?)payout.RecipientUserId,
                MemberName = member.Name,
                Amount = (decimal?)payout.Amount
            };

        // UnionAll/Concat keeps both types of financial activity.
        var query = contributions.Concat(payouts);

        // The same ordering must be used for both the list and bookmark.
        if (afterDate.HasValue &&
            afterType is not null &&
            afterId.HasValue)
        {
            query = query.Where(x =>
                x.OccurredAt < afterDate.Value ||
                (x.OccurredAt == afterDate.Value &&
                    string.Compare(x.ActivityType, afterType) < 0) ||
                (x.OccurredAt == afterDate.Value &&
                    x.ActivityType == afterType &&
                    x.Id.CompareTo(afterId.Value) < 0));
        }

        var rows = await query
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.ActivityType)
            .ThenByDescending(x => x.Id)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > pageSize;

        var items = rows
            .Take(pageSize)
            .Select(x => new ActivityFeedItem(
                x.Id,
                x.StokvelId,
                x.StokvelName,
                x.ActivityType,
                x.OccurredAt,
                x.MemberId,
                x.MemberName,
                x.Amount))
            .ToList();

        string? nextPageToken = null;

        if (hasMore)
        {
            var last = items[^1];

            nextPageToken = _tokenProtector.ProtectActivity(
                ActivityPageToken.Encode(
                userId,
                last.OccurredAt,
                last.ActivityType,
                last.Id));
        }

        return Ok(new PagedResponse<ActivityFeedItem>(
            items,
            nextPageToken));
    }
}

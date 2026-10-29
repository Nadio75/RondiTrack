using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Authorization;
using RondiTrack.Common;
using RondiTrack.Helpers;
using RondiTrack.Infrastructure;
using RondiTrack.Models.Dtos;

namespace RondiTrack.Controllers;

[ApiController]
[Authorize]
[Route(ApiRoutes.V1 + "/stokvels/{id:guid}/contributions")]
public sealed class StokvelContributionHistoryController : ControllerBase
{
    private readonly RondiTrackDbContext _db;
    private readonly IStokvelAccessService _access;
    private readonly PageTokenProtector _tokenProtector;

    public StokvelContributionHistoryController(
        RondiTrackDbContext db,
        IStokvelAccessService access,
        PageTokenProtector tokenProtector)
    {
        _db = db;
        _access = access;
        _tokenProtector = tokenProtector;
    }

    [HttpGet]
    public async Task<IActionResult> GetHistory(
        Guid id,
        [FromQuery] string? member = null,
        [FromQuery(Name = "from")] string? from = null,
        [FromQuery(Name = "to")] string? to = null,
        [FromQuery] string sort = "newest",
        [FromQuery] int pageSize = 20,
        [FromQuery] string? pageToken = null,
        CancellationToken cancellationToken = default)
    {
        var access = await _access.CheckAsync(User, id, "Member");

        if (access == StokvelAccessResult.NotFound)
            return NotFound();

        if (access != StokvelAccessResult.Allowed)
            return Forbid();

        if (sort != "newest" && sort != "oldest")
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid sort",
                Detail = "sort must be newest or oldest.",
                Status = 400
            });

        if (pageSize < 1)
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid pageSize",
                Detail = "pageSize must be greater than zero.",
                Status = 400
            });

        pageSize = Math.Min(pageSize, 100);

        Guid? memberId = null;

        if (!string.IsNullOrWhiteSpace(member))
        {
            if (!Guid.TryParse(member, out var parsedMember) ||
                parsedMember == Guid.Empty)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid member",
                    Detail = "member must be a valid user ID.",
                    Status = 400
                });
            }

            memberId = parsedMember;

            var belongsToStokvel = await _db.StokvelMembers
                .AsNoTracking()
                .AnyAsync(m =>
                    m.StokvelId == id &&
                    m.UserId == memberId.Value,
                    cancellationToken);

            if (!belongsToStokvel)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid member",
                    Detail = "The selected member is not in this stokvel.",
                    Status = 400
                });
            }
        }

        if (!TryParseUtcInstant(from, out var fromUtc) ||
            !TryParseUtcInstant(to, out var toUtc))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid date range",
                Detail = "from and to must be UTC ISO 8601 instants.",
                Status = 400
            });
        }

        if (fromUtc.HasValue &&
            toUtc.HasValue &&
            toUtc.Value <= fromUtc.Value)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid date range",
                Detail = "to must be later than from.",
                Status = 400
            });
        }

        var filterHash =
            ContributionHistoryPageToken.CreateFilterHash(
                id,
                memberId,
                fromUtc,
                toUtc,
                sort);

        DateTime? afterCreatedAt = null;
        Guid? afterId = null;

        if (!string.IsNullOrWhiteSpace(pageToken))
        {
            if (!_tokenProtector.TryUnprotectHistory(
                    pageToken,
                    out var originalToken) ||
                !ContributionHistoryPageToken.TryDecode(
                originalToken,
                filterHash,
                out var decodedDate,
                out var decodedId))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid pageToken",
                    Detail = "The page token is malformed or belongs to different filters.",
                    Status = 400
                });
            }

            afterCreatedAt = decodedDate;
            afterId = decodedId;
        }

        var query =
            from contribution in _db.Contributions.AsNoTracking()
            join user in _db.Users.AsNoTracking()
                on contribution.UserId equals user.Id
            where contribution.StokvelId == id
            select new
            {
                contribution.Id,
                contribution.StokvelId,
                contribution.ContributionCycleId,
                MemberId = contribution.UserId,
                MemberName = user.Name,
                contribution.Amount,
                contribution.CreatedAt
            };

        if (memberId.HasValue)
            query = query.Where(x => x.MemberId == memberId.Value);

        if (fromUtc.HasValue)
            query = query.Where(x => x.CreatedAt >= fromUtc.Value);

        if (toUtc.HasValue)
            query = query.Where(x => x.CreatedAt < toUtc.Value);

        if (afterCreatedAt.HasValue && afterId.HasValue)
        {
            if (sort == "oldest")
            {
                query = query.Where(x =>
                    x.CreatedAt > afterCreatedAt.Value ||
                    (x.CreatedAt == afterCreatedAt.Value &&
                     x.Id.CompareTo(afterId.Value) > 0));
            }
            else
            {
                query = query.Where(x =>
                    x.CreatedAt < afterCreatedAt.Value ||
                    (x.CreatedAt == afterCreatedAt.Value &&
                     x.Id.CompareTo(afterId.Value) < 0));
            }
        }

        var orderedQuery = sort == "oldest"
            ? query.OrderBy(x => x.CreatedAt)
                   .ThenBy(x => x.Id)
            : query.OrderByDescending(x => x.CreatedAt)
                   .ThenByDescending(x => x.Id);

        var rows = await orderedQuery
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > pageSize;
        var page = rows.Take(pageSize).ToList();

        string? nextPageToken = null;

        if (hasMore)
        {
            var last = page[^1];

            nextPageToken = _tokenProtector.ProtectHistory(
                ContributionHistoryPageToken.Encode(
                last.CreatedAt,
                last.Id,
                filterHash));
        }

        var items = page.Select(x =>
            new StokvelContributionHistoryItem(
                x.Id,
                x.StokvelId,
                x.ContributionCycleId,
                x.MemberId,
                x.MemberName,
                x.Amount,
                x.CreatedAt)).ToList();

        return Ok(new PagedResponse<StokvelContributionHistoryItem>(
            items,
            nextPageToken));
    }

    private static bool TryParseUtcInstant(
        string? value,
        out DateTime? instant)
    {
        instant = null;

        if (string.IsNullOrWhiteSpace(value))
            return true;

        if (!value.EndsWith("Z", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed))
        {
            return false;
        }

        instant = parsed.UtcDateTime;
        return true;
    }
}


using System.Data;
using System.Diagnostics;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RondiTrack.Domain;
using RondiTrack.Infrastructure;

namespace RondiTrack.Services;

/// <summary>
/// Closes overdue contribution cycles, records arrears,
/// and creates subsequent monthly cycles.
/// </summary>
public sealed class CycleClosingService
{
    public const string ActivitySourceName = "RondiTrack.CycleClosing";

    private static readonly ActivitySource ActivitySource =
        new(ActivitySourceName);

    private readonly RondiTrackDbContext _db;
    private readonly TimeProvider _clock;
    private readonly ILogger<CycleClosingService> _logger;

    public CycleClosingService(
        RondiTrackDbContext db,
        TimeProvider clock,
        ILogger<CycleClosingService> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task<int> CloseDueCyclesAsync(
        Guid stokvelId,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity(
            "cycle.close_due",
            ActivityKind.Internal);

        activity?.SetTag("stokvel.id", stokvelId.ToString());

        DateTime utcNow = _clock.GetUtcNow().UtcDateTime;

        var strategy = _db.Database.CreateExecutionStrategy();

        try
        {
            int closedCount = await strategy.ExecuteAsync(async () =>
            {
                await using var transaction =
                    await _db.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable,
                        cancellationToken);

                // Prevent two scheduler instances from processing
                // the same stokvel simultaneously.
                await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock(hashtext({stokvelId.ToString()}))",
                    cancellationToken);

                var stokvel = await _db.Stokvels
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        s => s.Id == stokvelId,
                        cancellationToken);

                if (stokvel is null)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return 0;
                }

                // Load existing open cycles.
                var cycles = await _db.ContributionCycles
                    .Where(c =>
                        c.StokvelId == stokvelId &&
                        !c.IsClosed)
                    .OrderBy(c => c.Period)
                    .ToListAsync(cancellationToken);

                var pending = new Queue<ContributionCycle>(cycles);

                // Track all known periods so we never create
                // the same month twice.
                var knownPeriods = (await _db.ContributionCycles
                    .Where(c => c.StokvelId == stokvelId)
                    .Select(c => c.Period)
                    .ToListAsync(cancellationToken))
                    .ToHashSet(StringComparer.Ordinal);

                int completed = 0;

                while (pending.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var cycle = pending.Dequeue();

                    if (cycle.IsClosed)
                        continue;

                    if (!DateTime.TryParseExact(
                            cycle.Period,
                            "yyyy-MM",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None,
                            out DateTime period))
                    {
                        _logger.LogWarning(
                            "Skipping cycle {CycleId} with invalid period {Period}",
                            cycle.Id,
                            cycle.Period);

                        continue;
                    }

                    DateTime closingInstantUtc =
                        CycleClosingSchedule.GetClosingInstantUtc(
                            period.Year,
                            period.Month,
                            stokvel.ClosingDayOfMonth);

                    // A cycle must never close before its deadline.
                    if (utcNow < closingInstantUtc)
                        continue;

                    var memberIds = await _db.StokvelMembers
                        .Where(m =>
                            m.StokvelId == stokvelId &&
                            m.JoinedAtUtc < closingInstantUtc)
                        .Select(m => m.UserId)
                        .ToListAsync(cancellationToken);

                    long expectedCents = checked(
                        (long)decimal.Round(
                            cycle.TargetAmount * 100m,
                            0,
                            MidpointRounding.AwayFromZero));

                    var payments = await _db.Contributions
                        .Where(c =>
                            c.StokvelId == stokvelId &&
                            c.ContributionCycleId == cycle.Id)
                        .GroupBy(c => c.UserId)
                        .Select(g => new
                        {
                            UserId = g.Key,
                            TotalPaid = g.Sum(c => c.Amount)
                        })
                        .ToListAsync(cancellationToken);

                    var paidByMember = payments.ToDictionary(
                        p => p.UserId,
                        p => checked(
                            (long)decimal.Round(
                                p.TotalPaid * 100m,
                                0,
                                MidpointRounding.AwayFromZero)));

                    foreach (Guid memberId in memberIds)
                    {
                        paidByMember.TryGetValue(
                            memberId,
                            out long paidCents);

                        long arrearsCents = Math.Max(
                            0,
                            expectedCents - paidCents);

                        if (arrearsCents == 0)
                            continue;

                        _db.ContributionArrears.Add(
                            new ContributionArrear(
                                stokvelId,
                                cycle.Id,
                                memberId,
                                arrearsCents,
                                utcNow));
                    }

                    cycle.Close(utcNow);

                    DateTime nextMonth = new DateTime(
                        period.Year,
                        period.Month,
                        1).AddMonths(1);

                    string nextPeriod = nextMonth.ToString(
                        "yyyy-MM",
                        CultureInfo.InvariantCulture);

                    // HashSet.Add returns false when the period
                    // already exists, preventing duplicate creation.
                    if (knownPeriods.Add(nextPeriod))
                    {
                        var nextCycle = new ContributionCycle(
                            stokvelId,
                            nextPeriod,
                            cycle.TargetAmount);

                        _db.ContributionCycles.Add(nextCycle);

                        // The crucial fix: process this new cycle
                        // during the same run if it is also overdue.
                        pending.Enqueue(nextCycle);
                    }

                    completed++;
                }

                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return completed;
            });

            activity?.SetTag("cycles.closed", closedCount);
            activity?.SetStatus(ActivityStatusCode.Ok);

            return closedCount;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown is not a financial processing failure.
            throw;
        }
        catch (Exception exception)
        {
            activity?.SetStatus(
                ActivityStatusCode.Error,
                exception.Message);

            _logger.LogError(
                exception,
                "Failed to close cycles for stokvel {StokvelId}",
                stokvelId);

            throw;
        }
    }
}

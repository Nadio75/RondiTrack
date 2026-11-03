using System.Data;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;
using RondiTrack.Infrastructure;

namespace RondiTrack.Services;

public sealed record ConfirmedCycleClosingResult(
    Guid StokvelId,
    Guid CycleId,
    string Period,
    long TotalArrearsCents,
    DateTime ClosedAtUtc,
    bool Replayed);

public sealed class ConfirmedCycleClosingService
{
    private readonly RondiTrackDbContext _db;
    private readonly CycleClosingPreviewService _preview;
    private readonly TimeProvider _clock;

    public ConfirmedCycleClosingService(
        RondiTrackDbContext db,
        CycleClosingPreviewService preview,
        TimeProvider clock)
    {
        _db = db;
        _preview = preview;
        _clock = clock;
    }

    public async Task<ConfirmedCycleClosingResult> CloseAsync(
        Guid stokvelId,
        Guid cycleId,
        Guid requestedByUserId,
        string idempotencyKey,
        string snapshotToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) ||
            idempotencyKey.Length > 128)
            throw new ArgumentException(
                "A valid idempotency key is required.");

        if (string.IsNullOrWhiteSpace(snapshotToken) ||
            snapshotToken.Length != 64)
            throw new ArgumentException(
                "A valid snapshot token is required.");

        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtext({stokvelId.ToString()}))",
                cancellationToken);

            // A successful retry returns the original result.
            var previous = await _db.CycleClosingRequests
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    r => r.StokvelId == stokvelId &&
                         r.RequestedByUserId == requestedByUserId &&
                         r.IdempotencyKey == idempotencyKey,
                    cancellationToken);

            if (previous is not null)
            {
                if (previous.CycleId != cycleId ||
                    previous.SnapshotToken != snapshotToken)
                    throw new ConflictException(
                        "This idempotency key was already used for another confirmation.");

                var previousCycle = await _db.ContributionCycles
                    .AsNoTracking()
                    .SingleAsync(c => c.Id == cycleId, cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return new ConfirmedCycleClosingResult(
                    stokvelId,
                    cycleId,
                    previousCycle.Period,
                    previous.TotalArrearsCents,
                    previous.CompletedAtUtc,
                    true);
            }

            var cycle = await _db.ContributionCycles
                .SingleOrDefaultAsync(
                    c => c.Id == cycleId &&
                         c.StokvelId == stokvelId,
                    cancellationToken);

            if (cycle is null)
                throw new NotFoundException(
                    "Contribution cycle not found.");

            if (cycle.IsClosed)
                throw new ConflictException(
                    "This cycle is already closed. Refresh the preview.");

            var fresh = await _preview.CalculateAsync(
                stokvelId,
                cycleId,
                cancellationToken);

            if (fresh is null ||
                !string.Equals(
                    fresh.SnapshotToken,
                    snapshotToken,
                    StringComparison.Ordinal))
                throw new ConflictException(
                    "The closing preview has changed. Refresh and confirm again.");

            var now = _clock.GetUtcNow().UtcDateTime;

            if (now < fresh.ClosingAtUtc)
                throw new ConflictException(
                    "This cycle has not reached its closing deadline.");

            foreach (var member in fresh.Arrears)
            {
                _db.ContributionArrears.Add(
                    new ContributionArrear(
                        stokvelId,
                        cycleId,
                        member.UserId,
                        member.ArrearsCents,
                        now));
            }

            cycle.Close(now);

            var period = DateTime.ParseExact(
                cycle.Period,
                "yyyy-MM",
                System.Globalization.CultureInfo.InvariantCulture);

            var nextPeriod = period.AddMonths(1).ToString(
                "yyyy-MM",
                System.Globalization.CultureInfo.InvariantCulture);

            var nextExists = await _db.ContributionCycles
                .AnyAsync(
                    c => c.StokvelId == stokvelId &&
                         c.Period == nextPeriod,
                    cancellationToken);

            if (!nextExists)
            {
                _db.ContributionCycles.Add(
                    new ContributionCycle(
                        stokvelId,
                        nextPeriod,
                        cycle.TargetAmount));
            }

            _db.CycleClosingRequests.Add(
                new CycleClosingRequest(
                    stokvelId,
                    cycleId,
                    requestedByUserId,
                    idempotencyKey,
                    snapshotToken,
                    now,
                    fresh.TotalArrearsCents));

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new ConfirmedCycleClosingResult(
                stokvelId,
                cycleId,
                cycle.Period,
                fresh.TotalArrearsCents,
                now,
                false);
        });
    }
}

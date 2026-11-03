using System.Data;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;
using RondiTrack.Infrastructure;

namespace RondiTrack.Services;

public sealed class ContributionTransactionService
{
    private readonly RondiTrackDbContext _db;

    public ContributionTransactionService(RondiTrackDbContext db)
    {
        _db = db;
    }

    public async Task<Contribution> RecordAsync(
        Guid stokvelId,
        Guid userId,
        decimal amount,
        Guid cycleId,
        CancellationToken cancellationToken = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            // Use the same lock as CycleClosingService.
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtext({stokvelId.ToString()}))",
                cancellationToken);

            var cycle = await _db.ContributionCycles
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    c => c.Id == cycleId &&
                         c.StokvelId == stokvelId,
                    cancellationToken);

            if (cycle is null)
                throw new NotFoundException(
                    "Contribution cycle not found.");

            if (cycle.IsClosed)
                throw new ConflictException(
                    "This contribution cycle is closed.");

            var existing = await _db.Contributions
                .AsNoTracking()
                .AnyAsync(
                    c => c.StokvelId == stokvelId &&
                         c.UserId == userId &&
                         c.ContributionCycleId == cycleId,
                    cancellationToken);

            if (existing)
                throw new ConflictException(
                    "This member has already paid for this cycle.");

            var contribution = new Contribution(
                stokvelId,
                userId,
                amount,
                cycleId);

            _db.Contributions.Add(contribution);

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return contribution;
        });
    }
}

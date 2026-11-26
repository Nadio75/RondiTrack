using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RondiTrack.Domain;
using RondiTrack.Infrastructure;
using RondiTrack.Mapping;
using RondiTrack.Models;
using RondiTrack.Models.Dtos;

namespace RondiTrack.Services;

public sealed class ContributionTransactionService
{
    private readonly RondiTrackDbContext _db;

    public ContributionTransactionService(RondiTrackDbContext db)
    {
        _db = db;
    }

    public async Task<(ContributionResponse Response, bool IsReplay)>
        RecordIdempotentAsync(
            Guid stokvelId,
            string idempotencyKey,
            ContributionRequest request,
            CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) ||
            idempotencyKey.Length > 128)
        {
            throw new ArgumentException(
                "Idempotency-Key must contain between 1 and 128 characters.");
        }

        // Include the target resource in the request fingerprint.
        var fingerprint = JsonSerializer.Serialize(new
        {
            StokvelId = stokvelId,
            Request = request
        });

        var requestHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(fingerprint)));

        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction =
                await _db.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken);

            // Serialize requests using the same key, including
            // requests targeting different stokvels.
            await AcquireLockAsync(
                idempotencyKey,
                cancellationToken);

            var previous = await _db.IdempotencyRecords
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    r => r.Key == idempotencyKey,
                    cancellationToken);

            if (previous is not null)
            {
                if (previous.RequestHash != requestHash)
                {
                    throw new IdempotencyConflictException(
                        "This Idempotency-Key was already used with a different request.");
                }

                var replay = JsonSerializer
                    .Deserialize<ContributionResponse>(
                        previous.ResponseBodyJson)!;

                await transaction.CommitAsync(cancellationToken);

                return (replay, true);
            }

            // Coordinate with the existing cycle-closing lock.
            await AcquireLockAsync(
                stokvelId.ToString(),
                cancellationToken);

            var stokvel = await _db.Stokvels
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    s => s.Id == stokvelId,
                    cancellationToken);

            if (stokvel is null)
                throw new NotFoundException("Stokvel not found.");

            var userExists = await _db.Users
                .AsNoTracking()
                .AnyAsync(u => u.Id == request.UserId, cancellationToken);

            if (!userExists)
                throw new NotFoundException("User not found.");

            var isMember = await _db.StokvelMembers
                .AsNoTracking()
                .AnyAsync(
                    m => m.StokvelId == stokvelId &&
                         m.UserId == request.UserId,
                    cancellationToken);

            if (!isMember)
                throw new NotFoundException(
                    "This user is not a member of this stokvel.");

            var cycle = await _db.ContributionCycles
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    c => c.Id == request.ContributionCycleId &&
                         c.StokvelId == stokvelId,
                    cancellationToken);

            if (cycle is null)
            {
                throw new NotFoundException(
                    "Contribution cycle not found.");
            }

            if (cycle.IsClosed)
            {
                throw new ConflictException(
                    "This contribution cycle is closed.");
            }

            var alreadyPaid = await _db.Contributions
                .AsNoTracking()
                .AnyAsync(
                    c => c.StokvelId == stokvelId &&
                         c.UserId == request.UserId &&
                         c.ContributionCycleId ==
                            request.ContributionCycleId,
                    cancellationToken);

            if (alreadyPaid)
            {
                throw new ConflictException(
                    "This member has already paid for this cycle.");
            }

            if (request.Amount != stokvel.ContributionAmount)
            {
                throw new BusinessRuleViolationException(
                    "The contribution must equal the stokvel's required monthly contribution.");
            }

            var contribution = new Contribution(
                stokvelId,
                request.UserId,
                request.Amount,
                request.ContributionCycleId);

            _db.Contributions.Add(contribution);

            var response = ContributionMapper.ToResponse(
                contribution);

            _db.IdempotencyRecords.Add(new IdempotencyRecord
            {
                Key = idempotencyKey,
                RequestHash = requestHash,
                ResponseStatusCode = 201,
                ResponseBodyJson = JsonSerializer.Serialize(response)
            });

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return (response, false);
        });
    }

    private async Task AcquireLockAsync(
        string value,
        CancellationToken cancellationToken)
    {
        var connection = _db.Database.GetDbConnection();

        await using var command = connection.CreateCommand();

        command.CommandText =
            "SELECT pg_advisory_xact_lock(hashtext(@lock_value))";

        command.Transaction =
            _db.Database.CurrentTransaction!.GetDbTransaction();

        var parameter = command.CreateParameter();
        parameter.ParameterName = "lock_value";
        parameter.Value = value;
        command.Parameters.Add(parameter);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
    // Retained for existing callers while they migrate
    // to the idempotent transaction path.
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

            await AcquireLockAsync(
                stokvelId.ToString(),
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

            var stokvel = await _db.Stokvels
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    s => s.Id == stokvelId,
                    cancellationToken);

            if (stokvel is null)
                throw new NotFoundException("Stokvel not found.");

            if (amount != stokvel.ContributionAmount)
            {
                throw new BusinessRuleViolationException(
                    "The contribution must equal the stokvel's required monthly contribution.");
            }

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

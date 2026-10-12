using RondiTrack.Telemetry;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;
using RondiTrack.Infrastructure;

namespace RondiTrack.Services;

public class PayoutService
{
    // The name must match the ActivitySource registered
    // in Program.cs.
    public const string ActivitySourceName = "RondiTrack.Payouts";

    private static readonly ActivitySource PayoutActivitySource =
        new(ActivitySourceName);

    private readonly RondiTrackDbContext _db;

    public PayoutService(RondiTrackDbContext db)
    {
        _db = db;
    }

    public async Task<Payout> ProcessAsync(
        Guid stokvelId,
        Guid contributionCycleId)
    {
        // Start a custom span around the entire payout operation.
        // SQL spans created during this operation can become
        // children of this span.
        using var activity = PayoutActivitySource.StartActivity(
            "payout.process",
            ActivityKind.Internal);

        activity?.SetTag("payout.operation", "process");

        try
        {
            var stokvel = await _db.Stokvels.FindAsync(stokvelId)
                ?? throw new NotFoundException(
                    "Stokvel not found.");

            var cycle = await _db.ContributionCycles.FindAsync(
                contributionCycleId)
                ?? throw new NotFoundException(
                    "Contribution cycle not found.");

            if (cycle.StokvelId != stokvelId)
            {
                throw new NotFoundException(
                    "This contribution cycle does not exist for this stokvel.");
            }

            if (cycle.Status == "PaidOut")
            {
                throw new ConflictException(
                    "This cycle has already been paid out.");
            }

            // Preserve the existing EF Core retry strategy.
            // The transaction must remain inside ExecuteAsync.
            var strategy = _db.Database.CreateExecutionStrategy();

            var payout = await strategy.ExecuteAsync(async () =>
            {
                await using var transaction =
                    await _db.Database.BeginTransactionAsync();

                var alreadyPaidUserIds = await _db.Payouts
                    .Where(p => p.StokvelId == stokvelId)
                    .Select(p => p.RecipientUserId)
                    .ToListAsync();

                var nextRecipientId = await _db.StokvelMembers
                    .Where(m =>
                        m.StokvelId == stokvelId &&
                        !alreadyPaidUserIds.Contains(m.UserId))
                    .OrderBy(m => m.JoinedAtUtc)
                    .Select(m => m.UserId)
                    .FirstOrDefaultAsync();

                if (nextRecipientId == Guid.Empty)
                {
                    throw new BusinessRuleViolationException(
                        "Every member of this stokvel has already received a payout.");
                }

                var newPayout = new Payout(
                    stokvelId,
                    contributionCycleId,
                    nextRecipientId,
                    stokvel.ContributionAmount);

                _db.Payouts.Add(newPayout);

                cycle.MarkPaidOut();

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                return newPayout;
            });

           activity?.SetTag("payout.outcome", "success");
activity?.SetStatus(ActivityStatusCode.Ok);

RondiTrackMetrics.PayoutsProcessed.Add(
    1,
    new KeyValuePair<string, object?>(
        "outcome",
        "success"));

return payout;
        }
       catch (Exception)
{
    activity?.SetTag("payout.outcome", "failure");
    activity?.SetStatus(ActivityStatusCode.Error);

    RondiTrackMetrics.PayoutsProcessed.Add(
        1,
        new KeyValuePair<string, object?>(
            "outcome",
            "failure"));

    throw;
}
    }

    public async Task<Payout> UpdateAmountAsync(
        Guid stokvelId,
        Guid contributionCycleId,
        decimal amount,
        uint version)
    {
        if (amount <= 0)
        {
            throw new BusinessRuleViolationException(
                "Payout amount must be greater than zero.");
        }

        var payout = await _db.Payouts
            .FirstOrDefaultAsync(p =>
                p.StokvelId == stokvelId &&
                p.ContributionCycleId == contributionCycleId);

        if (payout is null)
        {
            throw new NotFoundException("Payout not found.");
        }

        // Preserve optimistic concurrency protection.
        _db.Entry(payout)
            .Property(p => p.xmin)
            .OriginalValue = version;

        payout.UpdateAmount(amount);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "The payout was modified by another request. Refresh it and retry.");
        }

        return payout;
    }
}

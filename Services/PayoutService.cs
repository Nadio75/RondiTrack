using RondiTrack.Infrastructure;
using RondiTrack.Models.Dtos;
using RondiTrack.Models;
namespace RondiTrack.Services;

using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;

public class PayoutService
{
    private readonly RondiTrackDbContext _db;

    public PayoutService(RondiTrackDbContext db) => _db = db;

    public async Task<Payout> ProcessAsync(Guid stokvelId, Guid contributionCycleId)
    {
        var stokvel = await _db.Stokvels.FindAsync(stokvelId)
            ?? throw new NotFoundException("Stokvel not found.");
        var cycle = await _db.ContributionCycles.FindAsync(contributionCycleId)
            ?? throw new NotFoundException("Contribution cycle not found.");
        if (cycle.StokvelId != stokvelId)
            throw new NotFoundException("This contribution cycle does not exist for this stokvel.");
        if (cycle.Status == "PaidOut")
            throw new ConflictException("This cycle has already been paid out.");

        // EnableRetryOnFailure means an explicit transaction must run inside an
        // execution strategy, or a retried attempt could try to open a second
        // transaction on top of one already in progress.
        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();

            var alreadyPaidUserIds = await _db.Payouts
                .Where(p => p.StokvelId == stokvelId)
                .Select(p => p.RecipientUserId)
                .ToListAsync();

            var nextRecipientId = await _db.StokvelMembers
                .Where(m => m.StokvelId == stokvelId && !alreadyPaidUserIds.Contains(m.UserId))
                .OrderBy(m => m.JoinedAtUtc)
                .Select(m => m.UserId)
                .FirstOrDefaultAsync();

            if (nextRecipientId == Guid.Empty)
                throw new BusinessRuleViolationException("Every member of this stokvel has already received a payout.");

            var payout = new Payout(stokvelId, contributionCycleId, nextRecipientId, stokvel.ContributionAmount);
            _db.Payouts.Add(payout);
            cycle.MarkPaidOut();

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return payout;
        });
    }
    public async Task<Payout> UpdateAmountAsync(
    Guid stokvelId,
    Guid contributionCycleId,
    decimal amount,
    uint version)
{
    if (amount <= 0)
        throw new BusinessRuleViolationException(
            "Payout amount must be greater than zero.");

    var payout = await _db.Payouts
        .FirstOrDefaultAsync(p =>
            p.StokvelId == stokvelId &&
            p.ContributionCycleId == contributionCycleId);

    if (payout is null)
        throw new NotFoundException("Payout not found.");

    // Tell EF which xmin value the client originally read.
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


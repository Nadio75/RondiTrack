using RondiTrack.Infrastructure;
namespace RondiTrack.Tests;

using Microsoft.EntityFrameworkCore;
using RondiTrack.Data;
using Xunit;

public class ConcurrencyTests
{
    private static RondiTrackDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<RondiTrackDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=RondiTrackDb;Username=postgres;Password=bitcube")
            .Options;

        return new RondiTrackDbContext(options);
    }

    [Fact]
    public async Task Concurrent_Updates_Throw_DbUpdateConcurrencyException()
    {
        // Arrange – use an existing payout so we do not violate
        // the database rule that allows only one payout per cycle.
        Guid payoutId;

        using (var setup = CreateContext())
        {
            var payout = await setup.Payouts.FirstOrDefaultAsync();

            Assert.NotNull(payout);

            payoutId = payout!.Id;
        }

        // Two independent DbContexts represent two separate requests.
        using var ctxA = CreateContext();
        using var ctxB = CreateContext();

        var payoutA = await ctxA.Payouts.FindAsync(payoutId);
        var payoutB = await ctxB.Payouts.FindAsync(payoutId);

        Assert.NotNull(payoutA);
        Assert.NotNull(payoutB);

        // Context A wins.
        ctxA.Entry(payoutA!)
            .Property("Amount")
            .CurrentValue = 600m;

        await ctxA.SaveChangesAsync();

        // Context B still has the old xmin value.
        ctxB.Entry(payoutB!)
            .Property("Amount")
            .CurrentValue = 700m;

        // PostgreSQL xmin should detect that the row has changed.
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => ctxB.SaveChangesAsync());
    }
}

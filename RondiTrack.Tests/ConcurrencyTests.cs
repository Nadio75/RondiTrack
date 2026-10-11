using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RondiTrack.Infrastructure;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

[Collection("Postgres collection")]
public class ConcurrencyTests
{
    private readonly PostgresApiFactory _factory;

    public ConcurrencyTests(PostgresApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Concurrent_Updates_Throw_DbUpdateConcurrencyException()
    {
        using var scope = _factory.Services.CreateScope();

        var options = scope.ServiceProvider
            .GetRequiredService<DbContextOptions<RondiTrackDbContext>>();

        // Set up a test-owned stokvel.
        await using var setup = new RondiTrackDbContext(options);

        var stokvel = new RondiTrack.Domain.Stokvel(
            "Concurrency Test " + Guid.NewGuid(),
            500m);

        setup.Stokvels.Add(stokvel);
        await setup.SaveChangesAsync();

        // Two independent contexts load the same row.
        await using var ctxA = new RondiTrackDbContext(options);
        await using var ctxB = new RondiTrackDbContext(options);

        var stokvelA = await ctxA.Stokvels.FindAsync(stokvel.Id);
        var stokvelB = await ctxB.Stokvels.FindAsync(stokvel.Id);

        Assert.NotNull(stokvelA);
        Assert.NotNull(stokvelB);

        // First context saves a real change.
        ctxA.Entry(stokvelA!)
            .Property("ContributionAmount")
            .CurrentValue = 600m;

        await ctxA.SaveChangesAsync();

        // Second context still holds the old xmin value.
        ctxB.Entry(stokvelB!)
            .Property("ContributionAmount")
            .CurrentValue = 700m;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => ctxB.SaveChangesAsync());
    }
}
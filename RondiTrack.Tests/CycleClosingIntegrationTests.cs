
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RondiTrack.Domain;
using RondiTrack.Infrastructure;
using RondiTrack.Services;
using RondiTrack.Tests.TestSupport;
using Xunit;

namespace RondiTrack.Tests;

public sealed class CycleClosingIntegrationTests
{
    [Fact]
    public async Task ClosingCycle_RecordsCorrectArrears_AndIsIdempotent()
    {
        await using var factory = new PostgresApiFactory();
        await factory.InitializeAsync();

        var clock = new ManualTimeProvider(
            new DateTimeOffset(2026, 10, 26, 0, 0, 0,
                TimeSpan.Zero));

        var stokvel = new Stokvel("Closing Test Group", 500m);
        var fullyPaid = new User(
            "Fully Paid", "0710000001", "paid@example.com");
        var partlyPaid = new User(
            "Partly Paid", "0710000002", "partial@example.com");
        var cycle = new ContributionCycle(
            stokvel.Id, "2026-10", 500m);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<RondiTrackDbContext>();

            db.Users.AddRange(fullyPaid, partlyPaid);
            db.Stokvels.Add(stokvel);
            var fullyPaidMember = new StokvelMember(stokvel.Id, fullyPaid.Id);
            var partlyPaidMember = new StokvelMember(stokvel.Id, partlyPaid.Id);

            var joinedBeforeDeadline = new DateTime(
                2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

            db.StokvelMembers.AddRange(
                fullyPaidMember,
                partlyPaidMember);

            db.Entry(fullyPaidMember)
                .Property(m => m.JoinedAtUtc)
                .CurrentValue = joinedBeforeDeadline;

            db.Entry(partlyPaidMember)
                .Property(m => m.JoinedAtUtc)
                .CurrentValue = joinedBeforeDeadline;
            db.ContributionCycles.Add(cycle);

            db.Contributions.AddRange(
                new Contribution(
                    stokvel.Id, fullyPaid.Id, 500m, cycle.Id),
                new Contribution(
                    stokvel.Id, partlyPaid.Id, 200m, cycle.Id));

            await db.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<RondiTrackDbContext>();

            var service = new CycleClosingService(
                db,
                clock,
                NullLogger<CycleClosingService>.Instance);

            int closed = await service.CloseDueCyclesAsync(stokvel.Id);

            Assert.Equal(1, closed);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<RondiTrackDbContext>();

            var closedCycle = await db.ContributionCycles
                .SingleAsync(c => c.Id == cycle.Id);

            Assert.True(closedCycle.IsClosed);
            Assert.NotNull(closedCycle.ClosedAtUtc);

            var arrears = await db.ContributionArrears
                .Where(a => a.ContributionCycleId == cycle.Id)
                .ToListAsync();

            var outstanding = Assert.Single(arrears);

            Assert.Equal(partlyPaid.Id, outstanding.UserId);
            Assert.Equal(30000L, outstanding.AmountCents);

            Assert.True(await db.ContributionCycles.AnyAsync(
                c => c.StokvelId == stokvel.Id &&
                     c.Period == "2026-11" &&
                     !c.IsClosed));
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<RondiTrackDbContext>();

            var service = new CycleClosingService(
                db,
                clock,
                NullLogger<CycleClosingService>.Instance);

            int closedAgain = await service.CloseDueCyclesAsync(stokvel.Id);

            Assert.Equal(0, closedAgain);

            Assert.Equal(1, await db.ContributionArrears
                .CountAsync(a => a.ContributionCycleId == cycle.Id));

            Assert.Equal(1, await db.ContributionCycles
                .CountAsync(c => c.StokvelId == stokvel.Id &&
                                 c.Period == "2026-11"));
        }
    }

    [Fact]
    public async Task ClosingCycle_CatchesUpMultipleMissedMonths()
    {
        await using var factory = new PostgresApiFactory();
        await factory.InitializeAsync();

        var clock = new ManualTimeProvider(
            new DateTimeOffset(2026, 10, 10, 12, 0, 0,
                TimeSpan.Zero));

        var stokvel = new Stokvel("Catch Up Group", 250m);
        var julyCycle = new ContributionCycle(
            stokvel.Id, "2026-07", 250m);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<RondiTrackDbContext>();

            db.Stokvels.Add(stokvel);
            db.ContributionCycles.Add(julyCycle);
            await db.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<RondiTrackDbContext>();

            var service = new CycleClosingService(
                db,
                clock,
                NullLogger<CycleClosingService>.Instance);

            int closed = await service.CloseDueCyclesAsync(stokvel.Id);

            Assert.Equal(3, closed);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<RondiTrackDbContext>();

            var cycles = await db.ContributionCycles
                .Where(c => c.StokvelId == stokvel.Id)
                .OrderBy(c => c.Period)
                .ToListAsync();

            Assert.Equal(4, cycles.Count);
            Assert.Equal(
                new[] { "2026-07", "2026-08", "2026-09", "2026-10" },
                cycles.Select(c => c.Period).ToArray());

            Assert.All(cycles.Take(3),
                c => Assert.True(c.IsClosed));

            Assert.False(cycles[3].IsClosed);
        }
    }

    [Fact]
    public async Task ClosingCycle_WaitsUntilJohannesburgDeadline()
    {
        await using var factory = new PostgresApiFactory();
        await factory.InitializeAsync();

        // Closing day 25 means the cycle ends at the start
        // of October 26 in Johannesburg: October 25, 22:00 UTC.
        var clock = new ManualTimeProvider(
            new DateTimeOffset(2026, 10, 25, 21, 59, 59,
                TimeSpan.Zero));

        var stokvel = new Stokvel("Deadline Group", 100m);
        var cycle = new ContributionCycle(
            stokvel.Id, "2026-10", 100m);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<RondiTrackDbContext>();

            db.Stokvels.Add(stokvel);
            db.ContributionCycles.Add(cycle);
            await db.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<RondiTrackDbContext>();

            var service = new CycleClosingService(
                db,
                clock,
                NullLogger<CycleClosingService>.Instance);

            Assert.Equal(
                0,
                await service.CloseDueCyclesAsync(stokvel.Id));
        }

        clock.SetUtcNow(
            new DateTimeOffset(2026, 10, 25, 22, 0, 0,
                TimeSpan.Zero));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<RondiTrackDbContext>();

            var service = new CycleClosingService(
                db,
                clock,
                NullLogger<CycleClosingService>.Instance);

            Assert.Equal(
                1,
                await service.CloseDueCyclesAsync(stokvel.Id));
        }
    }
}

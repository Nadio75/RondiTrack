
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RondiTrack.Domain;
using RondiTrack.Infrastructure;
using RondiTrack.Services;
using RondiTrack.Tests.TestSupport;
using Xunit;

namespace RondiTrack.Tests;

public sealed class DatabaseHousekeepingIntegrationTests
{
    [Fact]
    public async Task Cleanup_DeletesOnlyTokensPastRetentionPeriod()
    {
        await using var factory = new PostgresApiFactory();
        await factory.InitializeAsync();

        var now = new DateTimeOffset(
            2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

        var clock = new ManualTimeProvider(now);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Housekeeping:RefreshTokenRetentionDays"] = "30"
            })
            .Build();

        var user = new User(
            "Housekeeping Test",
            "0719990001",
            "housekeeping@example.com");

        var familyId = Guid.NewGuid();

        // Expired 40 days ago: should be deleted.
        var oldToken = new RefreshToken(
            user.Id,
            familyId,
            "old-token-hash",
            now.UtcDateTime.AddDays(-60),
            now.UtcDateTime.AddDays(-40));

        // Expired 10 days ago: still inside retention.
        var recentToken = new RefreshToken(
            user.Id,
            familyId,
            "recent-token-hash",
            now.UtcDateTime.AddDays(-20),
            now.UtcDateTime.AddDays(-10));

        // Still valid: must never be deleted.
        var activeToken = new RefreshToken(
            user.Id,
            familyId,
            "active-token-hash",
            now.UtcDateTime.AddDays(-1),
            now.UtcDateTime.AddDays(7));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<RondiTrackDbContext>();

            db.Users.Add(user);
            db.RefreshTokens.AddRange(
                oldToken,
                recentToken,
                activeToken);

            await db.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<RondiTrackDbContext>();

            var service = new DatabaseHousekeepingService(
                db,
                clock,
                configuration,
                NullLogger<DatabaseHousekeepingService>.Instance);

            int deleted =
                await service.CleanupExpiredRefreshTokensAsync();

            Assert.Equal(1, deleted);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<RondiTrackDbContext>();

            Assert.False(await db.RefreshTokens.AnyAsync(
                t => t.Id == oldToken.Id));

            Assert.True(await db.RefreshTokens.AnyAsync(
                t => t.Id == recentToken.Id));

            Assert.True(await db.RefreshTokens.AnyAsync(
                t => t.Id == activeToken.Id));
        }
    }
}

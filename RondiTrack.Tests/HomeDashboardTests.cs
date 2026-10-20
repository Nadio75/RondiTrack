
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RondiTrack.Domain;
using RondiTrack.Models.Dtos;
using RondiTrack.Tests.TestSupport;
using RondiTrack.Infrastructure;

namespace RondiTrack.Tests;

public sealed class HomeDashboardTests :
    IClassFixture<PostgresApiFactory>
{
    private readonly PostgresApiFactory _factory;

    public HomeDashboardTests(PostgresApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Home_requires_authentication()
    {
        using var client = _factory.CreateClient();

        client.DefaultRequestHeaders.Add(
            "X-Test-Unauthenticated", "true");

        using var response =
            await client.GetAsync("/api/v1/me/home");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task Home_returns_empty_list_for_user_without_stokvels()
    {
        using var client = _factory.CreateClient();

        var user = await client.CreateUserAsync(
            "Dashboard Empty User");

        client.UseTestUser(user.Id);

        using var response =
            await client.GetAsync("/api/v1/me/home");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var home = await response.Content
            .ReadFromJsonAsync<HomeDashboardResponse>();

        Assert.NotNull(home);
        Assert.Empty(home.Stokvels);
    }

    [Fact]
    public async Task Home_shows_only_callers_stokvels_and_roles()
    {
        using var client = _factory.CreateClient();

        var caller = await client.CreateUserAsync(
            "Dashboard Caller");

        var first = await client.CreateStokvelAsync(500m);
        var second = await client.CreateStokvelAsync(750m);
        var unrelated = await client.CreateStokvelAsync(900m);

        await _factory.EnsureMembershipAsync(
            first.Id, caller.Id, "Chairperson");

        await _factory.EnsureMembershipAsync(
            second.Id, caller.Id, "Member");

        var otherUser = await client.CreateUserAsync(
            "Unrelated Member");

        await _factory.EnsureMembershipAsync(
            unrelated.Id, otherUser.Id, "Member");

        client.UseTestUser(caller.Id);

        using var response =
            await client.GetAsync("/api/v1/me/home");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var home = await response.Content
            .ReadFromJsonAsync<HomeDashboardResponse>();

        Assert.NotNull(home);
        Assert.Equal(2, home.Stokvels.Count);

        var firstHome = Assert.Single(
            home.Stokvels,
            s => s.StokvelId == first.Id);

        Assert.Equal("Chairperson", firstHome.Role);
        Assert.Equal("500.00", firstHome.ContributionAmount);
        Assert.Equal(50000L, firstHome.ContributionAmountCents);

        var secondHome = Assert.Single(
            home.Stokvels,
            s => s.StokvelId == second.Id);

        Assert.Equal("Member", secondHome.Role);
        Assert.Equal("750.00", secondHome.ContributionAmount);
        Assert.Equal(75000L, secondHome.ContributionAmountCents);

        Assert.DoesNotContain(
            home.Stokvels,
            s => s.StokvelId == unrelated.Id);
    }

    [Fact]
    public async Task Home_shows_payment_status_without_leaking_other_members()
    {
        using var client = _factory.CreateClient();

        var scenario =
            await client.ArrangeMemberWithCycleAsync(_factory);

        var otherMember = await client.CreateUserAsync(
            "Dashboard Other Member");

        await _factory.EnsureMembershipAsync(
            scenario.Stokvel.Id,
            otherMember.Id,
            "Member");

        // Record a payment for the other member only.
        using (var contribution =
            await client.PostContributionAsync(
                scenario.Stokvel.Id,
                Guid.NewGuid().ToString(),
                new
                {
                    userId = otherMember.Id,
                    amount = 500m,
                    contributionCycleId = scenario.Cycle.Id
                }))
        {
            Assert.Equal(
                HttpStatusCode.Created,
                contribution.StatusCode);
        }

        // The caller has not paid.
        using (var response =
            await client.GetAsync("/api/v1/me/home"))
        {
            Assert.Equal(
                HttpStatusCode.OK,
                response.StatusCode);

            var home = await response.Content
                .ReadFromJsonAsync<HomeDashboardResponse>();

            Assert.NotNull(home);

            var stokvel = Assert.Single(
                home.Stokvels,
                s => s.StokvelId == scenario.Stokvel.Id);

            Assert.NotNull(stokvel.CurrentCycle);
            Assert.False(stokvel.CurrentCycle.HasPaid);

            Assert.Equal(
                "500.00",
                stokvel.CurrentCycle.AmountOwed);

            Assert.Equal(
                0L,
                stokvel.CurrentCycle.PaidCents);

            Assert.Equal(
                50000L,
                stokvel.CurrentCycle.OwedCents);

            Assert.NotNull(stokvel.CurrentCycle.DueAtUtc);

            Assert.Equal(
                DateTimeKind.Utc,
                stokvel.CurrentCycle.DueAtUtc.Value.Kind);

            Assert.Null(stokvel.CurrentCycle.NextPayoutDate);
        }

        // Record the caller's own contribution.
        using (var contribution =
            await client.PostContributionAsync(
                scenario.Stokvel.Id,
                Guid.NewGuid().ToString(),
                ApiTestHelpers.ContributionBody(scenario)))
        {
            Assert.Equal(
                HttpStatusCode.Created,
                contribution.StatusCode);
        }

        // The caller should now show as paid.
        using (var response =
            await client.GetAsync("/api/v1/me/home"))
        {
            Assert.Equal(
                HttpStatusCode.OK,
                response.StatusCode);

            var home = await response.Content
                .ReadFromJsonAsync<HomeDashboardResponse>();

            Assert.NotNull(home);

            var stokvel = Assert.Single(
                home.Stokvels,
                s => s.StokvelId == scenario.Stokvel.Id);

            Assert.NotNull(stokvel.CurrentCycle);
            Assert.True(stokvel.CurrentCycle.HasPaid);

            Assert.Equal(
                "0.00",
                stokvel.CurrentCycle.AmountOwed);

            Assert.Equal(
                50000L,
                stokvel.CurrentCycle.PaidCents);

            Assert.Equal(
                0L,
                stokvel.CurrentCycle.OwedCents);
        }
    }

    [Fact]
    public async Task Home_returns_only_the_callers_recorded_arrears_per_stokvel()
    {
        using var client = _factory.CreateClient();

        // Create two different users.
        var caller = await client.CreateUserAsync(
            "Arrears Caller");

        var otherUser = await client.CreateUserAsync(
            "Arrears Other User");

        // Create three stokvels.
        var first = await client.CreateStokvelAsync(500m);
        var second = await client.CreateStokvelAsync(750m);
        var unrelated = await client.CreateStokvelAsync(900m);

        // Caller belongs to the first two stokvels.
        await _factory.EnsureMembershipAsync(
            first.Id,
            caller.Id,
            "Chairperson");

        await _factory.EnsureMembershipAsync(
            second.Id,
            caller.Id,
            "Chairperson");

        // Another member also belongs to the first stokvel.
        await _factory.EnsureMembershipAsync(
            first.Id,
            otherUser.Id,
            "Member");

        // Only the other user belongs to the unrelated stokvel.
        await _factory.EnsureMembershipAsync(
            unrelated.Id,
            otherUser.Id,
            "Chairperson");

        // Authenticate as the caller to create the first two cycles.
        client.UseTestUser(caller.Id);

        var firstCycle = await client.CreateCycleAsync(
            first.Id,
            "2026-08");

        var secondCycle = await client.CreateCycleAsync(
            second.Id,
            "2026-08");

        // IMPORTANT:
        // The caller does not belong to the unrelated stokvel.
        // Switch to its chairperson before creating that cycle.
        client.UseTestUser(otherUser.Id);

        var unrelatedCycle = await client.CreateCycleAsync(
            unrelated.Id,
            "2026-08");

        // Insert recorded arrears into the real test database.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<RondiTrackDbContext>();

            var recordedAtUtc = DateTime.UtcNow;

            db.ContributionArrears.AddRange(
                new ContributionArrear(
                    first.Id,
                    firstCycle.Id,
                    caller.Id,
                    50000L,
                    recordedAtUtc),

                new ContributionArrear(
                    second.Id,
                    secondCycle.Id,
                    caller.Id,
                    75000L,
                    recordedAtUtc),

                // Another member's arrears must not be counted.
                new ContributionArrear(
                    first.Id,
                    firstCycle.Id,
                    otherUser.Id,
                    30000L,
                    recordedAtUtc),

                // An unrelated stokvel must not appear.
                new ContributionArrear(
                    unrelated.Id,
                    unrelatedCycle.Id,
                    otherUser.Id,
                    90000L,
                    recordedAtUtc)
            );

            await db.SaveChangesAsync();
        }

        // Switch back to the caller before requesting the dashboard.
        client.UseTestUser(caller.Id);

        var home = await client.GetFromJsonAsync<HomeDashboardResponse>(
            "/api/v1/me/home");

        Assert.NotNull(home);
        Assert.Equal(2, home.Stokvels.Count);

        var firstHome = Assert.Single(
            home.Stokvels,
            s => s.StokvelId == first.Id);

        var secondHome = Assert.Single(
            home.Stokvels,
            s => s.StokvelId == second.Id);

        // Verify amounts are in cents.
        Assert.Equal(
            50000L,
            firstHome.PersonalArrearsCents);

        Assert.Equal(
            75000L,
            secondHome.PersonalArrearsCents);

        // Verify that unrelated stokvel data stays private.
        Assert.DoesNotContain(
            home.Stokvels,
            s => s.StokvelId == unrelated.Id);
    }
}
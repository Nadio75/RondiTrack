
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RondiTrack.Domain;
using RondiTrack.Models.Dtos;
using RondiTrack.Tests.TestSupport;

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
            home.Stokvels, s => s.StokvelId == first.Id);

        Assert.Equal("Chairperson", firstHome.Role);
        Assert.Equal("500.00", firstHome.ContributionAmount);
        Assert.Equal(50000L, firstHome.ContributionAmountCents);

        var secondHome = Assert.Single(
            home.Stokvels, s => s.StokvelId == second.Id);

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

        // Record payment for the other member only.
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

        // Caller has not paid, even though another member has.
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
                Assert.Equal(0L, stokvel.CurrentCycle.PaidCents);
Assert.Equal(50000L, stokvel.CurrentCycle.OwedCents);
Assert.NotNull(stokvel.CurrentCycle.DueAtUtc);
Assert.Equal(
    DateTimeKind.Utc,
    stokvel.CurrentCycle.DueAtUtc.Value.Kind);
            Assert.Null(stokvel.CurrentCycle.NextPayoutDate);
        }

        // Now record the caller's own contribution.
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
                Assert.Equal(50000L, stokvel.CurrentCycle.PaidCents);
Assert.Equal(0L, stokvel.CurrentCycle.OwedCents);
        }
    }
}

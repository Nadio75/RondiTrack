
using RondiTrack.Models.Dtos;
using RondiTrack.Infrastructure;
using RondiTrack.Domain;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RondiTrack.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

[Collection("Postgres collection")]
public class CyclesApiTests
{
    private readonly HttpClient _client;
    private readonly PostgresApiFactory _factory;

    public CyclesApiTests(PostgresApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ---------- Happy paths ----------

    [Fact]
    public async Task Creating_a_cycle_returns_201_with_a_location_and_the_cycle_can_then_be_fetched()
    {
        var stokvel = await _client.CreateStokvelAsync();

        await _client.ArrangeChairpersonAsync(_factory, stokvel.Id);

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/stokvels/{stokvel.Id}/cycles",
            new { period = "2026-09", targetAmount = 1000 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<ContributionCycleResponse>();

        Assert.Equal(stokvel.Id, created!.StokvelId);
        Assert.Equal("2026-09", created.Period);
        Assert.EndsWith(
            $"/api/v1/stokvels/{stokvel.Id}/cycles/{created.Id}",
            response.Headers.Location!.ToString());

        var fetched = await _client.GetFromJsonAsync<ContributionCycleResponse>(
            $"/api/v1/stokvels/{stokvel.Id}/cycles/{created.Id}");

        Assert.Equal(created.Id, fetched!.Id);
        Assert.Equal(created.StokvelId, fetched.StokvelId);
        Assert.Equal(created.Period, fetched.Period);

        Assert.True(
            created.TargetAmount == fetched.TargetAmount,
            $"Expected {created.TargetAmount}, got {fetched.TargetAmount}");

        Assert.True(
            (created.CreatedAt - fetched.CreatedAt).Duration()
                < TimeSpan.FromMilliseconds(1),
            $"Expected {created.CreatedAt}, got {fetched.CreatedAt}");
    }

    [Fact]
    public async Task Listing_cycles_returns_only_that_stokvels_cycles()
    {
        var stokvel = await _client.CreateStokvelAsync();
        var other = await _client.CreateStokvelAsync();

        var chairperson = await _client.ArrangeChairpersonAsync(
            _factory, stokvel.Id);

        await _factory.EnsureMembershipAsync(
            other.Id, chairperson.Id, "Chairperson");

        var mine = await _client.CreateCycleAsync(stokvel.Id, "2026-09");
        await _client.CreateCycleAsync(other.Id, "2026-09");

        var cycles = await _client.GetFromJsonAsync<List<ContributionCycleResponse>>(
            $"/api/v1/stokvels/{stokvel.Id}/cycles");

        var only = Assert.Single(cycles!);
        Assert.Equal(mine.Id, only.Id);
    }

    [Fact]
    public async Task Updating_a_cycle_replaces_period_and_target_amount()
    {
        var stokvel = await _client.CreateStokvelAsync();

        await _client.ArrangeChairpersonAsync(_factory, stokvel.Id);

        var cycle = await _client.CreateCycleAsync(stokvel.Id);

        var response = await _client.PutAsJsonAsync(
            $"/api/v1/stokvels/{stokvel.Id}/cycles/{cycle.Id}",
            new { period = "2026-10", targetAmount = 1200 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<ContributionCycleResponse>();

        Assert.Equal(cycle.Id, updated!.Id);
        Assert.Equal("2026-10", updated.Period);
        Assert.Equal(1200m, updated.TargetAmount);
    }

    [Fact]
    public async Task Deleting_a_cycle_returns_204_then_404_on_a_second_delete()
    {
        var stokvel = await _client.CreateStokvelAsync();

        await _client.ArrangeChairpersonAsync(_factory, stokvel.Id);

        var cycle = await _client.CreateCycleAsync(stokvel.Id);

        var first = await _client.DeleteAsync(
            $"/api/v1/stokvels/{stokvel.Id}/cycles/{cycle.Id}");

        var second = await _client.DeleteAsync(
            $"/api/v1/stokvels/{stokvel.Id}/cycles/{cycle.Id}");

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        second.AssertProblem(HttpStatusCode.NotFound);
    }
[Fact]
public async Task Listing_cycles_returns_newest_period_first()
{
    var stokvel = await _client.CreateStokvelAsync();

    await _client.ArrangeChairpersonAsync(_factory, stokvel.Id);

    // Deliberately create the cycles out of date order.
    await _client.CreateCycleAsync(stokvel.Id, "2026-10");
    await _client.CreateCycleAsync(stokvel.Id, "2026-08");
    await _client.CreateCycleAsync(stokvel.Id, "2026-09");

    var cycles = await _client.GetFromJsonAsync<
        List<ContributionCycleResponse>>(
        $"/api/v1/stokvels/{stokvel.Id}/cycles");

    Assert.NotNull(cycles);
    Assert.Equal(3, cycles.Count);

    Assert.Equal(
        new[] { "2026-10", "2026-09", "2026-08" },
        cycles.Select(c => c.Period));
}

[Fact]
public async Task Listing_cycles_with_no_cycles_returns_empty_array()
{
    var stokvel = await _client.CreateStokvelAsync();

    await _client.ArrangeChairpersonAsync(_factory, stokvel.Id);

    var cycles = await _client.GetFromJsonAsync<
        List<ContributionCycleResponse>>(
        $"/api/v1/stokvels/{stokvel.Id}/cycles");

    Assert.NotNull(cycles);
    Assert.Empty(cycles);
}

[Fact]
public async Task Listing_cycles_returns_the_correct_South_African_due_date()
{
    var stokvel = await _client.CreateStokvelAsync();

    await _client.ArrangeChairpersonAsync(_factory, stokvel.Id);

    await _client.CreateCycleAsync(stokvel.Id, "2026-10");

    var cycles = await _client.GetFromJsonAsync<
        List<ContributionCycleResponse>>(
        $"/api/v1/stokvels/{stokvel.Id}/cycles");

    var cycle = Assert.Single(cycles!);

    // The default closing day is the 25th.
    // Midnight on October 26 in South Africa is
    // October 25 at 22:00 UTC.
    var expectedDueAtUtc = new DateTime(
        2026, 10, 25, 22, 0, 0, DateTimeKind.Utc);

    Assert.Equal(expectedDueAtUtc, cycle.DueAtUtc);
}

[Fact]
public async Task Listing_cycles_without_contributions_returns_zero_collected()
{
    var stokvel = await _client.CreateStokvelAsync();

    await _client.ArrangeChairpersonAsync(_factory, stokvel.Id);

    var cycle = await _client.CreateCycleAsync(
        stokvel.Id, "2026-09");

    var cycles = await _client.GetFromJsonAsync<
        List<ContributionCycleResponse>>(
        $"/api/v1/stokvels/{stokvel.Id}/cycles");

    var result = Assert.Single(cycles!);

    Assert.Equal(cycle.Id, result.Id);
    Assert.Equal(0L, result.TotalCollectedCents);
    Assert.Equal(
        checked((long)(result.TargetAmount * 100m)),
        result.AmountDueCents);
    Assert.Equal("Open", result.Status);
}


    // ---------- Validation (400), not found (404), conflict (409) ----------

    [Theory]
    [InlineData("", 1000)]
    [InlineData("September 2026", 1000)]
    [InlineData("2026-09", 0)]
    public async Task Creating_a_cycle_that_breaks_a_rule_returns_400(
        string period, int targetAmount)
    {
        var stokvel = await _client.CreateStokvelAsync();

        await _client.ArrangeChairpersonAsync(_factory, stokvel.Id);

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/stokvels/{stokvel.Id}/cycles",
            new { period, targetAmount });

        response.AssertProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Creating_a_cycle_for_a_stokvel_that_does_not_exist_returns_404()
    {
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/stokvels/{Guid.NewGuid()}/cycles",
            new { period = "2026-09", targetAmount = 1000 });

        response.AssertProblem(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Listing_cycles_for_a_stokvel_that_does_not_exist_returns_404()
    {
        var response = await _client.GetAsync(
            $"/api/v1/stokvels/{Guid.NewGuid()}/cycles");

        response.AssertProblem(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Fetching_a_cycle_that_does_not_exist_returns_404()
    {
        var stokvel = await _client.CreateStokvelAsync();

        await _client.ArrangeChairpersonAsync(_factory, stokvel.Id);

        var response = await _client.GetAsync(
            $"/api/v1/stokvels/{stokvel.Id}/cycles/{Guid.NewGuid()}");

        response.AssertProblem(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Creating_a_second_cycle_for_the_same_period_returns_409()
    {
        var stokvel = await _client.CreateStokvelAsync();

        await _client.ArrangeChairpersonAsync(_factory, stokvel.Id);

        await _client.CreateCycleAsync(stokvel.Id, "2026-09");

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/stokvels/{stokvel.Id}/cycles",
            new { period = "2026-09", targetAmount = 2000 });

        response.AssertProblem(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Two_different_stokvels_can_each_have_a_cycle_for_the_same_period()
    {
        var first = await _client.CreateStokvelAsync();
        var second = await _client.CreateStokvelAsync();

        var chairperson = await _client.ArrangeChairpersonAsync(
            _factory, first.Id);

        await _factory.EnsureMembershipAsync(
            second.Id, chairperson.Id, "Chairperson");

        await _client.CreateCycleAsync(first.Id, "2026-09");

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/stokvels/{second.Id}/cycles",
            new { period = "2026-09", targetAmount = 1000 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Updating_a_cycle_with_an_invalid_body_returns_400_even_when_the_cycle_does_not_exist()
    {
        var stokvel = await _client.CreateStokvelAsync();

        await _client.ArrangeChairpersonAsync(_factory, stokvel.Id);

        var response = await _client.PutAsJsonAsync(
            $"/api/v1/stokvels/{stokvel.Id}/cycles/{Guid.NewGuid()}",
            new { period = "not-a-period", targetAmount = 0 });

        response.AssertProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Updating_a_cycle_that_does_not_exist_returns_404()
    {
        var stokvel = await _client.CreateStokvelAsync();

        await _client.ArrangeChairpersonAsync(_factory, stokvel.Id);

        var response = await _client.PutAsJsonAsync(
            $"/api/v1/stokvels/{stokvel.Id}/cycles/{Guid.NewGuid()}",
            new { period = "2026-10", targetAmount = 1200 });

        response.AssertProblem(HttpStatusCode.NotFound);
    }
[Fact]
public async Task Listing_cycles_calculates_collected_and_outstanding_amounts()
{
    var stokvel = await _client.CreateStokvelAsync();

    await _client.ArrangeChairpersonAsync(_factory, stokvel.Id);

    var cycle = await _client.CreateCycleAsync(
        stokvel.Id, "2026-09");

    // Store an actual contribution in the PostgreSQL test database.
    using (var scope = _factory.Services.CreateScope())
    {
        var db = scope.ServiceProvider
            .GetRequiredService<RondiTrackDbContext>();

        var member = await db.StokvelMembers
            .AsNoTracking()
            .FirstAsync(m => m.StokvelId == stokvel.Id);

        db.Contributions.Add(new Contribution(
            stokvel.Id,
            member.UserId,
            750m,
            cycle.Id));

        await db.SaveChangesAsync();
    }

    var cycles = await _client.GetFromJsonAsync<
        List<ContributionCycleResponse>>(
        $"/api/v1/stokvels/{stokvel.Id}/cycles");

    var result = Assert.Single(cycles!);

    Assert.Equal(75000L, result.TotalCollectedCents);
    Assert.Equal(25000L, result.AmountDueCents);
}

[Fact]
public async Task Listing_a_closed_cycle_reports_closed_status()
{
    var stokvel = await _client.CreateStokvelAsync();

    await _client.ArrangeChairpersonAsync(_factory, stokvel.Id);

    var cycle = await _client.CreateCycleAsync(
        stokvel.Id, "2026-09");

    // Close the actual domain object and save its new state.
    using (var scope = _factory.Services.CreateScope())
    {
        var db = scope.ServiceProvider
            .GetRequiredService<RondiTrackDbContext>();

        var storedCycle = await db.ContributionCycles
            .SingleAsync(c => c.Id == cycle.Id);

        storedCycle.Close(DateTime.UtcNow);

        await db.SaveChangesAsync();
    }

    var cycles = await _client.GetFromJsonAsync<
        List<ContributionCycleResponse>>(
        $"/api/v1/stokvels/{stokvel.Id}/cycles");

    var result = Assert.Single(cycles!);

    Assert.True(result.IsClosed);
    Assert.Equal("Closed", result.Status);
    Assert.NotNull(result.ClosedAtUtc);
}
}

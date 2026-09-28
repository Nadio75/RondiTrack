namespace RondiTrack.Tests;

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RondiTrack.Models;

public class CyclesApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public CyclesApiTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();

    // ---------- Happy paths ----------

    [Fact]
    public async Task Creating_a_cycle_returns_201_with_a_location_and_the_cycle_can_then_be_fetched()
    {
        var stokvel = await _client.CreateStokvelAsync();

        var response = await _client.PostAsJsonAsync($"/api/stokvels/{stokvel.Id}/cycles",
            new { period = "2026-09", targetAmount = 1000 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ContributionCycleResponse>();
        Assert.Equal(stokvel.Id, created!.StokvelId);
        Assert.Equal("2026-09", created.Period);
        Assert.EndsWith($"/api/stokvels/{stokvel.Id}/cycles/{created.Id}", response.Headers.Location!.ToString());

        var fetched = await _client.GetFromJsonAsync<ContributionCycleResponse>(
            $"/api/stokvels/{stokvel.Id}/cycles/{created.Id}");
        Assert.Equal(created, fetched);
    }

    [Fact]
    public async Task Listing_cycles_returns_only_that_stokvels_cycles()
    {
        var stokvel = await _client.CreateStokvelAsync();
        var other = await _client.CreateStokvelAsync();
        var mine = await _client.CreateCycleAsync(stokvel.Id, "2026-09");
        await _client.CreateCycleAsync(other.Id, "2026-09");

        var cycles = await _client.GetFromJsonAsync<List<ContributionCycleResponse>>(
            $"/api/stokvels/{stokvel.Id}/cycles");

        var only = Assert.Single(cycles!);
        Assert.Equal(mine.Id, only.Id);
    }

    [Fact]
    public async Task Updating_a_cycle_replaces_period_and_target_amount()
    {
        var stokvel = await _client.CreateStokvelAsync();
        var cycle = await _client.CreateCycleAsync(stokvel.Id);

        var response = await _client.PutAsJsonAsync($"/api/stokvels/{stokvel.Id}/cycles/{cycle.Id}",
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
        var cycle = await _client.CreateCycleAsync(stokvel.Id);

        var first = await _client.DeleteAsync($"/api/stokvels/{stokvel.Id}/cycles/{cycle.Id}");
        var second = await _client.DeleteAsync($"/api/stokvels/{stokvel.Id}/cycles/{cycle.Id}");

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        second.AssertProblem(HttpStatusCode.NotFound);
    }

    // ---------- Validation (400), not found (404), conflict (409) ----------

    [Theory]
    [InlineData("", 1000)]
    [InlineData("September 2026", 1000)]
    [InlineData("2026-09", 0)]
    public async Task Creating_a_cycle_that_breaks_a_rule_returns_400(string period, int targetAmount)
    {
        var stokvel = await _client.CreateStokvelAsync();

        var response = await _client.PostAsJsonAsync($"/api/stokvels/{stokvel.Id}/cycles",
            new { period, targetAmount });

        response.AssertProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Creating_a_cycle_for_a_stokvel_that_does_not_exist_returns_404()
    {
        var response = await _client.PostAsJsonAsync($"/api/stokvels/{Guid.NewGuid()}/cycles",
            new { period = "2026-09", targetAmount = 1000 });

        response.AssertProblem(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Listing_cycles_for_a_stokvel_that_does_not_exist_returns_404()
    {
        var response = await _client.GetAsync($"/api/stokvels/{Guid.NewGuid()}/cycles");

        response.AssertProblem(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Fetching_a_cycle_that_does_not_exist_returns_404()
    {
        var stokvel = await _client.CreateStokvelAsync();

        var response = await _client.GetAsync($"/api/stokvels/{stokvel.Id}/cycles/{Guid.NewGuid()}");

        response.AssertProblem(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Creating_a_second_cycle_for_the_same_period_returns_409()
    {
        var stokvel = await _client.CreateStokvelAsync();
        await _client.CreateCycleAsync(stokvel.Id, "2026-09");

        var response = await _client.PostAsJsonAsync($"/api/stokvels/{stokvel.Id}/cycles",
            new { period = "2026-09", targetAmount = 2000 });

        response.AssertProblem(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Two_different_stokvels_can_each_have_a_cycle_for_the_same_period()
    {
        var first = await _client.CreateStokvelAsync();
        var second = await _client.CreateStokvelAsync();
        await _client.CreateCycleAsync(first.Id, "2026-09");

        var response = await _client.PostAsJsonAsync($"/api/stokvels/{second.Id}/cycles",
            new { period = "2026-09", targetAmount = 1000 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

        [Fact]
    public async Task Updating_a_cycle_with_an_invalid_body_returns_400_even_when_the_cycle_does_not_exist()
    {
        var stokvel = await _client.CreateStokvelAsync();

        var response = await _client.PutAsJsonAsync(
            $"/api/stokvels/{stokvel.Id}/cycles/{Guid.NewGuid()}",
            new { period = "not-a-period", targetAmount = 0 });

        response.AssertProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Updating_a_cycle_that_does_not_exist_returns_404()
    {
        var stokvel = await _client.CreateStokvelAsync();

        var response = await _client.PutAsJsonAsync(
            $"/api/stokvels/{stokvel.Id}/cycles/{Guid.NewGuid()}",
            new { period = "2026-10", targetAmount = 1200 });

        response.AssertProblem(HttpStatusCode.NotFound);
    }
}
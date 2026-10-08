using RondiTrack.Models.Dtos;
using RondiTrack.Infrastructure;
using RondiTrack.Domain;
using RondiTrack.Tests.TestSupport;
namespace RondiTrack.Tests;

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RondiTrack.Models;

// Edge cases the happy-path tests do not reach: an empty collection, values right at a
// validator's limit, and a valid request that depends on another resource still existing.
[Collection("Postgres collection")]
public class EdgeCaseTests
{
    private readonly HttpClient _client;

    public EdgeCaseTests(PostgresApiFactory factory) => _client = factory.CreateClient();

    // ---------- Edge case 1: an empty collection ----------

    [Fact]
    public async Task A_stokvel_with_no_cycles_returns_200_and_an_empty_array_not_404()
    {
        var stokvel = await _client.CreateStokvelAsync();

        var response = await _client.GetAsync($"/api/stokvels/{stokvel.Id}/cycles");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cycles = await response.Content.ReadFromJsonAsync<List<ContributionCycleResponse>>();
        Assert.Empty(cycles!);
    }

    // ---------- Edge case 2: values right at the validators' limits ----------

    [Theory]
    [InlineData("2026-01", 201)]   // first valid month
    [InlineData("2026-12", 201)]   // last valid month
    [InlineData("2026-00", 400)]   // one below the first month
    [InlineData("2026-13", 400)]   // one above the last month
    [InlineData("2026-9", 400)]    // month not zero-padded
    public async Task A_cycle_period_is_accepted_or_rejected_exactly_at_the_month_boundaries(string period, int expected)
    {
        var stokvel = await _client.CreateStokvelAsync();

        var response = await _client.PostAsJsonAsync($"/api/stokvels/{stokvel.Id}/cycles",
            new { period, targetAmount = 1000 });

        Assert.Equal((HttpStatusCode)expected, response.StatusCode);
    }

    [Theory]
    [InlineData(100, 201)]   // exactly at the limit
    [InlineData(101, 400)]   // one over
    public async Task A_stokvel_name_is_accepted_at_100_characters_and_rejected_at_101(int length, int expected)
    {
        var response = await _client.PostAsJsonAsync("/api/stokvels",
            new { name = new string('a', length), contributionAmount = 500 });

        Assert.Equal((HttpStatusCode)expected, response.StatusCode);
    }

    [Fact]
    public async Task The_smallest_positive_contribution_amount_is_accepted()
    {
        var s = await _client.ArrangeMemberWithCycleAsync();

        var response = await _client.PostContributionAsync(
            s.Stokvel.Id, Guid.NewGuid().ToString(), ApiTestHelpers.ContributionBody(s, 0.01m));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task A_contribution_amount_of_exactly_zero_is_rejected()
    {
        var s = await _client.ArrangeMemberWithCycleAsync();

        var response = await _client.PostContributionAsync(
            s.Stokvel.Id, Guid.NewGuid().ToString(), ApiTestHelpers.ContributionBody(s, 0m));

        response.AssertProblem(HttpStatusCode.BadRequest);
    }

    // ---------- Edge case 3: a valid request that touches a resource another rule depends on ----------

    [Fact]
    public async Task A_real_cycle_cannot_be_fetched_through_a_different_stokvels_route()
    {
        var owner = await _client.CreateStokvelAsync();
        var other = await _client.CreateStokvelAsync();
        var cycle = await _client.CreateCycleAsync(owner.Id);

        var response = await _client.GetAsync($"/api/stokvels/{other.Id}/cycles/{cycle.Id}");

        response.AssertProblem(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_valid_contribution_to_a_cycle_that_was_deleted_returns_404()
    {
        var s = await _client.ArrangeMemberWithCycleAsync();
        (await _client.DeleteAsync($"/api/stokvels/{s.Stokvel.Id}/cycles/{s.Cycle.Id}")).EnsureSuccessStatusCode();

        var response = await _client.PostContributionAsync(
            s.Stokvel.Id, Guid.NewGuid().ToString(), ApiTestHelpers.ContributionBody(s));

        response.AssertProblem(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Cycles_can_no_longer_be_listed_once_their_stokvel_is_deleted()
    {
        var stokvel = await _client.CreateStokvelAsync();
        await _client.CreateCycleAsync(stokvel.Id);
        (await _client.DeleteAsync($"/api/stokvels/{stokvel.Id}")).EnsureSuccessStatusCode();

        var response = await _client.GetAsync($"/api/stokvels/{stokvel.Id}/cycles");

        response.AssertProblem(HttpStatusCode.NotFound);
    }
}




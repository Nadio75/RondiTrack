using RondiTrack.Models.Dtos;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

using System.Net;
using System.Net.Http.Json;

[Collection("Postgres collection")]
public class EdgeCaseTests
{
    private readonly HttpClient _client;
    private readonly PostgresApiFactory _factory;

    public EdgeCaseTests(PostgresApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task A_stokvel_with_no_cycles_returns_200_and_an_empty_array_not_404()
    {
        var stokvel = await _client.CreateStokvelAsync();
        await _client.ArrangeChairpersonAsync(_factory, stokvel.Id);

        var response = await _client.GetAsync(
            $"/api/v1/stokvels/{stokvel.Id}/cycles");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var cycles = await response.Content
            .ReadFromJsonAsync<List<ContributionCycleResponse>>();

        Assert.Empty(cycles!);
    }

    [Theory]
    [InlineData("2026-01", 201)]
    [InlineData("2026-12", 201)]
    [InlineData("2026-00", 400)]
    [InlineData("2026-13", 400)]
    [InlineData("2026-9", 400)]
    public async Task A_cycle_period_is_accepted_or_rejected_exactly_at_the_month_boundaries(
        string period,
        int expected)
    {
        var stokvel = await _client.CreateStokvelAsync();
        await _client.ArrangeChairpersonAsync(_factory, stokvel.Id);

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/stokvels/{stokvel.Id}/cycles",
            new { period, targetAmount = 1000 });

        Assert.Equal((HttpStatusCode)expected, response.StatusCode);
    }

    [Theory]
    [InlineData(100, 201)]
    [InlineData(101, 400)]
    public async Task A_stokvel_name_is_accepted_at_100_characters_and_rejected_at_101(
        int length,
        int expected)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/stokvels",
            new
            {
                name = new string('a', length),
                contributionAmount = 500
            });

        Assert.Equal((HttpStatusCode)expected, response.StatusCode);
    }

    [Fact]
    public async Task The_smallest_positive_contribution_amount_is_accepted()
    {
        var s = await _client.ArrangeMemberWithCycleAsync(_factory);

        var response = await _client.PostContributionAsync(
            s.Stokvel.Id,
            Guid.NewGuid().ToString(),
            ApiTestHelpers.ContributionBody(s, 0.01m));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task A_contribution_amount_of_exactly_zero_is_rejected()
    {
        var s = await _client.ArrangeMemberWithCycleAsync(_factory);

        var response = await _client.PostContributionAsync(
            s.Stokvel.Id,
            Guid.NewGuid().ToString(),
            ApiTestHelpers.ContributionBody(s, 0m));

        response.AssertProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_real_cycle_cannot_be_fetched_through_a_different_stokvels_route()
    {
        var owner = await _client.CreateStokvelAsync();
        var other = await _client.CreateStokvelAsync();

        var chairperson = await _client.ArrangeChairpersonAsync(
            _factory, owner.Id);

        await _factory.EnsureMembershipAsync(
            other.Id, chairperson.Id, "Chairperson");

        var cycle = await _client.CreateCycleAsync(owner.Id);

        var response = await _client.GetAsync(
            $"/api/v1/stokvels/{other.Id}/cycles/{cycle.Id}");

        response.AssertProblem(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_valid_contribution_to_a_cycle_that_was_deleted_returns_404()
    {
        var s = await _client.ArrangeMemberWithCycleAsync(_factory);

        (await _client.DeleteAsync(
            $"/api/v1/stokvels/{s.Stokvel.Id}/cycles/{s.Cycle.Id}"))
            .EnsureSuccessStatusCode();

        var response = await _client.PostContributionAsync(
            s.Stokvel.Id,
            Guid.NewGuid().ToString(),
            ApiTestHelpers.ContributionBody(s));

        response.AssertProblem(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Cycles_can_no_longer_be_listed_once_their_stokvel_is_deleted()
    {
        var stokvel = await _client.CreateStokvelAsync();
        await _client.ArrangeChairpersonAsync(_factory, stokvel.Id);

        await _client.CreateCycleAsync(stokvel.Id);

        (await _client.DeleteAsync(
            $"/api/v1/stokvels/{stokvel.Id}"))
            .EnsureSuccessStatusCode();

        var response = await _client.GetAsync(
            $"/api/v1/stokvels/{stokvel.Id}/cycles");

        response.AssertProblem(HttpStatusCode.NotFound);
    }
}
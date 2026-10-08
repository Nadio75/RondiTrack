using RondiTrack.Models.Dtos;
using RondiTrack.Infrastructure;
using RondiTrack.Domain;
using RondiTrack.Tests.TestSupport;
namespace RondiTrack.Tests;

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RondiTrack.Models;

[Collection("Postgres collection")]
public class ContributionsApiTests
{
    private readonly HttpClient _client;

    public ContributionsApiTests(PostgresApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Recording_a_contribution_returns_201_with_the_contribution_and_a_location_pointing_to_the_stokvel()
    {
        var s = await _client.ArrangeMemberWithCycleAsync();

        var response = await _client.PostContributionAsync(
            s.Stokvel.Id, Guid.NewGuid().ToString(), ApiTestHelpers.ContributionBody(s, 500m));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ContributionResponse>();
        Assert.Equal(s.Stokvel.Id, body!.StokvelId);
        Assert.Equal(s.User.Id, body.UserId);
        Assert.Equal(500m, body.Amount);
        Assert.Equal(s.Cycle.Id, body.ContributionCycleId);
        Assert.EndsWith($"/api/stokvels/{s.Stokvel.Id}", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Recording_a_contribution_with_an_amount_of_zero_returns_400()
    {
        var s = await _client.ArrangeMemberWithCycleAsync();

        var response = await _client.PostContributionAsync(
            s.Stokvel.Id, Guid.NewGuid().ToString(), ApiTestHelpers.ContributionBody(s, 0m));

        response.AssertProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Recording_a_contribution_with_an_empty_user_id_returns_400()
    {
        var s = await _client.ArrangeMemberWithCycleAsync();

        var response = await _client.PostContributionAsync(
            s.Stokvel.Id, Guid.NewGuid().ToString(),
            new { userId = Guid.Empty, amount = 500, contributionCycleId = s.Cycle.Id });

        response.AssertProblem(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Recording_a_contribution_without_an_idempotency_key_returns_422()
    {
        var s = await _client.ArrangeMemberWithCycleAsync();

        var response = await _client.PostContributionAsync(
            s.Stokvel.Id, key: null, ApiTestHelpers.ContributionBody(s));

        response.AssertProblem(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Recording_a_contribution_for_a_stokvel_that_does_not_exist_returns_404()
    {
        var s = await _client.ArrangeMemberWithCycleAsync();

        var response = await _client.PostContributionAsync(
            Guid.NewGuid(), Guid.NewGuid().ToString(), ApiTestHelpers.ContributionBody(s));

        response.AssertProblem(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_user_who_is_not_a_member_of_the_stokvel_cannot_contribute_and_gets_404()
    {
        var s = await _client.ArrangeMemberWithCycleAsync();
        var outsider = await _client.CreateUserAsync("Outsider");

        var response = await _client.PostContributionAsync(
            s.Stokvel.Id, Guid.NewGuid().ToString(),
            new { userId = outsider.Id, amount = 500, contributionCycleId = s.Cycle.Id });

        response.AssertProblem(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Contributing_to_a_cycle_that_does_not_exist_returns_404()
    {
        var s = await _client.ArrangeMemberWithCycleAsync();

        var response = await _client.PostContributionAsync(
            s.Stokvel.Id, Guid.NewGuid().ToString(),
            new { userId = s.User.Id, amount = 500, contributionCycleId = Guid.NewGuid() });

        response.AssertProblem(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_member_who_already_paid_a_cycle_cannot_pay_it_again_with_a_new_key_and_gets_409()
    {
        var s = await _client.ArrangeMemberWithCycleAsync();
        var body = ApiTestHelpers.ContributionBody(s);
        var first = await _client.PostContributionAsync(s.Stokvel.Id, Guid.NewGuid().ToString(), body);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await _client.PostContributionAsync(s.Stokvel.Id, Guid.NewGuid().ToString(), body);

        second.AssertProblem(HttpStatusCode.Conflict);
    }
}




using RondiTrack.Models.Dtos;
using RondiTrack.Infrastructure;
using RondiTrack.Domain;
using RondiTrack.Tests.TestSupport;
namespace RondiTrack.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RondiTrack.Models;

// Proves the idempotency guarantee holds through the real pipeline, not just once in Scalar.
[Collection("Postgres collection")]
public class IdempotencyApiTests
{
    private readonly HttpClient _client;

    public IdempotencyApiTests(PostgresApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task The_same_key_and_the_same_body_twice_returns_an_identical_201_the_second_time()
    {
        var s = await _client.ArrangeMemberWithCycleAsync();
        var key = Guid.NewGuid().ToString();
        var body = ApiTestHelpers.ContributionBody(s);

        var first = await _client.PostContributionAsync(s.Stokvel.Id, key, body);
        var second = await _client.PostContributionAsync(s.Stokvel.Id, key, body);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        var firstBody = await first.Content.ReadFromJsonAsync<ContributionResponse>();
        var secondBody = await second.Content.ReadFromJsonAsync<ContributionResponse>();
        Assert.Equal(firstBody, secondBody);            // same id, same createdAt: nothing new was recorded
        Assert.Equal(first.Headers.Location, second.Headers.Location);
    }

    [Fact]
    public async Task The_same_key_with_a_different_amount_is_rejected_with_409()
    {
        var s = await _client.ArrangeMemberWithCycleAsync();
        var key = Guid.NewGuid().ToString();
        await _client.PostContributionAsync(s.Stokvel.Id, key, ApiTestHelpers.ContributionBody(s, 500m));

        var second = await _client.PostContributionAsync(s.Stokvel.Id, key, ApiTestHelpers.ContributionBody(s, 999m));

        second.AssertProblem(HttpStatusCode.Conflict);
        using var problem = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.Equal("Idempotency Key Conflict", problem.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task A_rejected_key_reuse_does_not_disturb_the_original_which_can_still_be_replayed()
    {
        var s = await _client.ArrangeMemberWithCycleAsync();
        var key = Guid.NewGuid().ToString();
        var original = await _client.PostContributionAsync(s.Stokvel.Id, key, ApiTestHelpers.ContributionBody(s, 500m));
        await _client.PostContributionAsync(s.Stokvel.Id, key, ApiTestHelpers.ContributionBody(s, 999m));

        var replay = await _client.PostContributionAsync(s.Stokvel.Id, key, ApiTestHelpers.ContributionBody(s, 500m));

        Assert.Equal(HttpStatusCode.Created, replay.StatusCode);
        Assert.Equal(
            await original.Content.ReadFromJsonAsync<ContributionResponse>(),
            await replay.Content.ReadFromJsonAsync<ContributionResponse>());
    }

    [Fact]
    public async Task A_request_that_failed_is_not_remembered_so_retrying_with_the_same_key_runs_again()
    {
        var stokvel = await _client.CreateStokvelAsync();
        var user = await _client.CreateUserAsync();
        var cycle = await _client.CreateCycleAsync(stokvel.Id);
        var key = Guid.NewGuid().ToString();
        var body = new { userId = user.Id, amount = 500, contributionCycleId = cycle.Id };

        // The user is not a member yet, so this fails with 404 and must not be remembered.
        var failed = await _client.PostContributionAsync(stokvel.Id, key, body);
        failed.AssertProblem(HttpStatusCode.NotFound);

        (await _client.AddMemberAsync(stokvel.Id, user.Id)).EnsureSuccessStatusCode();
        var retry = await _client.PostContributionAsync(stokvel.Id, key, body);

        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
    }
}




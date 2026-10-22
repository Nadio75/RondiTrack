
using System.Net;
using System.Net.Http.Json;
using RondiTrack.Infrastructure;
using RondiTrack.Models.Dtos;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

[Collection("Postgres collection")]
public class RecordCycleContributionApiTests
{
    private readonly HttpClient _client;
    private readonly PostgresApiFactory _factory;

    public RecordCycleContributionApiTests(PostgresApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<HttpResponseMessage> PostAsync(
        Guid stokvelId,
        Guid cycleId,
        string? key,
        Guid userId,
        long amountInCents)
    {
        var url =
            $"/api/v1/stokvels/{stokvelId}/cycles/{cycleId}/contributions";

        using var request = new HttpRequestMessage(
            HttpMethod.Post, url);

        if (key is not null)
        {
            request.Headers.Add("Idempotency-Key", key);
        }

        request.Content = JsonContent.Create(new
        {
            userId,
            amountInCents
        });

        return await _client.SendAsync(request);
    }

    [Fact]
    public async Task Records_integer_cents_as_exact_decimal_rands()
    {
        var s = await _client.ArrangeMemberWithCycleAsync(_factory);

        var response = await PostAsync(
            s.Stokvel.Id,
            s.Cycle.Id,
            Guid.NewGuid().ToString(),
            s.User.Id,
            15050);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body =
            await response.Content.ReadFromJsonAsync<ContributionResponse>();

        Assert.NotNull(body);
        Assert.Equal(150.50m, body.Amount);
        Assert.Equal(s.Cycle.Id, body.ContributionCycleId);
        Assert.Equal(s.User.Id, body.UserId);
    }

    [Fact]
    public async Task Rejects_zero_cents_with_400()
    {
        var s = await _client.ArrangeMemberWithCycleAsync(_factory);

        var response = await PostAsync(
            s.Stokvel.Id,
            s.Cycle.Id,
            Guid.NewGuid().ToString(),
            s.User.Id,
            0);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Replaying_same_key_returns_same_contribution()
    {
        var s = await _client.ArrangeMemberWithCycleAsync(_factory);
        var key = Guid.NewGuid().ToString();

        var first = await PostAsync(
            s.Stokvel.Id, s.Cycle.Id, key, s.User.Id, 15050);

        var second = await PostAsync(
            s.Stokvel.Id, s.Cycle.Id, key, s.User.Id, 15050);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        var firstBody =
            await first.Content.ReadFromJsonAsync<ContributionResponse>();

        var secondBody =
            await second.Content.ReadFromJsonAsync<ContributionResponse>();

        Assert.Equal(firstBody, secondBody);
    }

    [Fact]
    public async Task New_key_for_already_paid_cycle_returns_409()
    {
        var s = await _client.ArrangeMemberWithCycleAsync(_factory);

        var first = await PostAsync(
            s.Stokvel.Id,
            s.Cycle.Id,
            Guid.NewGuid().ToString(),
            s.User.Id,
            15050);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await PostAsync(
            s.Stokvel.Id,
            s.Cycle.Id,
            Guid.NewGuid().ToString(),
            s.User.Id,
            15050);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Missing_idempotency_key_returns_422()
    {
        var s = await _client.ArrangeMemberWithCycleAsync(_factory);

        var response = await PostAsync(
            s.Stokvel.Id,
            s.Cycle.Id,
            null,
            s.User.Id,
            15050);

        Assert.Equal(
            HttpStatusCode.UnprocessableEntity,
            response.StatusCode);
    }
}

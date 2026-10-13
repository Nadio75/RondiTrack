namespace RondiTrack.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RondiTrack.Tests.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RondiTrack.Infrastructure;
using RondiTrack.Domain;

[Collection("Postgres collection")]
public sealed class RateLimitingTests
{
    private readonly PostgresApiFactory _factory;

    public RateLimitingTests(PostgresApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_returns_429_after_IP_limit()
    {
        using var limitedFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:Login:PermitLimit", "2");
            builder.UseSetting("RateLimiting:Login:WindowSeconds", "60");
        });

        using var client = limitedFactory.CreateClient();

        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var response = await client.PostAsJsonAsync(
                "/api/v1/auth/login",
                new
                {
                    email = "not-a-real-user@example.com",
                    password = "WrongPassword123!"
                });

            Assert.NotEqual(
                HttpStatusCode.TooManyRequests,
                response.StatusCode);
        }

        using var rejected = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new
            {
                email = "another-user@example.com",
                password = "WrongPassword123!"
            });

        await AssertRateLimitResponseAsync(rejected);
    }

    [Fact]
    public async Task Financial_limit_is_shared_between_contributions_and_payouts()
    {
        using var limitedFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:Financial:PermitLimit", "2");
            builder.UseSetting("RateLimiting:Financial:WindowSeconds", "60");
        });

        using var client = limitedFactory.CreateClient();

        var stokvelId = Guid.NewGuid();
        var cycleId = Guid.NewGuid();

        var contributionUrl =
            $"/api/v1/stokvels/{stokvelId}/contributions";

        var payoutUrl =
            $"/api/v1/stokvels/{stokvelId}/cycles/{cycleId}/payout";

        using var first = await client.PostAsJsonAsync(
            contributionUrl,
            new { });

        using var second = await client.PostAsJsonAsync(
            payoutUrl,
            new { });

        Assert.NotEqual(
            HttpStatusCode.TooManyRequests,
            first.StatusCode);

        Assert.NotEqual(
            HttpStatusCode.TooManyRequests,
            second.StatusCode);

        using var third = await client.PostAsJsonAsync(
            contributionUrl,
            new { });

        await AssertRateLimitResponseAsync(third);
    }

    [Fact]
    public async Task Different_users_have_independent_financial_limits()
    {
        using var limitedFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RateLimiting:Financial:PermitLimit", "1");
            builder.UseSetting("RateLimiting:Financial:WindowSeconds", "60");
        });

        using var client = limitedFactory.CreateClient();

        var url =
            $"/api/v1/stokvels/{Guid.NewGuid()}/contributions";

        var firstUser = Guid.NewGuid();
        var secondUser = Guid.NewGuid();

        using var first = await SendAsUserAsync(
            client, url, firstUser);

        using var rejected = await SendAsUserAsync(
            client, url, firstUser);

        using var otherUser = await SendAsUserAsync(
            client, url, secondUser);

        Assert.NotEqual(
            HttpStatusCode.TooManyRequests,
            first.StatusCode);

        await AssertRateLimitResponseAsync(rejected);

        Assert.NotEqual(
            HttpStatusCode.TooManyRequests,
            otherUser.StatusCode);
    }

    [Fact]
public async Task Rate_limited_contribution_can_be_retried_without_duplicates()
{
    // Give this test its own rate-limit settings.
    // Only one financial request is allowed every 3 seconds.
    using var limitedFactory = _factory.WithWebHostBuilder(builder =>
    {
        builder.UseSetting(
            "RateLimiting:Financial:PermitLimit", "1");

        builder.UseSetting(
            "RateLimiting:Financial:WindowSeconds", "3");
    });

    using var client = limitedFactory.CreateClient();

    // Create a real stokvel, chairperson and cycle.
    var scenario = await client.ArrangeMemberWithCycleAsync(_factory);

    // Create a second member so we can record a second
    // contribution in the same cycle.
    var secondMember = await client.CreateUserAsync("Second Member");

    await _factory.EnsureMembershipAsync(
        scenario.Stokvel.Id,
        secondMember.Id,
        "Member");

    var firstBody = ApiTestHelpers.ContributionBody(scenario);

    var secondBody = new
    {
        userId = secondMember.Id,
        amount = 500m,
        contributionCycleId = scenario.Cycle.Id
    };

    var firstKey = Guid.NewGuid().ToString();
    var secondKey = Guid.NewGuid().ToString();

    // The first contribution should succeed.
    using var first = await client.PostContributionAsync(
        scenario.Stokvel.Id,
        firstKey,
        firstBody);

    Assert.Equal(HttpStatusCode.Created, first.StatusCode);

    // The second request is within the same rate-limit window.
    // It must be rejected before a contribution is saved.
    using var rejected = await client.PostContributionAsync(
        scenario.Stokvel.Id,
        secondKey,
        secondBody);

    await AssertRateLimitResponseAsync(rejected);

    // Verify that the rejected request did not save a contribution.
{
    var store = limitedFactory.Services
        .GetRequiredService<IContributionStore>();

    var contributions = await store.GetAllAsync();

    var count = contributions.Count(c =>
        c.StokvelId == scenario.Stokvel.Id &&
        c.ContributionCycleId == scenario.Cycle.Id);

    Assert.Equal(1, count);
}

    // Wait until the short test window has reset.
    await Task.Delay(TimeSpan.FromSeconds(3.5));

    // Retry using exactly the same key and body that
    // previously received 429.
    using var retry = await client.PostContributionAsync(
        scenario.Stokvel.Id,
        secondKey,
        secondBody);

    Assert.Equal(HttpStatusCode.Created, retry.StatusCode);

    var successfulResponse =
        await retry.Content.ReadAsStringAsync();

    // Both contributions should now exist.
{
    var store = limitedFactory.Services
        .GetRequiredService<IContributionStore>();

    var contributions = await store.GetAllAsync();

    var count = contributions.Count(c =>
        c.StokvelId == scenario.Stokvel.Id &&
        c.ContributionCycleId == scenario.Cycle.Id);

    Assert.Equal(2, count);
}

    // Wait for another window so the rate limiter permits
    // the idempotent replay.
    await Task.Delay(TimeSpan.FromSeconds(3.5));

    // Repeating the successful request with the same key
    // must return the original response, not create a duplicate.
    using var replay = await client.PostContributionAsync(
        scenario.Stokvel.Id,
        secondKey,
        secondBody);

    Assert.Equal(HttpStatusCode.Created, replay.StatusCode);

    Assert.Equal(
        successfulResponse,
        await replay.Content.ReadAsStringAsync());

    // The idempotent replay must not create a third contribution.
{
    var store = limitedFactory.Services
        .GetRequiredService<IContributionStore>();

    var contributions = await store.GetAllAsync();

    var count = contributions.Count(c =>
        c.StokvelId == scenario.Stokvel.Id &&
        c.ContributionCycleId == scenario.Cycle.Id);

    Assert.Equal(2, count);
}
}

    private static async Task<HttpResponseMessage> SendAsUserAsync(
        HttpClient client,
        string url,
        Guid userId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post, url);

        request.Headers.Add(
            "X-Test-UserId",
            userId.ToString());

        request.Content = JsonContent.Create(new { });

        return await client.SendAsync(request);
    }

    private static async Task AssertRateLimitResponseAsync(
        HttpResponseMessage response)
    {
        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            response.StatusCode);

        Assert.True(
            response.Headers.TryGetValues(
                "Retry-After",
                out var values));

        var retryAfter = Assert.Single(values);

        Assert.True(
            int.TryParse(retryAfter, out var seconds));

        Assert.True(seconds > 0);

        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = document.RootElement;

        Assert.Equal(
            429,
            root.GetProperty("status").GetInt32());

        Assert.False(
            string.IsNullOrWhiteSpace(
                root.GetProperty("title").GetString()));

        Assert.False(
            string.IsNullOrWhiteSpace(
                root.GetProperty("detail").GetString()));

        Assert.False(
            string.IsNullOrWhiteSpace(
                root.GetProperty("correlationId").GetString()));
    }
}
using RondiTrack.Models.Dtos;
namespace RondiTrack.Tests;

using System.Net;
using System.Net.Http.Json;
using RondiTrack.Models;
using RondiTrack.Tests.TestSupport;

// A stokvel with one member and one cycle: everything needed to record a contribution.
public record ApiScenario(StokvelResponse Stokvel, UserResponse User, ContributionCycleResponse Cycle);

internal static class ApiTestHelpers
{
    public static async Task<UserResponse> CreateUserAsync(this HttpClient client, string name = "Test User")
    {
        var response = await client.PostAsJsonAsync("/api/v1/users", new { name, contactNumber = "0821234567" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UserResponse>())!;
    }

    public static async Task<StokvelResponse> CreateStokvelAsync(this HttpClient client, decimal contributionAmount = 500m)
    {
        var response = await client.PostAsJsonAsync("/api/v1/stokvels", new
        {
            name = "Test Stokvel " + Guid.NewGuid(),
            contributionAmount
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<StokvelResponse>())!;
    }

    public static async Task<ContributionCycleResponse> CreateCycleAsync(
        this HttpClient client, Guid stokvelId, string period = "2026-09", decimal targetAmount = 1000m)
    {
        var response = await client.PostAsJsonAsync($"/api/v1/stokvels/{stokvelId}/cycles", new { period, targetAmount });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ContributionCycleResponse>())!;
    }

    public static Task<HttpResponseMessage> AddMemberAsync(this HttpClient client, Guid stokvelId, Guid userId) =>
        client.PostAsJsonAsync($"/api/v1/stokvels/{stokvelId}/members", new { userId });

   public static void UseTestUser(this HttpClient client, Guid userId)
{
    client.DefaultRequestHeaders.Remove("X-Test-UserId");

    client.DefaultRequestHeaders.Add(
        "X-Test-UserId",
        userId.ToString());
}

public static async Task<ApiScenario> ArrangeMemberWithCycleAsync(
    this HttpClient client,
    PostgresApiFactory factory)
{
    // Create a stokvel. It starts with zero members.
    var stokvel = await client.CreateStokvelAsync();

    // Create a real user in the test database.
    var user = await client.CreateUserAsync();

    // Test-only setup: give this user Chairperson membership.
    // This does NOT change the production API's creation rules.
    await factory.EnsureMembershipAsync(
        stokvel.Id,
        user.Id,
        "Chairperson");

    // Make the authenticated test caller this user.
    client.UseTestUser(user.Id);

    // A Chairperson is allowed to open a contribution cycle.
    var cycle = await client.CreateCycleAsync(stokvel.Id);

    return new ApiScenario(stokvel, user, cycle);
}

    public static object ContributionBody(ApiScenario s, decimal amount = 500m) =>
        new { userId = s.User.Id, amount, contributionCycleId = s.Cycle.Id };

    // key = null sends the request with no Idempotency-Key header at all.
    public static Task<HttpResponseMessage> PostContributionAsync(
        this HttpClient client, Guid stokvelId, string? key, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/stokvels/{stokvelId}/contributions")
        {
            Content = JsonContent.Create(body)
        };
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request);
    }

    // Every failure must come back as problem+json with the expected status.
    public static void AssertProblem(this HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
    public static async Task<UserResponse> ArrangeChairpersonAsync(
    this HttpClient client,
    PostgresApiFactory factory,
    Guid stokvelId)
{
    var user = await client.CreateUserAsync();

    await factory.EnsureMembershipAsync(
        stokvelId,
        user.Id,
        "Chairperson");

    client.UseTestUser(user.Id);

    return user;
}
}

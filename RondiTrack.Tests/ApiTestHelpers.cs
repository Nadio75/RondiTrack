using RondiTrack.Models.Dtos;
namespace RondiTrack.Tests;

using System.Net;
using System.Net.Http.Json;
using RondiTrack.Models;
using RondiTrack.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RondiTrack.Infrastructure;

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

    public static async Task<StokvelResponse> CreateStokvelAsync(
    this HttpClient client,
    decimal contributionAmount = 500m)
{
    // Create an actual user before creating the stokvel.
    var creator = await client.CreateUserAsync("Stokvel Creator");

    // Make this user the authenticated test caller.
    client.UseTestUser(creator.Id);

    var response = await client.PostAsJsonAsync(
        "/api/v1/stokvels",
        new
        {
            name = "Test Stokvel " + Guid.NewGuid(),
            contributionAmount
        });

    response.EnsureSuccessStatusCode();

    return (await response.Content
        .ReadFromJsonAsync<StokvelResponse>())!;
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
    // Creating a stokvel now automatically adds its creator
    // as the first member with the Chairperson role.
    var stokvel = await client.CreateStokvelAsync();

    // Retrieve the ID of the authenticated creator.
    var creatorId = Guid.Parse(
        client.DefaultRequestHeaders
            .GetValues("X-Test-UserId")
            .Single());

    // Load that user from the test database.
    await using var scope = factory.Services.CreateAsyncScope();

    var db = scope.ServiceProvider
        .GetRequiredService<RondiTrackDbContext>();

    var creator = await db.Users
        .AsNoTracking()
        .SingleAsync(u => u.Id == creatorId);

    // The creator is already authenticated as Chairperson.
    var cycle = await client.CreateCycleAsync(stokvel.Id);

    return new ApiScenario(
        stokvel,
        new UserResponse(
            creator.Id,
            creator.Name,
            creator.ContactNumber),
        cycle);
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

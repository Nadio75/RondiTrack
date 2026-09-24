namespace RondiTrack.Tests;

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

// WebApplicationFactory<Program> boots the real app in-memory, using the real in-memory
// stores — no database, no mocking, just the actual RondiTrack pipeline end to end.
public class NegativePathTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public NegativePathTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_Stokvel_With_Empty_Name_Returns_400_ProblemJson()
    {
        // A malformed request — this should get rejected by FluentValidation
        // before it ever reaches a controller or the exception hierarchy.
        var response = await _client.PostAsJsonAsync("/api/stokvels", new
        {
            name = "",
            contributionAmount = -5
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Get_Stokvel_That_Does_Not_Exist_Returns_404_ProblemJson()
    {
        // A well-formed request referencing something that simply isn't there —
        // this should throw NotFoundException, caught by the centralized handler.
        var randomId = Guid.NewGuid();

        var response = await _client.GetAsync($"/api/stokvels/{randomId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Creating_Duplicate_Contribution_Cycle_Returns_409_ProblemJson()
    {
        // A business-rule violation — well-formed on its own, but conflicts with
        // something the system already knows. This proves ConflictException
        // reaches the handler and comes back in the same shape as every other failure.

        // Arrange: create a real stokvel to attach a cycle to.
        var createStokvelResponse = await _client.PostAsJsonAsync("/api/stokvels", new
        {
            name = "Test Stokvel " + Guid.NewGuid(),
            contributionAmount = 500
        });
        createStokvelResponse.EnsureSuccessStatusCode();
        var stokvel = await createStokvelResponse.Content.ReadFromJsonAsync<StokvelResponseDto>();

        // Act: create a cycle for a period, then try to create the SAME period again.
        var firstCycleResponse = await _client.PostAsJsonAsync(
            $"/api/stokvels/{stokvel!.Id}/cycles",
            new { period = "2026-09", targetAmount = 6000 });
        firstCycleResponse.EnsureSuccessStatusCode();

        var duplicateCycleResponse = await _client.PostAsJsonAsync(
            $"/api/stokvels/{stokvel.Id}/cycles",
            new { period = "2026-09", targetAmount = 6000 });

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, duplicateCycleResponse.StatusCode);
        Assert.Equal("application/problem+json", duplicateCycleResponse.Content.Headers.ContentType?.MediaType);
    }

    // A tiny local DTO just for deserializing the response in the test above —
    // doesn't need to match your real StokvelResponse exactly, only the fields used here.
    private record StokvelResponseDto(Guid Id, string Name, decimal ContributionAmount, int MemberCount);
}
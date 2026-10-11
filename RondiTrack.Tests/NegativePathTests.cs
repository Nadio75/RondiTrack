using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

using System.Net;
using System.Net.Http.Json;
using Xunit;

[Collection("Postgres collection")]
public class NegativePathTests
{
    private readonly HttpClient _client;
    private readonly PostgresApiFactory _factory;

    public NegativePathTests(PostgresApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_Stokvel_With_Empty_Name_Returns_400_ProblemJson()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/stokvels",
            new
            {
                name = "",
                contributionAmount = -5
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Get_Stokvel_That_Does_Not_Exist_Returns_404_ProblemJson()
    {
        var randomId = Guid.NewGuid();

        var response = await _client.GetAsync(
            $"/api/stokvels/{randomId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Creating_Duplicate_Contribution_Cycle_Returns_409_ProblemJson()
    {
        var stokvel = await _client.CreateStokvelAsync();

        await _client.ArrangeChairpersonAsync(_factory, stokvel.Id);

        var firstCycleResponse = await _client.PostAsJsonAsync(
            $"/api/stokvels/{stokvel.Id}/cycles",
            new
            {
                period = "2026-09",
                targetAmount = 6000
            });

        Assert.Equal(HttpStatusCode.Created, firstCycleResponse.StatusCode);

        var duplicateCycleResponse = await _client.PostAsJsonAsync(
            $"/api/stokvels/{stokvel.Id}/cycles",
            new
            {
                period = "2026-09",
                targetAmount = 6000
            });

        Assert.Equal(
            HttpStatusCode.Conflict,
            duplicateCycleResponse.StatusCode);

        Assert.Equal(
            "application/problem+json",
            duplicateCycleResponse.Content.Headers.ContentType?.MediaType);
    }
}

using System.Net;
using System.Text;
using System.Text.Json;
using RondiTrack.Tests.TestSupport;
using System.Net.Http.Json;

namespace RondiTrack.Tests;

public sealed class Stage5ErrorContractTests :
    IClassFixture<PostgresApiFactory>
{
    private readonly PostgresApiFactory _factory;

    public Stage5ErrorContractTests(PostgresApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Zero_contribution_amount_returns_camelCase_validation_error()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/stokvels",
            new
            {
                name = "Stage 5 Test Stokvel",
                contributionAmount = 0m
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = await ReadProblemAsync(response);
        var root = document.RootElement;

        Assert.Equal(400, root.GetProperty("status").GetInt32());

        var errors = root.GetProperty("errors");

        Assert.True(
            errors.TryGetProperty("contributionAmount", out var messages),
            "Expected camelCase contributionAmount validation error.");

        Assert.NotEmpty(messages.EnumerateArray());
    }

    [Fact]
    public async Task Invalid_name_returns_camelCase_validation_error()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/stokvels",
            new
            {
                name = "",
                contributionAmount = 500m
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = await ReadProblemAsync(response);

        Assert.True(
            document.RootElement
                .GetProperty("errors")
                .TryGetProperty("name", out _));
    }

    [Fact]
    public async Task Quoted_numeric_amount_is_rejected()
    {
        using var client = _factory.CreateClient();

        using var content = new StringContent(
            """
            {
              "name": "Quoted Amount Test",
              "contributionAmount": "500"
            }
            """,
            Encoding.UTF8,
            "application/json");

        using var response = await client.PostAsync(
            "/api/v1/stokvels",
            content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = await ReadProblemAsync(response);

        Assert.Equal(
            400,
            document.RootElement.GetProperty("status").GetInt32());

        Assert.True(
            document.RootElement.TryGetProperty("errors", out var errors));

        Assert.NotEmpty(errors.EnumerateObject());
    }

    [Fact]
    public async Task Malformed_json_returns_validation_problem()
    {
        using var client = _factory.CreateClient();

        using var content = new StringContent(
            """{"name": "Broken", "contributionAmount": """,
            Encoding.UTF8,
            "application/json");

        using var response = await client.PostAsync(
            "/api/v1/stokvels",
            content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = await ReadProblemAsync(response);

        Assert.True(
            document.RootElement.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Unknown_stokvel_returns_problem_details()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/v1/stokvels/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var document = await ReadProblemAsync(response);

        Assert.Equal(
            404,
            document.RootElement.GetProperty("status").GetInt32());
    }

    private static async Task<JsonDocument> ReadProblemAsync(
        HttpResponseMessage response)
    {
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadAsStringAsync();

        var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.True(
            root.TryGetProperty("title", out _),
            $"Missing problem title: {json}");

        Assert.True(
            root.TryGetProperty("status", out _),
            $"Missing problem status: {json}");

        Assert.True(
            root.TryGetProperty("correlationId", out var correlationId),
            $"Missing correlationId: {json}");

        Assert.False(
            string.IsNullOrWhiteSpace(correlationId.GetString()));

        return document;
    }
}

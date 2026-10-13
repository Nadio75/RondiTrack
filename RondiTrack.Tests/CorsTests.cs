
namespace RondiTrack.Tests;

using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using RondiTrack.Tests.TestSupport;

[Collection("Postgres collection")]
public class CorsTests
{
    private readonly PostgresApiFactory _factory;

    private const string AllowedOrigin =
        "http://localhost:3000";

    private const string UnknownOrigin =
        "https://unapproved.example.com";

    public CorsTests(PostgresApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient()
    {
        return _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });
    }

    [Fact]
    public async Task Allowed_origin_preflight_succeeds()
    {
        using var client = CreateClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Options,
            "/api/v1/stokvels");

        request.Headers.Add("Origin", AllowedOrigin);

        request.Headers.Add(
            "Access-Control-Request-Method", "POST");

        request.Headers.Add(
            "Access-Control-Request-Headers",
            "content-type,idempotency-key,authorization");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.Equal(
            AllowedOrigin,
            response.Headers.GetValues(
                "Access-Control-Allow-Origin").Single());

        Assert.Contains(
            "true",
            response.Headers.GetValues(
                "Access-Control-Allow-Credentials"));

        var allowedHeaders = string.Join(
            ",",
            response.Headers.GetValues(
                "Access-Control-Allow-Headers"));

        Assert.Contains(
            "idempotency-key",
            allowedHeaders,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unknown_origin_is_not_allowed()
    {
        using var client = CreateClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Options,
            "/api/v1/stokvels");

        request.Headers.Add("Origin", UnknownOrigin);

        request.Headers.Add(
            "Access-Control-Request-Method", "POST");

        request.Headers.Add(
            "Access-Control-Request-Headers",
            "content-type,idempotency-key");

        using var response = await client.SendAsync(request);

        Assert.False(
            response.Headers.Contains(
                "Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Unauthorized_response_includes_cors_headers()
    {
        using var client = CreateClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/v1/auth/refresh");

        request.Headers.Add("Origin", AllowedOrigin);

        using var response = await client.SendAsync(request);

        Assert.Equal(
            AllowedOrigin,
            response.Headers.GetValues(
                "Access-Control-Allow-Origin").Single());
    }

[Fact]
public async Task Allowed_origin_exposes_retry_after_header()
{
    using var client = CreateClient();

    // Use a real GET request, not an OPTIONS preflight.
    using var request = new HttpRequestMessage(
        HttpMethod.Get,
        "/api/v1/stokvels");

    request.Headers.Add("Origin", AllowedOrigin);

    using var response = await client.SendAsync(request);

    // The test factory may authenticate the request automatically.
    // We don't need to assert 200 or 401 here.
    // We only need to check the CORS response headers.

    Assert.Equal(
        AllowedOrigin,
        response.Headers.GetValues(
            "Access-Control-Allow-Origin").Single());

    var exposedHeaders = string.Join(
        ",",
        response.Headers.GetValues(
            "Access-Control-Expose-Headers"));

    Assert.Contains(
        "Retry-After",
        exposedHeaders,
        StringComparison.OrdinalIgnoreCase);
}
}

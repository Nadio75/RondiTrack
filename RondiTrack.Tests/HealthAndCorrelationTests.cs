
namespace RondiTrack.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RondiTrack.Tests.TestSupport;

[Collection("Real JWT security collection")]
public class HealthAndCorrelationTests
{
    private readonly RealJwtApiFactory _factory;

    public HealthAndCorrelationTests(RealJwtApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient()
    {
        return _factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false
            });
    }

    [Fact]
    public async Task LiveHealth_IsAnonymousAndHealthy()
    {
        using var client = CreateClient();

        using var response =
            await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal("Healthy", body);
    }

    [Fact]
    public async Task ReadyHealth_IsAnonymousAndHealthyWithDatabase()
    {
        using var client = CreateClient();

        using var response =
            await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal("Healthy", body);
    }

    [Fact]
    public async Task UnauthorizedRequest_ReturnsCorrelationId()
    {
        using var client = CreateClient();

        using var response =
            await client.GetAsync("/api/stokvels");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var correlationId = json.RootElement
            .GetProperty("correlationId")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(correlationId));

        Assert.Equal(32, correlationId!.Length);
    }

    [Fact]
    public async Task IncomingTraceparent_IsPreservedOnUnauthorized()
    {
        using var client = CreateClient();

        const string traceId =
            "1234567890abcdef1234567890abcdef";

        const string traceparent =
            "00-1234567890abcdef1234567890abcdef-0123456789abcdef-01";

        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                "/api/stokvels");

        request.Headers.TryAddWithoutValidation(
            "traceparent",
            traceparent);

        using var response =
            await client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var correlationId = json.RootElement
            .GetProperty("correlationId")
            .GetString();

        Assert.Equal(traceId, correlationId);
    }

    [Fact]
    public async Task IncomingTraceparent_IsPreservedOnNotFound()
    {
        // Use the existing integration-test factory.
        // It supplies test authentication automatically.
        using var client = CreateClient();

        // Register and log in to obtain a real JWT.
        var email = $"correlation-{Guid.NewGuid():N}@example.com";
        const string password = "CorrelationTest123!";

        using var registerResponse =
            await client.PostAsJsonAsync(
                "/api/auth/register",
                new
                {
                    name = "Correlation Test User",
                    contactNumber = "0821234567",
                    email,
                    password
                });

        Assert.Equal(
            HttpStatusCode.Created,
            registerResponse.StatusCode);

        using var loginResponse =
            await client.PostAsJsonAsync(
                "/api/auth/login",
                new
                {
                    email,
                    password
                });

        Assert.Equal(
            HttpStatusCode.OK,
            loginResponse.StatusCode);

        using var loginJson = JsonDocument.Parse(
            await loginResponse.Content.ReadAsStringAsync());

        var accessToken = loginJson.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        const string traceId =
            "abcdef1234567890abcdef1234567890";

        const string traceparent =
            "00-abcdef1234567890abcdef1234567890-0123456789abcdef-01";

        using var request =
            new HttpRequestMessage(
                HttpMethod.Get,
                $"/api/stokvels/{Guid.NewGuid()}");

        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                accessToken);

        request.Headers.TryAddWithoutValidation(
            "traceparent",
            traceparent);

        using var response =
            await client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);

        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var correlationId = json.RootElement
            .GetProperty("correlationId")
            .GetString();

        Assert.Equal(traceId, correlationId);
    }
}

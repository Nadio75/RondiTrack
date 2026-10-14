
namespace RondiTrack.Tests;

using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

public class DatabaseUnavailableHealthTests
{
    private sealed class UnavailableDatabaseFactory
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(
            IWebHostBuilder builder)
        {
            // Port 1 is deliberately used as an unavailable
            // PostgreSQL endpoint for this test.
            builder.UseSetting(
                "ConnectionStrings:RondiTrack",
                "Host=127.0.0.1;Port=1;" +
                "Database=ronditrack_unavailable_test;" +
                "Username=postgres;Password=postgres;" +
                "Timeout=3");
builder.UseSetting("CycleClosing:Enabled", "false");
builder.UseSetting("Housekeeping:Enabled", "false");

            // Test-only JWT configuration is required
            // because Program.cs validates it at startup.
            builder.UseSetting(
                "Jwt:Issuer",
                "RondiTrackHealthTests");

            builder.UseSetting(
                "Jwt:Audience",
                "RondiTrackHealthTests");

            builder.UseSetting(
                "Jwt:SigningKey",
                Convert.ToBase64String(new byte[32]));
        }
    }

    [Fact]
    public async Task Liveness_RemainsHealthy_WhenDatabaseIsUnavailable()
    {
        using var factory = new UnavailableDatabaseFactory();

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        using var response =
            await client.GetAsync("/health/live");

        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", body);
    }

    [Fact]
    public async Task Readiness_ReturnsUnhealthyQuickly_WhenDatabaseIsUnavailable()
    {
        using var factory = new UnavailableDatabaseFactory();

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

        var stopwatch = Stopwatch.StartNew();

        using var response =
            await client.GetAsync("/health/ready");

        stopwatch.Stop();

        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            response.StatusCode);

        Assert.Equal("Unhealthy", body);

        // The registered health-check timeout is 5 seconds.
        // Allow a little extra time for scheduling and
        // test-environment overhead.
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(8),
            $"Readiness took {stopwatch.Elapsed.TotalSeconds:F2} seconds.");
    }
}

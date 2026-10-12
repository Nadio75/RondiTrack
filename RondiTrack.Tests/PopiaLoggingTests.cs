
namespace RondiTrack.Tests;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using RondiTrack.Tests.TestSupport;

[Collection("Real JWT security collection")]
public class PopiaLoggingTests
{
    private readonly RealJwtApiFactory _factory;

    public PopiaLoggingTests(RealJwtApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Authentication_logs_do_not_expose_sensitive_values()
    {
        using var logProvider = new CapturingLoggerProvider();

        using var app = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureLogging(logging =>
            {
                logging.AddProvider(logProvider);
            });
        });

        using var client = app.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                HandleCookies = false,
                AllowAutoRedirect = false
            });

        var uniqueId = Guid.NewGuid().ToString("N");

        var email = $"popia-{uniqueId}@example.com";
        var password = $"SafeTest-{uniqueId}!";
        var wrongPassword = $"WrongTest-{uniqueId}!";

        // 1. Register a dummy user.
        using var registration = await client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                name = "POPIA Test User",
                contactNumber = "0821234567",
                email,
                password
            });

        Assert.Equal(
            HttpStatusCode.Created,
            registration.StatusCode);

        // 2. Log in and collect the test-only tokens.
        using var login = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email,
                password
            });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var loginJson = JsonDocument.Parse(
            await login.Content.ReadAsStringAsync());

        var accessToken = loginJson.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        var refreshCookie = Assert.Single(
            login.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith(
                "ronditrack_refresh=",
                StringComparison.Ordinal));

        var cookiePair = refreshCookie.Split(';')[0];
        var refreshToken = cookiePair[
            "ronditrack_refresh=".Length..];

        Assert.False(string.IsNullOrWhiteSpace(refreshToken));

        // 3. Refresh the session using the cookie.
        using var refreshRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/auth/refresh");

        refreshRequest.Headers.TryAddWithoutValidation(
            "Cookie",
            cookiePair);

        using var refreshResponse = await client.SendAsync(
            refreshRequest);

        Assert.Equal(
            HttpStatusCode.OK,
            refreshResponse.StatusCode);

        using var refreshJson = JsonDocument.Parse(
            await refreshResponse.Content.ReadAsStringAsync());

        var refreshedAccessToken = refreshJson.RootElement
            .GetProperty("accessToken")
            .GetString();

        Assert.False(
            string.IsNullOrWhiteSpace(refreshedAccessToken));

        // 4. Trigger an authentication failure.
        using var failedLogin = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email,
                password = wrongPassword
            });

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            failedLogin.StatusCode);

        // 5. Inspect all captured log messages.
        var logs = logProvider.GetCapturedText();

        foreach (var sensitiveValue in new[]
        {
            email,
            password,
            wrongPassword,
            accessToken!,
            refreshedAccessToken!,
            refreshToken,
            cookiePair
        })
        {
            Assert.DoesNotContain(
                sensitiveValue,
                logs,
                StringComparison.Ordinal);
        }
    }

[Fact]
public async Task Contribution_logs_do_not_expose_sensitive_values()
{
    using var logProvider = new CapturingLoggerProvider();

    using var app = _factory.WithWebHostBuilder(builder =>
    {
        builder.ConfigureLogging(logging =>
        {
            logging.AddProvider(logProvider);
        });
    });

    using var client = app.CreateClient(
        new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            AllowAutoRedirect = false
        });

    var uniqueId = Guid.NewGuid().ToString("N");
    var email = $"contribution-popia-{uniqueId}@example.com";
    var password = $"ContributionTest-{uniqueId}!";

    // Register the Treasurer.
    using var registration = await client.PostAsJsonAsync(
        "/api/auth/register",
        new
        {
            name = "POPIA Contribution Tester",
            contactNumber = "0821234567",
            email,
            password
        });

    Assert.Equal(
        HttpStatusCode.Created,
        registration.StatusCode);

    using var registeredJson = JsonDocument.Parse(
        await registration.Content.ReadAsStringAsync());

    var userId = registeredJson.RootElement
        .GetProperty("id")
        .GetGuid();

    // Log in and obtain a JWT.
    using var login = await client.PostAsJsonAsync(
        "/api/auth/login",
        new { email, password });

    Assert.Equal(HttpStatusCode.OK, login.StatusCode);

    using var loginJson = JsonDocument.Parse(
        await login.Content.ReadAsStringAsync());

    var accessToken = loginJson.RootElement
        .GetProperty("accessToken")
        .GetString();

    Assert.False(string.IsNullOrWhiteSpace(accessToken));

    client.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            accessToken);

    // Create a stokvel.
    using var stokvelResponse = await client.PostAsJsonAsync(
        "/api/stokvels",
        new
        {
            name = $"POPIA Stokvel {uniqueId}",
            contributionAmount = 500
        });

    Assert.Equal(
        HttpStatusCode.Created,
        stokvelResponse.StatusCode);

    using var stokvelJson = JsonDocument.Parse(
        await stokvelResponse.Content.ReadAsStringAsync());

    var stokvelId = stokvelJson.RootElement
        .GetProperty("id")
        .GetGuid();

    // Give the user Treasurer permissions.
    await _factory.EnsureMembershipAsync(
        stokvelId,
        userId,
        "Treasurer");

    // Create a contribution cycle.
    using var cycleResponse = await client.PostAsJsonAsync(
        $"/api/stokvels/{stokvelId}/cycles",
        new
        {
            period = "2026-10",
            targetAmount = 1000
        });

    Assert.Equal(
        HttpStatusCode.Created,
        cycleResponse.StatusCode);

    using var cycleJson = JsonDocument.Parse(
        await cycleResponse.Content.ReadAsStringAsync());

    var cycleId = cycleJson.RootElement
        .GetProperty("id")
        .GetGuid();

    // Use a distinctive amount and idempotency key.
    const decimal amount = 78342.19m;
    var idempotencyKey = $"popia-{uniqueId}";

    using var request = new HttpRequestMessage(
        HttpMethod.Post,
        $"/api/stokvels/{stokvelId}/contributions");

    request.Headers.Add(
        "Idempotency-Key",
        idempotencyKey);

    request.Content = JsonContent.Create(new
    {
        userId,
        amount,
        contributionCycleId = cycleId
    });

    using var contributionResponse =
        await client.SendAsync(request);

    Assert.Equal(
        HttpStatusCode.Created,
        contributionResponse.StatusCode);

    // Check the logs for sensitive values.
    var logs = logProvider.GetCapturedText();

    foreach (var sensitiveValue in new[]
    {
        email,
        password,
        accessToken!,
        idempotencyKey,
        amount.ToString(
            System.Globalization.CultureInfo.InvariantCulture),
        userId.ToString(),
        stokvelId.ToString(),
        cycleId.ToString()
    })
    {
        Assert.DoesNotContain(
            sensitiveValue,
            logs,
            StringComparison.OrdinalIgnoreCase);
    }
}


    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _messages = new();

        public ILogger CreateLogger(string categoryName)
        {
            return new CapturingLogger(_messages);
        }

        public string GetCapturedText()
        {
            return string.Join(
                Environment.NewLine,
                _messages.ToArray());
        }

        public void Dispose()
        {
        }

        private sealed class CapturingLogger : ILogger
        {
            private readonly ConcurrentQueue<string> _messages;

            public CapturingLogger(
                ConcurrentQueue<string> messages)
            {
                _messages = messages;
            }

            public IDisposable BeginScope<TState>(TState state)
                where TState : notnull
            {
                return NullScope.Instance;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return logLevel != LogLevel.None;
            }

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                var message = formatter(state, exception);

                if (exception is not null)
                {
                    message += Environment.NewLine
                        + exception;
                }

                _messages.Enqueue(message);
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}

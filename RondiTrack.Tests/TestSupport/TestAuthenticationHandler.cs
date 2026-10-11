namespace RondiTrack.Tests.TestSupport;

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class TestAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestAuthentication";

    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.ContainsKey("X-Test-Unauthenticated"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        const string defaultUserId =
            "11111111-1111-1111-1111-111111111111";

        var userIdValue = Request.Headers.TryGetValue(
            "X-Test-UserId",
            out var suppliedUserId)
                ? suppliedUserId.ToString()
                : defaultUserId;

        if (!Guid.TryParse(userIdValue, out var userId))
        {
            return Task.FromResult(
                AuthenticateResult.Fail("Invalid test user ID."));
        }

        var claims = new[]
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                userId.ToString()),
            new Claim(
                ClaimTypes.Name,
                "Integration Test User")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
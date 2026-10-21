
namespace RondiTrack.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RondiTrack.Tests.TestSupport;

[Collection("Real JWT security collection")]
public class AuthSecurityTests
{
    private readonly RealJwtApiFactory _factory;

    public AuthSecurityTests(RealJwtApiFactory factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient()
    {
        return _factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing
                .WebApplicationFactoryClientOptions
            {
                HandleCookies = false,
                AllowAutoRedirect = false
            });
    }

    private static string NewEmail()
    {
        return $"security-{Guid.NewGuid():N}@example.com";
    }

    private static async Task RegisterAsync(
        HttpClient client,
        string email,
        string password = "SecurityTest123!")
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new
            {
                name = "Security Test User",
                contactNumber = "0821234567",
                email,
                password
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string email,
        string password = "SecurityTest123!")
    {
        return await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password });
    }

    private static string GetRefreshCookie(
        HttpResponseMessage response)
    {
        var header = Assert.Single(
            response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith(
                "ronditrack_refresh=",
                StringComparison.Ordinal));

        return header.Split(';')[0];
    }

    private static HttpRequestMessage CookieRequest(
        HttpMethod method,
        string path,
        string cookie)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation(
            "Cookie", cookie);
        return request;
    }

    [Fact]
    public async Task Login_returns_access_token_and_secure_refresh_cookie()
    {
        using var client = CreateClient();
        var email = NewEmail();

        await RegisterAsync(client, email);

        var response = await LoginAsync(client, email);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var accessToken = json.RootElement
            .GetProperty("accessToken").GetString();

        Assert.False(string.IsNullOrWhiteSpace(accessToken));
        Assert.Equal(
            "Bearer",
            json.RootElement.GetProperty("tokenType").GetString());
        Assert.Equal(
            900,
            json.RootElement.GetProperty("expiresIn").GetInt32());

        var cookieHeader = string.Join(
            "; ",
            response.Headers.GetValues("Set-Cookie"));

        Assert.Contains("ronditrack_refresh=", cookieHeader);
        Assert.Contains("httponly", cookieHeader,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookieHeader,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookieHeader,
            StringComparison.OrdinalIgnoreCase);
    }

   
[Fact]
public async Task Wrong_password_and_unknown_email_return_same_problem()
{
    using var client = CreateClient();
    var email = NewEmail();

    await RegisterAsync(client, email);

    using var wrongPassword = await LoginAsync(
        client, email, "IncorrectPassword123!");

    using var unknownEmail = await LoginAsync(
        client, NewEmail(), "IncorrectPassword123!");

    Assert.Equal(
        HttpStatusCode.Unauthorized,
        wrongPassword.StatusCode);

    Assert.Equal(
        HttpStatusCode.Unauthorized,
        unknownEmail.StatusCode);

    Assert.Equal(
        "application/problem+json",
        wrongPassword.Content.Headers.ContentType?.MediaType);

    Assert.Equal(
        "application/problem+json",
        unknownEmail.Content.Headers.ContentType?.MediaType);

    using var first = JsonDocument.Parse(
        await wrongPassword.Content.ReadAsStringAsync());

    using var second = JsonDocument.Parse(
        await unknownEmail.Content.ReadAsStringAsync());

    // Both authentication failures must have the same
    // security-relevant response fields.
    foreach (var propertyName in new[]
    {
        "status",
        "title",
        "detail",
        "type"
    })
    {
        var firstHasProperty =
            first.RootElement.TryGetProperty(
                propertyName, out var firstValue);

        var secondHasProperty =
            second.RootElement.TryGetProperty(
                propertyName, out var secondValue);

        Assert.Equal(firstHasProperty, secondHasProperty);

        if (firstHasProperty)
        {
            Assert.Equal(
                firstValue.GetRawText(),
                secondValue.GetRawText());
        }
    }

    // Each response must contain a valid trace correlation ID.
    var firstCorrelationId = first.RootElement
        .GetProperty("correlationId")
        .GetString();

    var secondCorrelationId = second.RootElement
        .GetProperty("correlationId")
        .GetString();

    Assert.NotNull(firstCorrelationId);
    Assert.NotNull(secondCorrelationId);

    Assert.Equal(32, firstCorrelationId.Length);
    Assert.Equal(32, secondCorrelationId.Length);

    Assert.True(
        Guid.TryParseExact(
            firstCorrelationId, "N", out _));

    Assert.True(
        Guid.TryParseExact(
            secondCorrelationId, "N", out _));
}


    [Fact]
    public async Task Refresh_rotates_token_and_returns_new_access_token()
    {
        using var client = CreateClient();
        var email = NewEmail();

        await RegisterAsync(client, email);

        var login = await LoginAsync(client, email);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var originalCookie = GetRefreshCookie(login);

        using var request = CookieRequest(
            HttpMethod.Post, "/api/v1/auth/refresh", originalCookie);

        var refresh = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);

        var rotatedCookie = GetRefreshCookie(refresh);

        Assert.NotEqual(originalCookie, rotatedCookie);

        using var json = JsonDocument.Parse(
            await refresh.Content.ReadAsStringAsync());

        Assert.False(string.IsNullOrWhiteSpace(
            json.RootElement.GetProperty("accessToken").GetString()));
    }

    [Fact]
    public async Task Reusing_old_refresh_token_revokes_new_token()
    {
        using var client = CreateClient();
        var email = NewEmail();

        await RegisterAsync(client, email);

        var login = await LoginAsync(client, email);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var oldCookie = GetRefreshCookie(login);

        using var firstRequest = CookieRequest(
            HttpMethod.Post, "/api/v1/auth/refresh", oldCookie);

        var firstRefresh = await client.SendAsync(firstRequest);

        Assert.Equal(HttpStatusCode.OK, firstRefresh.StatusCode);

        var newCookie = GetRefreshCookie(firstRefresh);

        using var reuseRequest = CookieRequest(
            HttpMethod.Post, "/api/v1/auth/refresh", oldCookie);

        var reuse = await client.SendAsync(reuseRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        using var latestRequest = CookieRequest(
            HttpMethod.Post, "/api/v1/auth/refresh", newCookie);

        var latest = await client.SendAsync(latestRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, latest.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_refresh_token()
    {
        using var client = CreateClient();
        var email = NewEmail();

        await RegisterAsync(client, email);

        var login = await LoginAsync(client, email);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var cookie = GetRefreshCookie(login);

        using var logoutRequest = CookieRequest(
            HttpMethod.Post, "/api/v1/auth/logout", cookie);

        var logout = await client.SendAsync(logoutRequest);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        using var refreshRequest = CookieRequest(
            HttpMethod.Post, "/api/v1/auth/refresh", cookie);

        var refresh = await client.SendAsync(refreshRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Refresh_without_cookie_returns_401()
    {
        using var client = CreateClient();

        var response = await client.PostAsync(
            "/api/v1/auth/refresh", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<(Guid UserId, string Token)> CreateAuthenticatedUserAsync(
        HttpClient client)
    {
        var email = NewEmail();

        var registration = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new
            {
                name = "Authorization Test User",
                contactNumber = "0821234567",
                email,
                password = "SecurityTest123!"
            });

        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);

        using var registered = JsonDocument.Parse(
            await registration.Content.ReadAsStringAsync());

        var userId = registered.RootElement
            .GetProperty("id").GetGuid();

        var login = await LoginAsync(client, email);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        using var loggedIn = JsonDocument.Parse(
            await login.Content.ReadAsStringAsync());

        var token = loggedIn.RootElement
            .GetProperty("accessToken").GetString();

        Assert.False(string.IsNullOrWhiteSpace(token));

        return (userId, token!);
    }

    private static void SetBearerToken(
        HttpClient client,
        string token)
    {
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", token);
    }

    private static async Task<Guid> CreateStokvelAsync(
        HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/stokvels",
            new
            {
                name = $"Security Stokvel {Guid.NewGuid():N}",
                contributionAmount = 500
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        return json.RootElement.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Anonymous_user_cannot_access_protected_stokvel_endpoint()
    {
        using var client = CreateClient();

        var response = await client.GetAsync(
            $"/api/v1/stokvels/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
public async Task Authenticated_nonmember_receives_404_for_stokvel()
{
    using var client = CreateClient();

    // Create the stokvel as its legitimate owner.
    var (_, ownerToken) =
        await CreateAuthenticatedUserAsync(client);

    SetBearerToken(client, ownerToken);

    var stokvelId = await CreateStokvelAsync(client);

    // Create a completely different authenticated user.
    var (_, outsiderToken) =
        await CreateAuthenticatedUserAsync(client);

    SetBearerToken(client, outsiderToken);

    // The outsider must not be able to access the stokvel.
    var response = await client.GetAsync(
        $"/api/v1/stokvels/{stokvelId}");

    Assert.Equal(
        HttpStatusCode.NotFound,
        response.StatusCode);
}
    [Fact]
    public async Task Member_cannot_update_stokvel()
    {
        using var client = CreateClient();

        var (userId, token) = await CreateAuthenticatedUserAsync(client);
        SetBearerToken(client, token);

        var stokvelId = await CreateStokvelAsync(client);

        await _factory.EnsureMembershipAsync(
            stokvelId, userId, "Member");

        var response = await client.PutAsJsonAsync(
            $"/api/v1/stokvels/{stokvelId}",
            new
            {
                name = "Unauthorized Update",
                contributionAmount = 750
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Treasurer_can_update_stokvel()
    {
        using var client = CreateClient();

        var (userId, token) = await CreateAuthenticatedUserAsync(client);
        SetBearerToken(client, token);

        var stokvelId = await CreateStokvelAsync(client);

        await _factory.EnsureMembershipAsync(
            stokvelId, userId, "Treasurer");

        var response = await client.PutAsJsonAsync(
            $"/api/v1/stokvels/{stokvelId}",
            new
            {
                name = "Treasurer Updated Stokvel",
                contributionAmount = 750
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            "Treasurer Updated Stokvel",
            json.RootElement.GetProperty("name").GetString());
    }
[Fact]
public async Task Treasurer_cannot_delete_stokvel()
{
    using var client = CreateClient();

    var (userId, token) = await CreateAuthenticatedUserAsync(client);
    SetBearerToken(client, token);

    var stokvelId = await CreateStokvelAsync(client);

    await _factory.EnsureMembershipAsync(
        stokvelId, userId, "Treasurer");

    var response = await client.DeleteAsync(
        $"/api/v1/stokvels/{stokvelId}");

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

    // The stokvel must still exist.
    var getResponse = await client.GetAsync(
        $"/api/v1/stokvels/{stokvelId}");

    Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
}

[Fact]
public async Task Chairperson_can_delete_stokvel()
{
    using var client = CreateClient();

    var (userId, token) = await CreateAuthenticatedUserAsync(client);
    SetBearerToken(client, token);

    var stokvelId = await CreateStokvelAsync(client);

    await _factory.EnsureMembershipAsync(
        stokvelId, userId, "Chairperson");

    var response = await client.DeleteAsync(
        $"/api/v1/stokvels/{stokvelId}");

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

    var getResponse = await client.GetAsync(
        $"/api/v1/stokvels/{stokvelId}");

    Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
}

[Fact]
public async Task Treasurer_cannot_add_member()
{
    using var client = CreateClient();

    var (treasurerId, token) =
        await CreateAuthenticatedUserAsync(client);

    SetBearerToken(client, token);

    var stokvelId = await CreateStokvelAsync(client);

    await _factory.EnsureMembershipAsync(
        stokvelId, treasurerId, "Treasurer");

    // Create another user who is not yet a stokvel member.
    var (newUserId, _) =
        await CreateAuthenticatedUserAsync(client);

    var response = await client.PostAsJsonAsync(
        $"/api/v1/stokvels/{stokvelId}/members",
        new { userId = newUserId });

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
}

[Fact]
public async Task Chairperson_can_add_member()
{
    using var client = CreateClient();

    var (chairpersonId, token) =
        await CreateAuthenticatedUserAsync(client);

    SetBearerToken(client, token);

    var stokvelId = await CreateStokvelAsync(client);

    await _factory.EnsureMembershipAsync(
        stokvelId, chairpersonId, "Chairperson");

    var (newUserId, _) =
        await CreateAuthenticatedUserAsync(client);

    var response = await client.PostAsJsonAsync(
        $"/api/v1/stokvels/{stokvelId}/members",
        new { userId = newUserId });

    Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
}
private static async Task<Guid> CreateCycleAsync(
    HttpClient client,
    Guid stokvelId)
{
    var response = await client.PostAsJsonAsync(
        $"/api/v1/stokvels/{stokvelId}/cycles",
        new
        {
            period = "2026-10",
            targetAmount = 1000
        });

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    using var json = JsonDocument.Parse(
        await response.Content.ReadAsStringAsync());

    return json.RootElement.GetProperty("id").GetGuid();
}

private static async Task<HttpResponseMessage> SubmitContributionAsync(
    HttpClient client,
    Guid stokvelId,
    Guid userId,
    Guid cycleId)
{
    using var request = new HttpRequestMessage(
        HttpMethod.Post,
        $"/api/v1/stokvels/{stokvelId}/contributions");

    request.Headers.Add(
        "Idempotency-Key",
        Guid.NewGuid().ToString());

    request.Content = JsonContent.Create(new
    {
        userId,
        amount = 500,
        contributionCycleId = cycleId
    });

    return await client.SendAsync(request);
}

[Fact]
public async Task Member_cannot_record_another_members_contribution()
{
    using var client = CreateClient();

    var (memberId, memberToken) =
        await CreateAuthenticatedUserAsync(client);

    var (otherMemberId, _) =
        await CreateAuthenticatedUserAsync(client);

    var (treasurerId, treasurerToken) =
        await CreateAuthenticatedUserAsync(client);

    SetBearerToken(client, treasurerToken);

    var stokvelId = await CreateStokvelAsync(client);

    await _factory.EnsureMembershipAsync(
        stokvelId, treasurerId, "Treasurer");

    await _factory.EnsureMembershipAsync(
        stokvelId, memberId, "Member");

    await _factory.EnsureMembershipAsync(
        stokvelId, otherMemberId, "Member");

    var cycleId = await CreateCycleAsync(client, stokvelId);

    SetBearerToken(client, memberToken);

    var response = await SubmitContributionAsync(
        client, stokvelId, otherMemberId, cycleId);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
}

[Fact]
public async Task Member_can_record_own_contribution()
{
    using var client = CreateClient();

    var (memberId, memberToken) =
        await CreateAuthenticatedUserAsync(client);

    var (treasurerId, treasurerToken) =
        await CreateAuthenticatedUserAsync(client);

    SetBearerToken(client, treasurerToken);

    var stokvelId = await CreateStokvelAsync(client);

    await _factory.EnsureMembershipAsync(
        stokvelId, treasurerId, "Treasurer");

    await _factory.EnsureMembershipAsync(
        stokvelId, memberId, "Member");

    var cycleId = await CreateCycleAsync(client, stokvelId);

    SetBearerToken(client, memberToken);

    var response = await SubmitContributionAsync(
        client, stokvelId, memberId, cycleId);

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
}

[Fact]
public async Task Treasurer_can_record_another_members_contribution()
{
    using var client = CreateClient();

    var (treasurerId, treasurerToken) =
        await CreateAuthenticatedUserAsync(client);

    var (memberId, _) =
        await CreateAuthenticatedUserAsync(client);

    SetBearerToken(client, treasurerToken);

    var stokvelId = await CreateStokvelAsync(client);

    await _factory.EnsureMembershipAsync(
        stokvelId, treasurerId, "Treasurer");

    await _factory.EnsureMembershipAsync(
        stokvelId, memberId, "Member");

    var cycleId = await CreateCycleAsync(client, stokvelId);

    var response = await SubmitContributionAsync(
        client, stokvelId, memberId, cycleId);

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);
}

}

using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Infrastructure;
using RondiTrack.Models;
using RondiTrack.Services;
using RondiTrack.Common;

namespace RondiTrack.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/auth")]public class AuthController : ControllerBase
{
    private const string RefreshCookieName = "ronditrack_refresh";
    private static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(7);

    private readonly IStokvelStore _store;
    private readonly RondiTrackDbContext _db;
    private readonly PasswordHasher<User> _passwordHasher;
    private readonly JwtTokenService _jwt;

    public AuthController(
        IStokvelStore store,
        RondiTrackDbContext db,
        PasswordHasher<User> passwordHasher,
        JwtTokenService jwt)
    {
        _store = store;
        _db = db;
        _passwordHasher = passwordHasher;
        _jwt = jwt;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        if (await _store.GetUserByEmailAsync(normalizedEmail) is not null)
        {
            return Conflict(new ProblemDetails
            {
                Status = 409,
                Title = "Email already registered",
                Detail = "An account with this email address already exists."
            });
        }

        var user = new User(
            request.Name.Trim(),
            request.ContactNumber.Trim(),
            normalizedEmail);

        user.SetPasswordHash(
            _passwordHasher.HashPassword(user, request.Password));

        try
        {
            await _store.AddUserAsync(user);
        }
        catch (DbUpdateException)
        {
            // Recheck for a concurrent registration with the same email.
            if (await _store.GetUserByEmailAsync(normalizedEmail) is not null)
            {
                return Conflict(new ProblemDetails
                {
                    Status = 409,
                    Title = "Email already registered",
                    Detail = "An account with this email address already exists."
                });
            }

            throw;
        }

        return StatusCode(201, new
        {
            user.Id,
            user.Name,
            user.ContactNumber,
            user.Email
        });
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _store.GetUserByEmailAsync(email);

        if (user is null)
            return InvalidCredentials();

        var result = _passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.Password);

        if (result == PasswordVerificationResult.Failed)
            return InvalidCredentials();

        var rawRefreshToken = CreateOpaqueToken();
        var now = DateTime.UtcNow;
        var familyId = Guid.NewGuid();
        var tokenHash = HashToken(rawRefreshToken);

        _db.RefreshTokens.Add(new RefreshToken(
            user.Id,
            familyId,
            tokenHash,
            now,
            now.Add(RefreshLifetime)));

        await _db.SaveChangesAsync();

        SetRefreshCookie(rawRefreshToken);

        return Ok(new
        {
            accessToken = _jwt.CreateAccessToken(user),
            tokenType = "Bearer",
            expiresIn = 900
        });
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        if (!Request.Cookies.TryGetValue(
            RefreshCookieName, out var rawToken)
            || string.IsNullOrWhiteSpace(rawToken))
        {
            ClearRefreshCookie();
            return UnauthorizedProblem();
        }

        var tokenHash = HashToken(rawToken);
        var stored = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (stored is null)
        {
            ClearRefreshCookie();
            return UnauthorizedProblem();
        }

        var now = DateTime.UtcNow;

        // Reuse of a previously revoked token means the token may have
        // been stolen. Revoke every token belonging to its family.
        if (stored.RevokedAtUtc is not null)
        {
            await RevokeFamilyAsync(stored.FamilyId, now);
            ClearRefreshCookie();
            return UnauthorizedProblem();
        }

        if (stored.ExpiresAtUtc <= now)
        {
            stored.Revoke(now);
            await _db.SaveChangesAsync();
            ClearRefreshCookie();
            return UnauthorizedProblem();
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == stored.UserId);

        if (user is null)
        {
            await RevokeFamilyAsync(stored.FamilyId, now);
            ClearRefreshCookie();
            return UnauthorizedProblem();
        }

        var newRawToken = CreateOpaqueToken();
        var newHash = HashToken(newRawToken);

        stored.Revoke(now, newHash);

        _db.RefreshTokens.Add(new RefreshToken(
            user.Id,
            stored.FamilyId,
            newHash,
            now,
            now.Add(RefreshLifetime)));

        await _db.SaveChangesAsync();

        SetRefreshCookie(newRawToken);

        return Ok(new
        {
            accessToken = _jwt.CreateAccessToken(user),
            tokenType = "Bearer",
            expiresIn = 900
        });
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        if (Request.Cookies.TryGetValue(
            RefreshCookieName, out var rawToken)
            && !string.IsNullOrWhiteSpace(rawToken))
        {
            var hash = HashToken(rawToken);

            var stored = await _db.RefreshTokens
                .FirstOrDefaultAsync(t => t.TokenHash == hash);

            if (stored is not null)
                await RevokeFamilyAsync(stored.FamilyId, DateTime.UtcNow);
        }

        ClearRefreshCookie();
        return NoContent();
    }

    private IActionResult InvalidCredentials()
    {
        return Unauthorized(new ProblemDetails
        {
            Status = 401,
            Title = "Authentication failed",
            Detail = "The email address or password is incorrect."
        });
    }

    private IActionResult UnauthorizedProblem()
    {
        return Unauthorized(new ProblemDetails
        {
            Status = 401,
            Title = "Authentication required",
            Detail = "The refresh token is invalid or expired."
        });
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTime now)
    {
        var familyTokens = await _db.RefreshTokens
            .Where(t => t.FamilyId == familyId)
            .ToListAsync();

        foreach (var token in familyTokens)
            token.Revoke(now);

        await _db.SaveChangesAsync();
    }

    private static string CreateOpaqueToken()
    {
        return WebEncoders.Base64UrlEncode(
            RandomNumberGenerator.GetBytes(64));
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    private void SetRefreshCookie(string token)
    {
        Response.Cookies.Append(
            RefreshCookieName,
            token,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
Path = "/" + ApiRoutes.V1 + "/auth"  ,              
Expires = DateTimeOffset.UtcNow.Add(RefreshLifetime)
            });
    }

    private void ClearRefreshCookie()
    {
        Response.Cookies.Delete(
            RefreshCookieName,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/" + ApiRoutes.V1 + "/auth"
            });
    }
}

public sealed class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using RondiTrack.Domain;
using System.Text;

namespace RondiTrack.Services;

public class JwtTokenService
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly byte[] _signingKey;

    public JwtTokenService(IConfiguration configuration)
    {
        _issuer = configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException(
                "Missing required configuration: Jwt:Issuer.");

        _audience = configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException(
                "Missing required configuration: Jwt:Audience.");

        var encodedKey = configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException(
                "Missing required configuration: Jwt:SigningKey.");

        try
        {
            _signingKey = Convert.FromBase64String(encodedKey);
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must be a valid Base64-encoded key.",
                exception);
        }

        if (_signingKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must decode to at least 32 bytes.");
        }
    }

    public string CreateAccessToken(User user)
    {
        var claims = new[]
        {
            new Claim(
                JwtRegisteredClaimNames.Sub,
                user.Id.ToString()),
            new Claim(
                ClaimTypes.NameIdentifier,
                user.Id.ToString()),
            new Claim(
                JwtRegisteredClaimNames.Email,
                user.Email)
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(_signingKey),
            SecurityAlgorithms.HmacSha256);

        var now = DateTime.UtcNow;

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: now,
            expires: now.AddMinutes(15),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

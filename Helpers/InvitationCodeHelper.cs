using System.Security.Cryptography;
using System.Text;

namespace RondiTrack.Helpers;

public static class InvitationCodeHelper
{
    public static string GenerateCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);

        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static string HashCode(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var bytes = SHA256.HashData(
            Encoding.UTF8.GetBytes(code));

        return Convert.ToHexString(bytes);
    }
}

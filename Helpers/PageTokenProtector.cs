namespace RondiTrack.Helpers;

using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;

/// <summary>
/// Protects pagination bookmarks against reading and tampering.
/// Different purposes prevent one endpoint's tokens being used by another.
/// </summary>
public sealed class PageTokenProtector
{
    private readonly IDataProtector _history;
    private readonly IDataProtector _activity;

    public PageTokenProtector(IDataProtectionProvider provider)
    {
        _history = provider.CreateProtector(
            "RondiTrack.ContributionHistory.PageToken.v1");

        _activity = provider.CreateProtector(
            "RondiTrack.Activity.PageToken.v1");
    }

    public string ProtectHistory(string token) =>
        _history.Protect(token);

    public string ProtectActivity(string token) =>
        _activity.Protect(token);

    public bool TryUnprotectHistory(
        string? token,
        out string original)
    {
        return TryUnprotect(_history, token, out original);
    }

    public bool TryUnprotectActivity(
        string? token,
        out string original)
    {
        return TryUnprotect(_activity, token, out original);
    }

    private static bool TryUnprotect(
        IDataProtector protector,
        string? token,
        out string original)
    {
        original = "";

        if (string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            original = protector.Unprotect(token);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

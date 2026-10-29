using Microsoft.AspNetCore.DataProtection;
using RondiTrack.Helpers;
using Xunit;

namespace RondiTrack.Tests;

public sealed class PageTokenProtectorTests
{
    private static PageTokenProtector CreateProtector()
    {
        var provider = new EphemeralDataProtectionProvider();
        return new PageTokenProtector(provider);
    }

    [Fact]
    public void HistoryToken_ModificationIsRejected()
    {
        var protector = CreateProtector();

        var token = protector.ProtectHistory(
            "original-history-bookmark");

        var modified = token[..^1] +
            (token[^1] == 'A' ? "B" : "A");

        Assert.False(protector.TryUnprotectHistory(
            modified, out _));
    }

    [Fact]
    public void ActivityToken_ModificationIsRejected()
    {
        var protector = CreateProtector();

        var token = protector.ProtectActivity(
            "original-activity-bookmark");

        var modified = token[..^1] +
            (token[^1] == 'A' ? "B" : "A");

        Assert.False(protector.TryUnprotectActivity(
            modified, out _));
    }

    [Fact]
    public void ValidHistoryToken_CanBeDecrypted()
    {
        var protector = CreateProtector();

        var token = protector.ProtectHistory(
            "original-history-bookmark");

        Assert.True(protector.TryUnprotectHistory(
            token, out var original));

        Assert.Equal("original-history-bookmark", original);
    }

    [Fact]
    public void ValidActivityToken_CanBeDecrypted()
    {
        var protector = CreateProtector();

        var token = protector.ProtectActivity(
            "original-activity-bookmark");

        Assert.True(protector.TryUnprotectActivity(
            token, out var original));

        Assert.Equal("original-activity-bookmark", original);
    }

    [Fact]
    public void HistoryToken_CannotBeUsedForActivity()
    {
        var protector = CreateProtector();

        var token = protector.ProtectHistory(
            "history-bookmark");

        Assert.False(protector.TryUnprotectActivity(
            token, out _));
    }

    [Fact]
    public void HistoryToken_ChangingProtectedBytesIsRejected()
    {
        var protector = CreateProtector();

        var token = protector.ProtectHistory(
            "real-history-bookmark");

        // Change a byte in the middle of the encrypted data.
        var bytes = Microsoft.AspNetCore.WebUtilities.WebEncoders
            .Base64UrlDecode(token);

        bytes[bytes.Length / 2] ^= 0x01;

        var modified = Microsoft.AspNetCore.WebUtilities.WebEncoders
            .Base64UrlEncode(bytes);

        Assert.False(protector.TryUnprotectHistory(
            modified, out _));
    }

    [Fact]
    public void ActivityToken_ChangingProtectedBytesIsRejected()
    {
        var protector = CreateProtector();

        var token = protector.ProtectActivity(
            "real-activity-bookmark");

        var bytes = Microsoft.AspNetCore.WebUtilities.WebEncoders
            .Base64UrlDecode(token);

        bytes[bytes.Length / 2] ^= 0x01;

        var modified = Microsoft.AspNetCore.WebUtilities.WebEncoders
            .Base64UrlEncode(bytes);

        Assert.False(protector.TryUnprotectActivity(
            modified, out _));
    }
}

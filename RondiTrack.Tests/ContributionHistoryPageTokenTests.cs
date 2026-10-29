using System;
using System.Text;
using System.Text.Json;
using RondiTrack.Helpers;
using Xunit;

namespace RondiTrack.Tests;

public sealed class ContributionHistoryPageTokenTests
{
    private static readonly Guid StokvelId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly Guid MemberId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static readonly Guid ContributionId =
        Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static readonly DateTime CreatedAt =
        new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ValidToken_CanBeDecoded()
    {
        var hash = ContributionHistoryPageToken.CreateFilterHash(
            StokvelId, MemberId, null, null);

        var token = ContributionHistoryPageToken.Encode(
            CreatedAt, ContributionId, hash);

        var success = ContributionHistoryPageToken.TryDecode(
            token, hash, out var date, out var id);

        Assert.True(success);
        Assert.Equal(CreatedAt, date);
        Assert.Equal(ContributionId, id);
    }

    [Fact]
    public void ChangingMember_RejectsOldToken()
    {
        var originalHash = ContributionHistoryPageToken.CreateFilterHash(
            StokvelId, MemberId, null, null);

        var token = ContributionHistoryPageToken.Encode(
            CreatedAt, ContributionId, originalHash);

        var differentMember = Guid.NewGuid();

        var newHash = ContributionHistoryPageToken.CreateFilterHash(
            StokvelId, differentMember, null, null);

        Assert.False(ContributionHistoryPageToken.TryDecode(
            token, newHash, out _, out _));
    }

    [Fact]
    public void ChangingDates_RejectsOldToken()
    {
        var octoberStart =
            new DateTime(2026, 9, 30, 22, 0, 0, DateTimeKind.Utc);

        var novemberStart =
            new DateTime(2026, 10, 31, 22, 0, 0, DateTimeKind.Utc);

        var originalHash = ContributionHistoryPageToken.CreateFilterHash(
            StokvelId, null, octoberStart, novemberStart);

        var token = ContributionHistoryPageToken.Encode(
            CreatedAt, ContributionId, originalHash);

        var changedHash = ContributionHistoryPageToken.CreateFilterHash(
            StokvelId, null, novemberStart, null);

        Assert.False(ContributionHistoryPageToken.TryDecode(
            token, changedHash, out _, out _));
    }

    [Fact]
    public void ChangingSort_RejectsOldToken()
    {
        var newestHash = ContributionHistoryPageToken.CreateFilterHash(
            StokvelId, MemberId, null, null, "newest");

        var token = ContributionHistoryPageToken.Encode(
            CreatedAt, ContributionId, newestHash);

        var oldestHash = ContributionHistoryPageToken.CreateFilterHash(
            StokvelId, MemberId, null, null, "oldest");

        Assert.NotEqual(newestHash, oldestHash);

        Assert.False(ContributionHistoryPageToken.TryDecode(
            token, oldestHash, out _, out _));
    }
    [Theory]
    [InlineData("not-a-valid-token")]
    [InlineData("%%%")]
    [InlineData("e30=")]
    public void MalformedToken_IsRejected(string token)
    {
        var hash = ContributionHistoryPageToken.CreateFilterHash(
            StokvelId, null, null, null);

        Assert.False(ContributionHistoryPageToken.TryDecode(
            token, hash, out _, out _));
    }

    [Fact]
    public void WrongVersion_IsRejected()
    {
        var hash = ContributionHistoryPageToken.CreateFilterHash(
            StokvelId, null, null, null);

        var json = JsonSerializer.Serialize(new
        {
            Version = 99,
            CreatedAt,
            Id = ContributionId,
            FilterHash = hash
        });

        var token = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(json));

        Assert.False(ContributionHistoryPageToken.TryDecode(
            token, hash, out _, out _));
    }
}

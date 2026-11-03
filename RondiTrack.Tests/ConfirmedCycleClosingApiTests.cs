using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RondiTrack.Domain;
using RondiTrack.Infrastructure;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

[Collection("Postgres collection")]
public sealed class ConfirmedCycleClosingApiTests
{
    private readonly PostgresApiFactory _factory;

    public ConfirmedCycleClosingApiTests(PostgresApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<(Guid StokvelId, Guid CycleId, Guid TreasurerId, Guid MemberId)>
        ArrangeAsync()
    {
        var stokvel = new Stokvel("Manual Closing Test", 500m);

        var treasurer = new User(
            "Closing Treasurer",
            "0711234567",
            $"closing-treasurer-{Guid.NewGuid():N}@example.com");

        var member = new User(
            "Closing Member",
            "0721234567",
            $"closing-member-{Guid.NewGuid():N}@example.com");

        // This period has already passed its closing deadline.
        var cycle = new ContributionCycle(stokvel.Id, "2026-10", 500m);

        await using var scope = _factory.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<RondiTrackDbContext>();

        db.Stokvels.Add(stokvel);
        db.Users.AddRange(treasurer, member);

        var treasurerMembership =
            new StokvelMember(stokvel.Id, treasurer.Id, "Treasurer");

        var memberMembership =
            new StokvelMember(stokvel.Id, member.Id, "Member");

        db.StokvelMembers.AddRange(
            treasurerMembership,
            memberMembership);

        var joined = new DateTime(
            2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        db.Entry(treasurerMembership)
            .Property(m => m.JoinedAtUtc)
            .CurrentValue = joined;

        db.Entry(memberMembership)
            .Property(m => m.JoinedAtUtc)
            .CurrentValue = joined;

        db.ContributionCycles.Add(cycle);

        db.Contributions.AddRange(
            new Contribution(stokvel.Id, treasurer.Id, 500m, cycle.Id),
            new Contribution(stokvel.Id, member.Id, 200m, cycle.Id));

        await db.SaveChangesAsync();

        return (stokvel.Id, cycle.Id, treasurer.Id, member.Id);
    }

    private static string PreviewUrl(Guid stokvelId, Guid cycleId) =>
        $"/api/v1/stokvels/{stokvelId}/cycles/{cycleId}/closing-preview";

    private static string CloseUrl(Guid stokvelId, Guid cycleId) =>
        $"/api/v1/stokvels/{stokvelId}/cycles/{cycleId}/close";

    private static async Task<string> GetTokenAsync(
        HttpClient client, Guid stokvelId, Guid cycleId)
    {
        using var response = await client.GetAsync(
            PreviewUrl(stokvelId, cycleId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        return json.RootElement
            .GetProperty("snapshotToken")
            .GetString()!;
    }

    private static async Task<HttpResponseMessage> ConfirmAsync(
        HttpClient client,
        Guid stokvelId,
        Guid cycleId,
        string token,
        string key)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            CloseUrl(stokvelId, cycleId));

        request.Headers.TryAddWithoutValidation(
            "Idempotency-Key", key);

        request.Content = JsonContent.Create(
            new { snapshotToken = token });

        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Treasurer_ClosesCycle_AndRecordsCorrectArrears()
    {
        var data = await ArrangeAsync();

        using var client = _factory.CreateClient();
        client.UseTestUser(data.TreasurerId);

        var token = await GetTokenAsync(
            client, data.StokvelId, data.CycleId);

        using var response = await ConfirmAsync(
            client, data.StokvelId, data.CycleId,
            token, Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(
            30000L,
            json.RootElement
                .GetProperty("totalArrearsCents")
                .GetInt64());

        Assert.False(
            json.RootElement.GetProperty("replayed").GetBoolean());

        await using var scope = _factory.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<RondiTrackDbContext>();

        var cycle = await db.ContributionCycles
            .SingleAsync(c => c.Id == data.CycleId);

        Assert.True(cycle.IsClosed);

        var arrear = await db.ContributionArrears
            .SingleAsync(a => a.ContributionCycleId == data.CycleId);

        Assert.Equal(data.MemberId, arrear.UserId);
        Assert.Equal(30000L, arrear.AmountCents);

        Assert.Equal(
            1,
            await db.CycleClosingRequests.CountAsync(
                r => r.CycleId == data.CycleId));
    }

    [Fact]
    public async Task SameKeyAndToken_ReplaysWithoutDuplicateArrears()
    {
        var data = await ArrangeAsync();

        using var client = _factory.CreateClient();
        client.UseTestUser(data.TreasurerId);

        var token = await GetTokenAsync(
            client, data.StokvelId, data.CycleId);

        var key = Guid.NewGuid().ToString();

        using var first = await ConfirmAsync(
            client, data.StokvelId, data.CycleId, token, key);

        using var second = await ConfirmAsync(
            client, data.StokvelId, data.CycleId, token, key);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        using var json = JsonDocument.Parse(
            await second.Content.ReadAsStringAsync());

        Assert.True(
            json.RootElement.GetProperty("replayed").GetBoolean());

        await using var scope = _factory.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<RondiTrackDbContext>();

        Assert.Equal(
            1,
            await db.ContributionArrears.CountAsync(
                a => a.ContributionCycleId == data.CycleId));

        Assert.Equal(
            1,
            await db.CycleClosingRequests.CountAsync(
                r => r.CycleId == data.CycleId));
    }

    [Fact]
    public async Task OrdinaryMember_CannotCloseCycle()
    {
        var data = await ArrangeAsync();

        using var client = _factory.CreateClient();
        client.UseTestUser(data.MemberId);

        using var response = await ConfirmAsync(
            client, data.StokvelId, data.CycleId,
            new string('A', 64),
            Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StaleSnapshot_Returns409_AndLeavesCycleOpen()
    {
        var data = await ArrangeAsync();

        using var client = _factory.CreateClient();
        client.UseTestUser(data.TreasurerId);

        var token = await GetTokenAsync(
            client, data.StokvelId, data.CycleId);

        // A different token represents an outdated preview.
        var staleToken = token[0] == 'A'
            ? "B" + token[1..]
            : "A" + token[1..];

        using var response = await ConfirmAsync(
            client, data.StokvelId, data.CycleId,
            staleToken,
            Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<RondiTrackDbContext>();

        var cycle = await db.ContributionCycles
            .SingleAsync(c => c.Id == data.CycleId);

        Assert.False(cycle.IsClosed);

        Assert.Equal(
            0,
            await db.ContributionArrears.CountAsync(
                a => a.ContributionCycleId == data.CycleId));
    }
}

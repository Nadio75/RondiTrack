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
public sealed class CycleClosingPreviewApiTests
{
    private readonly PostgresApiFactory _factory;

    public CycleClosingPreviewApiTests(PostgresApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<(Guid StokvelId, Guid CycleId, Guid TreasurerId, Guid MemberId)>
        ArrangeAsync()
    {
        var stokvel = new Stokvel("Preview Test Group", 500m);

        var treasurer = new User(
            "Preview Treasurer",
            "0711234567",
            $"treasurer-{Guid.NewGuid():N}@example.com");

        var member = new User(
            "Partly Paid Member",
            "0721234567",
            $"member-{Guid.NewGuid():N}@example.com");

        var cycle = new ContributionCycle(
            stokvel.Id,
            "2026-11",
            500m);

        await using var scope = _factory.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<RondiTrackDbContext>();

        db.Stokvels.Add(stokvel);
        db.Users.AddRange(treasurer, member);

        var treasurerMembership = new StokvelMember(
            stokvel.Id, treasurer.Id, "Treasurer");

        var memberMembership = new StokvelMember(
            stokvel.Id, member.Id, "Member");

        db.StokvelMembers.AddRange(
            treasurerMembership,
            memberMembership);

        var joinedBeforeDeadline = new DateTime(
            2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

        db.Entry(treasurerMembership)
            .Property(m => m.JoinedAtUtc)
            .CurrentValue = joinedBeforeDeadline;

        db.Entry(memberMembership)
            .Property(m => m.JoinedAtUtc)
            .CurrentValue = joinedBeforeDeadline;

        db.ContributionCycles.Add(cycle);

        db.Contributions.AddRange(
            new Contribution(
                stokvel.Id, treasurer.Id, 500m, cycle.Id),
            new Contribution(
                stokvel.Id, member.Id, 200m, cycle.Id));

        await db.SaveChangesAsync();

        return (stokvel.Id, cycle.Id, treasurer.Id, member.Id);
    }

    [Fact]
    public async Task Treasurer_SeesCorrectArrears_WithoutClosingCycle()
    {
        var data = await ArrangeAsync();

        using var client = _factory.CreateClient();
        client.UseTestUser(data.TreasurerId);

        using var response = await client.GetAsync(
            $"/api/v1/stokvels/{data.StokvelId}/cycles/{data.CycleId}/closing-preview");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = json.RootElement;

        Assert.Equal(
            "2026-11",
            root.GetProperty("period").GetString());

        Assert.Equal(
            30000L,
            root.GetProperty("totalArrearsCents").GetInt64());

        var arrears = root.GetProperty("arrears");
        var item = Assert.Single(arrears.EnumerateArray());

        Assert.Equal(
            data.MemberId,
            item.GetProperty("userId").GetGuid());

        Assert.Equal(
            "Partly Paid Member",
            item.GetProperty("memberName").GetString());

        Assert.Equal(
            20000L,
            item.GetProperty("paidCents").GetInt64());

        Assert.Equal(
            30000L,
            item.GetProperty("arrearsCents").GetInt64());

        await using var scope = _factory.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<RondiTrackDbContext>();

        var cycle = await db.ContributionCycles
            .SingleAsync(c => c.Id == data.CycleId);

        Assert.False(cycle.IsClosed);

        Assert.Equal(0, await db.ContributionArrears
            .CountAsync(a => a.ContributionCycleId == data.CycleId));
    }

    [Fact]
    public async Task OrdinaryMember_Gets403()
    {
        var data = await ArrangeAsync();

        using var client = _factory.CreateClient();
        client.UseTestUser(data.MemberId);

        using var response = await client.GetAsync(
            $"/api/v1/stokvels/{data.StokvelId}/cycles/{data.CycleId}/closing-preview");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Outsider_Gets404()
    {
        var data = await ArrangeAsync();

        using var client = _factory.CreateClient();

        var outsider = await client.CreateUserAsync(
            $"Preview Outsider {Guid.NewGuid():N}");

        client.UseTestUser(outsider.Id);

        using var response = await client.GetAsync(
            $"/api/v1/stokvels/{data.StokvelId}/cycles/{data.CycleId}/closing-preview");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ClosedCycle_Gets409()
    {
        var data = await ArrangeAsync();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<RondiTrackDbContext>();

            var cycle = await db.ContributionCycles
                .SingleAsync(c => c.Id == data.CycleId);

            cycle.Close(DateTime.UtcNow);

            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        client.UseTestUser(data.TreasurerId);

        using var response = await client.GetAsync(
            $"/api/v1/stokvels/{data.StokvelId}/cycles/{data.CycleId}/closing-preview");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}

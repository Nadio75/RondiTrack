
using System.Net;
using System.Net.Http.Json;
using RondiTrack.Models.Dtos;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

[Collection("Postgres collection")]
public sealed class PayoutRotationApiTests
{
    private readonly PostgresApiFactory _factory;

    public PayoutRotationApiTests(PostgresApiFactory factory)
    {
        _factory = factory;
    }

    private static string Url(Guid stokvelId, Guid cycleId) =>
        $"/api/v1/stokvels/{stokvelId}/cycles/{cycleId}/payout-rotation";

    [Fact]
    public async Task Member_can_read_rotation_with_correct_contract()
    {
        using var client = _factory.CreateClient();

        var scenario = await client.ArrangeMemberWithCycleAsync(_factory);

        using var response = await client.GetAsync(
            Url(scenario.Stokvel.Id, scenario.Cycle.Id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var rotation = await response.Content
            .ReadFromJsonAsync<PayoutRotationResponse>();

        Assert.NotNull(rotation);
        Assert.Equal(scenario.Stokvel.Id, rotation.StokvelId);
        Assert.Equal(scenario.Cycle.Id, rotation.CycleId);

        var entry = Assert.Single(rotation.Entries);

        Assert.Equal(scenario.User.Id, entry.MemberId);
        Assert.Equal(scenario.User.Name, entry.MemberName);
        Assert.Equal(1, entry.Position);
        Assert.Equal(50000L, entry.AmountCents);
        Assert.Null(entry.ScheduledDate);
        Assert.Equal("next", entry.Status);
        Assert.True(entry.IsNext);
    }

    [Fact]
    public async Task Rotation_preserves_member_order_and_next_recipient()
    {
        using var client = _factory.CreateClient();

        var scenario = await client.ArrangeMemberWithCycleAsync(_factory);

        var second = await client.CreateUserAsync("Second Member");

        using var added = await client.AddMemberAsync(
            scenario.Stokvel.Id,
            second.Id);

        Assert.True(
            added.IsSuccessStatusCode,
            await added.Content.ReadAsStringAsync());

        using var response = await client.GetAsync(
            Url(scenario.Stokvel.Id, scenario.Cycle.Id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var rotation = await response.Content
            .ReadFromJsonAsync<PayoutRotationResponse>();

        Assert.NotNull(rotation);
        Assert.Equal(2, rotation.Entries.Count);

        Assert.Equal(scenario.User.Id, rotation.Entries[0].MemberId);
        Assert.Equal(1, rotation.Entries[0].Position);
        Assert.True(rotation.Entries[0].IsNext);

        Assert.Equal(second.Id, rotation.Entries[1].MemberId);
        Assert.Equal(2, rotation.Entries[1].Position);
        Assert.Equal("upcoming", rotation.Entries[1].Status);
        Assert.False(rotation.Entries[1].IsNext);
    }

    [Fact]
    public async Task Outsider_cannot_read_rotation()
    {
        using var client = _factory.CreateClient();

        var scenario = await client.ArrangeMemberWithCycleAsync(_factory);
        var outsider = await client.CreateUserAsync("Rotation Outsider");

        client.UseTestUser(outsider.Id);

        using var response = await client.GetAsync(
            Url(scenario.Stokvel.Id, scenario.Cycle.Id));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_cycle_returns_404()
    {
        using var client = _factory.CreateClient();

        var scenario = await client.ArrangeMemberWithCycleAsync(_factory);

        using var response = await client.GetAsync(
            Url(scenario.Stokvel.Id, Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Completed_cycle_shows_paid_without_actionable_next()
    {
        using var client = _factory.CreateClient();

        var scenario = await client.ArrangeMemberWithCycleAsync(_factory);

        var second = await client.CreateUserAsync("Next Recipient");

        using var added = await client.AddMemberAsync(
            scenario.Stokvel.Id,
            second.Id);

        Assert.True(
            added.IsSuccessStatusCode,
            await added.Content.ReadAsStringAsync());

        using var processed = await client.PostAsync(
            $"/api/v1/stokvels/{scenario.Stokvel.Id}/cycles/{scenario.Cycle.Id}/payout",
            null);

        Assert.Equal(HttpStatusCode.Created, processed.StatusCode);

        using var response = await client.GetAsync(
            Url(scenario.Stokvel.Id, scenario.Cycle.Id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var rotation = await response.Content
            .ReadFromJsonAsync<PayoutRotationResponse>();

        Assert.NotNull(rotation);
        Assert.Equal(2, rotation.Entries.Count);

        var paid = rotation.Entries[0];
        Assert.Equal(scenario.User.Id, paid.MemberId);
        Assert.Equal("paid", paid.Status);
        Assert.Equal(50000L, paid.AmountCents);
        Assert.False(paid.IsNext);

        var upcoming = rotation.Entries[1];
        Assert.Equal(second.Id, upcoming.MemberId);
        Assert.Equal("upcoming", upcoming.Status);
        Assert.False(upcoming.IsNext);

        Assert.DoesNotContain(rotation.Entries, e => e.IsNext);
    }

    [Fact]
    public async Task Next_open_cycle_advances_to_unpaid_member()
    {
        using var client = _factory.CreateClient();

        var scenario = await client.ArrangeMemberWithCycleAsync(_factory);

        var second = await client.CreateUserAsync("Next Cycle Member");

        using var added = await client.AddMemberAsync(
            scenario.Stokvel.Id,
            second.Id);

        Assert.True(
            added.IsSuccessStatusCode,
            await added.Content.ReadAsStringAsync());

        using var processed = await client.PostAsync(
            $"/api/v1/stokvels/{scenario.Stokvel.Id}/cycles/{scenario.Cycle.Id}/payout",
            null);

        Assert.Equal(HttpStatusCode.Created, processed.StatusCode);

        var nextCycle = await client.CreateCycleAsync(
            scenario.Stokvel.Id,
            "2026-10");

        using var response = await client.GetAsync(
            Url(scenario.Stokvel.Id, nextCycle.Id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var rotation = await response.Content
            .ReadFromJsonAsync<PayoutRotationResponse>();

        Assert.NotNull(rotation);

        Assert.Equal("paid", rotation.Entries[0].Status);
        Assert.False(rotation.Entries[0].IsNext);

        Assert.Equal(second.Id, rotation.Entries[1].MemberId);
        Assert.Equal("next", rotation.Entries[1].Status);
        Assert.True(rotation.Entries[1].IsNext);
    }

    [Fact]
    public async Task Unauthenticated_caller_cannot_read_rotation()
    {
        using var client = _factory.CreateClient();

        var scenario = await client.ArrangeMemberWithCycleAsync(_factory);

        client.DefaultRequestHeaders.Add(
            "X-Test-Unauthenticated",
            "true");

        using var response = await client.GetAsync(
            Url(scenario.Stokvel.Id, scenario.Cycle.Id));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

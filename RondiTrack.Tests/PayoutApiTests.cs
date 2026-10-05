namespace RondiTrack.Tests;

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RondiTrack.Data;
using RondiTrack.Exceptions;
using RondiTrack.Models.Dtos;
using RondiTrack.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

public class PayoutApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public PayoutApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Processing_a_payout_returns_201_records_a_payout_and_marks_the_cycle_paid_out()
    {
        var s = await _client.ArrangeMemberWithCycleAsync();

        var response = await _client.PostAsync($"/api/stokvels/{s.Stokvel.Id}/cycles/{s.Cycle.Id}/payout", null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payout = await response.Content.ReadFromJsonAsync<PayoutResponse>();
        Assert.Equal(s.User.Id, payout!.RecipientUserId);
        Assert.Equal(s.Cycle.Id, payout.ContributionCycleId);

        await using var scope = _factory.Services.CreateAsyncScope();
        await using var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        var reloadedCycle = await db.ContributionCycles.FindAsync(s.Cycle.Id);
        Assert.Equal("PaidOut", reloadedCycle!.Status);
    }

    [Fact]
    public async Task Processing_a_payout_twice_for_the_same_cycle_is_rejected_the_second_time()
    {
        var s = await _client.ArrangeMemberWithCycleAsync();
        var first = await _client.PostAsync($"/api/stokvels/{s.Stokvel.Id}/cycles/{s.Cycle.Id}/payout", null);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await _client.PostAsync($"/api/stokvels/{s.Stokvel.Id}/cycles/{s.Cycle.Id}/payout", null);

        Assert.NotEqual(HttpStatusCode.Created, second.StatusCode);
    }

    // The rollback test: forces the operation to fail AFTER the recipient has
    // already been determined but BEFORE the transaction would commit both
    // writes, then re-queries the database directly — not the HTTP response —
    // to prove neither write was left behind.
    [Fact]
    public async Task A_failed_payout_leaves_no_partial_payout_or_cycle_status_change_behind()
    {
        var s = await _client.ArrangeMemberWithCycleAsync(); // one member, one cycle
        var firstPayout = await _client.PostAsync($"/api/stokvels/{s.Stokvel.Id}/cycles/{s.Cycle.Id}/payout", null);
        Assert.Equal(HttpStatusCode.Created, firstPayout.StatusCode); // the one member is now paid

        // A second cycle, same stokvel, same single member — who has already
        // been paid. ProcessAsync will find no eligible recipient and throw
        // AFTER starting its transaction, but BEFORE either write happens.
        var secondCycle = await _client.CreateCycleAsync(s.Stokvel.Id, "2026-10");

        await using var scope = _factory.Services.CreateAsyncScope();
        var payoutService = scope.ServiceProvider.GetRequiredService<PayoutService>();

        await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => payoutService.ProcessAsync(s.Stokvel.Id, secondCycle.Id));

        // Re-query fresh from the database — not the exception, not a status
        // code — to prove nothing partial was committed for this cycle.
        await using var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        var payoutsForSecondCycle = await db.Payouts
            .Where(p => p.ContributionCycleId == secondCycle.Id)
            .ToListAsync();
        var reloadedSecondCycle = await db.ContributionCycles.FindAsync(secondCycle.Id);

        Assert.Empty(payoutsForSecondCycle);
        Assert.Equal("Open", reloadedSecondCycle!.Status);
    }

    [Fact]
public async Task Updating_payout_with_stale_version_returns_409()
{
    var s = await _client.ArrangeMemberWithCycleAsync();

    // Create the payout.
    var createResponse = await _client.PostAsync(
        $"/api/stokvels/{s.Stokvel.Id}/cycles/{s.Cycle.Id}/payout",
        null);

    Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

    // HTTP GET obtains the original xmin/version token.
    var getResponse = await _client.GetAsync(
        $"/api/stokvels/{s.Stokvel.Id}/cycles/{s.Cycle.Id}/payout");

    Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

    var original = await getResponse.Content
        .ReadFromJsonAsync<PayoutResponse>();

    Assert.NotNull(original);
    Assert.True(original!.Version > 0);

    // First update uses the token we just obtained.
    var firstUpdate = await _client.PutAsJsonAsync(
        $"/api/stokvels/{s.Stokvel.Id}/cycles/{s.Cycle.Id}/payout",
        new UpdatePayoutRequest(750m, original.Version));

    Assert.Equal(HttpStatusCode.OK, firstUpdate.StatusCode);

    // Second update deliberately reuses the OLD token.
    var staleUpdate = await _client.PutAsJsonAsync(
        $"/api/stokvels/{s.Stokvel.Id}/cycles/{s.Cycle.Id}/payout",
        new UpdatePayoutRequest(900m, original.Version));

    Assert.Equal(HttpStatusCode.Conflict, staleUpdate.StatusCode);

    var problem = await staleUpdate.Content
        .ReadFromJsonAsync<ProblemDetails>();

    Assert.Equal(409, problem!.Status);
}
}
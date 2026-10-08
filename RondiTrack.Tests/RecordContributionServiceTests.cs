using RondiTrack.Domain;
namespace RondiTrack.Tests;

using RondiTrack.Data;
using RondiTrack.Exceptions;
using RondiTrack.Models;
using RondiTrack.Services;

// Unit tests for the rules that decide whether a contribution is recorded:
// membership, one payment per member per cycle, and the idempotency-key comparison.
public class RecordContributionServiceTests
{
    private readonly InMemoryStokvelStore _stokvels = new();
    private readonly InMemoryContributionStore _contributions = new();
    private readonly InMemoryContributionCycleStore _cycles = new();
    private readonly InMemoryIdempotencyStore _idempotency = new();
    private readonly RecordContributionService _service;

    public RecordContributionServiceTests()
    {
        _service = new RecordContributionService(_stokvels, _contributions, _cycles, _idempotency);
    }

    private record Scenario(Stokvel Stokvel, User User, ContributionCycle Cycle);

    // A stokvel with one member and one cycle: everything a valid contribution needs.
    private async Task<Scenario> ArrangeMemberWithCycleAsync()
    {
        var user = new User("Test Member", "0800000000");
        await _stokvels.AddUserAsync(user);

        var stokvel = new Stokvel("Test Stokvel", 500m);
        stokvel.AddMember(user.Id);
        await _stokvels.AddStokvelAsync(stokvel);

        var cycle = new ContributionCycle(stokvel.Id, "2026-09", 1000m);
        await _cycles.AddAsync(cycle);

        return new Scenario(stokvel, user, cycle);
    }

    private static ContributionRequest RequestFor(Scenario s, decimal amount = 500m) =>
        new(s.User.Id, amount, s.Cycle.Id);

    private async Task<int> ContributionCountAsync() =>
        (await _contributions.GetAllAsync()).Count();

    // ---------- Membership rule ----------

    [Fact]
    public async Task A_member_who_pays_a_real_cycle_gets_a_recorded_contribution()
    {
        var s = await ArrangeMemberWithCycleAsync();

        var response = await _service.ExecuteAsync(s.Stokvel.Id, "key-1", RequestFor(s));

        Assert.Equal(s.Stokvel.Id, response.StokvelId);
        Assert.Equal(s.User.Id, response.UserId);
        Assert.Equal(500m, response.Amount);
        Assert.Equal(s.Cycle.Id, response.ContributionCycleId);
        Assert.Equal(1, await ContributionCountAsync());
    }

    [Fact]
    public async Task A_user_who_is_not_a_member_of_the_stokvel_cannot_contribute()
    {
        var s = await ArrangeMemberWithCycleAsync();
        var outsider = new User("Not A Member", "0811111111");
        await _stokvels.AddUserAsync(outsider);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ExecuteAsync(s.Stokvel.Id, "key-1", new ContributionRequest(outsider.Id, 500m, s.Cycle.Id)));

        Assert.Equal(0, await ContributionCountAsync());
    }

    [Fact]
    public async Task A_user_that_does_not_exist_cannot_contribute()
    {
        var s = await ArrangeMemberWithCycleAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ExecuteAsync(s.Stokvel.Id, "key-1", new ContributionRequest(Guid.NewGuid(), 500m, s.Cycle.Id)));
    }

    [Fact]
    public async Task Contributing_to_a_stokvel_that_does_not_exist_throws_NotFoundException()
    {
        var s = await ArrangeMemberWithCycleAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ExecuteAsync(Guid.NewGuid(), "key-1", RequestFor(s)));
    }

    // ---------- Cycle rule ----------

    [Fact]
    public async Task Contributing_to_a_cycle_that_does_not_exist_throws_NotFoundException()
    {
        var s = await ArrangeMemberWithCycleAsync();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ExecuteAsync(s.Stokvel.Id, "key-1", new ContributionRequest(s.User.Id, 500m, Guid.NewGuid())));
    }

    [Fact]
    public async Task Contributing_to_a_cycle_that_belongs_to_a_different_stokvel_throws_NotFoundException()
    {
        var s = await ArrangeMemberWithCycleAsync();
        var otherStokvel = new Stokvel("Other Stokvel", 300m);
        await _stokvels.AddStokvelAsync(otherStokvel);
        var otherCycle = new ContributionCycle(otherStokvel.Id, "2026-09", 900m);
        await _cycles.AddAsync(otherCycle);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ExecuteAsync(s.Stokvel.Id, "key-1", new ContributionRequest(s.User.Id, 500m, otherCycle.Id)));

        Assert.Equal(0, await ContributionCountAsync());
    }

    // ---------- Duplicate-contribution rule ----------

    [Fact]
    public async Task A_member_who_already_paid_a_cycle_cannot_pay_it_again_with_a_new_key()
    {
        var s = await ArrangeMemberWithCycleAsync();
        await _service.ExecuteAsync(s.Stokvel.Id, "key-1", RequestFor(s));

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.ExecuteAsync(s.Stokvel.Id, "key-2", RequestFor(s)));

        Assert.Equal(1, await ContributionCountAsync());
    }

    [Fact]
    public async Task A_member_can_pay_two_different_cycles()
    {
        var s = await ArrangeMemberWithCycleAsync();
        var nextCycle = new ContributionCycle(s.Stokvel.Id, "2026-10", 1000m);
        await _cycles.AddAsync(nextCycle);

        await _service.ExecuteAsync(s.Stokvel.Id, "key-1", RequestFor(s));
        await _service.ExecuteAsync(s.Stokvel.Id, "key-2",
            new ContributionRequest(s.User.Id, 500m, nextCycle.Id));

        Assert.Equal(2, await ContributionCountAsync());
    }

    [Fact]
    public async Task Two_members_can_each_pay_the_same_cycle()
    {
        var s = await ArrangeMemberWithCycleAsync();
        var second = new User("Second Member", "0822222222");
        await _stokvels.AddUserAsync(second);
        s.Stokvel.AddMember(second.Id);

        await _service.ExecuteAsync(s.Stokvel.Id, "key-1", RequestFor(s));
        await _service.ExecuteAsync(s.Stokvel.Id, "key-2",
            new ContributionRequest(second.Id, 500m, s.Cycle.Id));

        Assert.Equal(2, await ContributionCountAsync());
    }

    // ---------- Idempotency-key comparison ----------

    [Fact]
    public async Task Repeating_a_request_with_the_same_key_and_same_body_returns_the_original_response()
    {
        var s = await ArrangeMemberWithCycleAsync();

        var first = await _service.ExecuteAsync(s.Stokvel.Id, "key-1", RequestFor(s));
        var second = await _service.ExecuteAsync(s.Stokvel.Id, "key-1", RequestFor(s));

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task Repeating_a_request_with_the_same_key_and_same_body_records_only_one_contribution()
    {
        var s = await ArrangeMemberWithCycleAsync();

        await _service.ExecuteAsync(s.Stokvel.Id, "key-1", RequestFor(s));
        await _service.ExecuteAsync(s.Stokvel.Id, "key-1", RequestFor(s));

        Assert.Equal(1, await ContributionCountAsync());
    }

    [Fact]
    public async Task Reusing_a_key_with_a_different_amount_throws_IdempotencyConflictException()
    {
        var s = await ArrangeMemberWithCycleAsync();
        await _service.ExecuteAsync(s.Stokvel.Id, "key-1", RequestFor(s, amount: 500m));

        await Assert.ThrowsAsync<IdempotencyConflictException>(() =>
            _service.ExecuteAsync(s.Stokvel.Id, "key-1", RequestFor(s, amount: 501m)));
    }

    [Fact]
    public async Task Reusing_a_key_with_a_different_cycle_throws_IdempotencyConflictException()
    {
        var s = await ArrangeMemberWithCycleAsync();
        var nextCycle = new ContributionCycle(s.Stokvel.Id, "2026-10", 1000m);
        await _cycles.AddAsync(nextCycle);
        await _service.ExecuteAsync(s.Stokvel.Id, "key-1", RequestFor(s));

        await Assert.ThrowsAsync<IdempotencyConflictException>(() =>
            _service.ExecuteAsync(s.Stokvel.Id, "key-1",
                new ContributionRequest(s.User.Id, 500m, nextCycle.Id)));
    }

    [Fact]
    public async Task A_rejected_key_reuse_records_nothing_new()
    {
        var s = await ArrangeMemberWithCycleAsync();
        await _service.ExecuteAsync(s.Stokvel.Id, "key-1", RequestFor(s, amount: 500m));

        await Assert.ThrowsAsync<IdempotencyConflictException>(() =>
            _service.ExecuteAsync(s.Stokvel.Id, "key-1", RequestFor(s, amount: 999m)));

        Assert.Equal(1, await ContributionCountAsync());
    }

    [Fact]
    public async Task A_failed_request_is_not_remembered_so_the_same_key_can_be_retried()
    {
        // The user exists but is not yet a member, so the first attempt fails with NotFound.
        var user = new User("Late Joiner", "0833333333");
        await _stokvels.AddUserAsync(user);
        var stokvel = new Stokvel("Test Stokvel", 500m);
        await _stokvels.AddStokvelAsync(stokvel);
        var cycle = new ContributionCycle(stokvel.Id, "2026-09", 1000m);
        await _cycles.AddAsync(cycle);
        var request = new ContributionRequest(user.Id, 500m, cycle.Id);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.ExecuteAsync(stokvel.Id, "key-1", request));

        // They join, then retry with the very same key and body: it must now succeed.
        stokvel.AddMember(user.Id);
        var response = await _service.ExecuteAsync(stokvel.Id, "key-1", request);

        Assert.Equal(user.Id, response.UserId);
        Assert.Equal(1, await ContributionCountAsync());
    }

    [Fact]
    public async Task A_replay_is_answered_from_memory_even_after_the_stokvel_is_deleted()
    {
        var s = await ArrangeMemberWithCycleAsync();
        var first = await _service.ExecuteAsync(s.Stokvel.Id, "key-1", RequestFor(s));
        await _stokvels.DeleteStokvelAsync(s.Stokvel.Id);

        var replay = await _service.ExecuteAsync(s.Stokvel.Id, "key-1", RequestFor(s));

        Assert.Equal(first, replay);
    }

    // ---------- Amount rule as seen by the service ----------

    [Fact]
    public async Task A_zero_amount_that_reaches_the_service_is_rejected_as_a_business_rule_violation()
    {
        // In the real pipeline FluentValidation stops this with a 400 first. This proves the
        // service still refuses it on its own if something ever lets it through.
        var s = await ArrangeMemberWithCycleAsync();

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            _service.ExecuteAsync(s.Stokvel.Id, "key-1", RequestFor(s, amount: 0m)));

        Assert.Equal(0, await ContributionCountAsync());
    }
}

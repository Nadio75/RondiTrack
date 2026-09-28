namespace RondiTrack.Tests;

using RondiTrack.Data;
using RondiTrack.Exceptions;
using RondiTrack.Models;
using RondiTrack.Services;

// Unit tests for the membership rule: a user can join a stokvel once, and only if both exist.
// No HTTP and no DI container: the real service runs against the real in-memory store.
public class StokvelMembershipServiceTests
{
    private readonly InMemoryStokvelStore _store = new();
    private readonly StokvelMembershipService _service;

    public StokvelMembershipServiceTests()
    {
        _service = new StokvelMembershipService(_store);
    }

    private async Task<(Stokvel stokvel, User user)> ArrangeStokvelAndUserAsync()
    {
        var user = new User("Test Member", "0800000000");
        await _store.AddUserAsync(user);

        var stokvel = new Stokvel("Test Stokvel", 500m);
        await _store.AddStokvelAsync(stokvel);

        return (stokvel, user);
    }

    [Fact]
    public async Task Adding_an_existing_user_to_an_existing_stokvel_makes_them_a_member()
    {
        var (stokvel, user) = await ArrangeStokvelAndUserAsync();

        await _service.AddMemberAsync(stokvel.Id, user.Id);

        Assert.Contains(user.Id, stokvel.MemberIds);
    }

    [Fact]
    public async Task Adding_the_same_user_twice_throws_ConflictException()
    {
        var (stokvel, user) = await ArrangeStokvelAndUserAsync();
        await _service.AddMemberAsync(stokvel.Id, user.Id);

        await Assert.ThrowsAsync<ConflictException>(
            () => _service.AddMemberAsync(stokvel.Id, user.Id));
    }

    [Fact]
    public async Task Adding_the_same_user_twice_leaves_them_listed_only_once()
    {
        var (stokvel, user) = await ArrangeStokvelAndUserAsync();
        await _service.AddMemberAsync(stokvel.Id, user.Id);

        await Assert.ThrowsAsync<ConflictException>(
            () => _service.AddMemberAsync(stokvel.Id, user.Id));

        Assert.Single(stokvel.MemberIds);
    }

    [Fact]
    public async Task Adding_a_user_to_a_stokvel_that_does_not_exist_throws_NotFoundException()
    {
        var user = new User("Test Member", "0800000000");
        await _store.AddUserAsync(user);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.AddMemberAsync(Guid.NewGuid(), user.Id));
    }

    [Fact]
    public async Task Adding_a_user_that_does_not_exist_throws_NotFoundException_and_adds_nobody()
    {
        var stokvel = new Stokvel("Test Stokvel", 500m);
        await _store.AddStokvelAsync(stokvel);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.AddMemberAsync(stokvel.Id, Guid.NewGuid()));

        Assert.Empty(stokvel.MemberIds);
    }
}
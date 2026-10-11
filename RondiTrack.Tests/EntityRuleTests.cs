using RondiTrack.Domain;
using RondiTrack.Models.Dtos;
using RondiTrack.Infrastructure;
namespace RondiTrack.Tests;

using RondiTrack.Models;

// Unit tests for the rules the entities enforce on themselves.
public class EntityRuleTests
{
    // ---------- Stokvel ----------

    [Fact]
    public void A_new_stokvel_starts_with_no_members()
    {
        var stokvel = new Stokvel("Test Stokvel", 500m);

        Assert.Empty(stokvel.MemberIds);
    }

    [Fact]
    public void Adding_the_same_member_twice_throws_InvalidOperationException()
    {
        var stokvel = new Stokvel("Test Stokvel", 500m);
        var userId = Guid.NewGuid();
        stokvel.AddMember(userId);

        Assert.Throws<InvalidOperationException>(() => stokvel.AddMember(userId));
    }

    [Fact]
    public void Removing_someone_who_is_not_a_member_throws_InvalidOperationException()
    {
        var stokvel = new Stokvel("Test Stokvel", 500m);

        Assert.Throws<InvalidOperationException>(() => stokvel.RemoveMember(Guid.NewGuid()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_stokvel_cannot_be_created_with_a_contribution_amount_of_zero_or_less(int amount)
    {
        Assert.Throws<ArgumentException>(() => new Stokvel("Test Stokvel", amount));
    }

    [Fact]
    public void A_stokvel_accepts_the_smallest_positive_contribution_amount()
    {
        var stokvel = new Stokvel("Test Stokvel", 0.01m);

        Assert.Equal(0.01m, stokvel.ContributionAmount);
    }

    // ---------- Contribution ----------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_contribution_cannot_have_an_amount_of_zero_or_less(int amount)
    {
        Assert.Throws<ArgumentException>(() =>
            new Contribution(Guid.NewGuid(), Guid.NewGuid(), amount, Guid.NewGuid()));
    }

    [Fact]
    public void A_contribution_must_reference_a_cycle()
    {
        Assert.Throws<ArgumentException>(() =>
            new Contribution(Guid.NewGuid(), Guid.NewGuid(), 500m, Guid.Empty));
    }

    // ---------- ContributionCycle ----------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void A_cycle_cannot_have_a_target_amount_of_zero_or_less(int target)
    {
        Assert.Throws<ArgumentException>(() =>
            new ContributionCycle(Guid.NewGuid(), "2026-09", target));
    }

    [Fact]
    public void A_cycle_cannot_be_created_without_a_period()
    {
        Assert.Throws<ArgumentException>(() =>
            new ContributionCycle(Guid.NewGuid(), " ", 1000m));
    }
}

using RondiTrack.Services;
using Xunit;

namespace RondiTrack.Tests;

public class PayoutRotationCalculatorTests
{
    private static readonly DateTime Joined =
        new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void OrdersMembersByJoiningDate()
    {
        var first = new RotationMember(
            Guid.NewGuid(), "Lerato", Joined);

        var second = new RotationMember(
            Guid.NewGuid(), "Sipho", Joined.AddDays(1));

        var result = PayoutRotationCalculator.Calculate(
            new[] { second, first },
            Array.Empty<RotationPayment>(),
            500m);

        Assert.Equal(first.UserId, result[0].MemberId);
        Assert.Equal(second.UserId, result[1].MemberId);
        Assert.Equal(1, result[0].Position);
        Assert.Equal(2, result[1].Position);
    }

    [Fact]
    public void UsesUserIdToBreakJoiningDateTies()
    {
        var firstId =
            Guid.Parse("00000000-0000-0000-0000-000000000001");

        var secondId =
            Guid.Parse("00000000-0000-0000-0000-000000000002");

        var result = PayoutRotationCalculator.Calculate(
            new[]
            {
                new RotationMember(secondId, "Sipho", Joined),
                new RotationMember(firstId, "Lerato", Joined)
            },
            Array.Empty<RotationPayment>(),
            500m);

        Assert.Equal(firstId, result[0].MemberId);
        Assert.Equal(secondId, result[1].MemberId);
    }

    [Fact]
    public void PaidMemberKeepsRecordedAmount()
    {
        var memberId = Guid.NewGuid();

        var result = PayoutRotationCalculator.Calculate(
            new[]
            {
                new RotationMember(memberId, "Lerato", Joined)
            },
            new[]
            {
                new RotationPayment(memberId, 650m)
            },
            500m);

        Assert.Single(result);
        Assert.Equal("paid", result[0].Status);
        Assert.Equal(65000L, result[0].AmountCents);
        Assert.False(result[0].IsNext);
    }

    [Fact]
    public void FirstUnpaidMemberIsNext()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var thirdId = Guid.NewGuid();

        var result = PayoutRotationCalculator.Calculate(
            new[]
            {
                new RotationMember(firstId, "Lerato", Joined),
                new RotationMember(
                    secondId, "Sipho", Joined.AddDays(1)),
                new RotationMember(
                    thirdId, "Thabo", Joined.AddDays(2))
            },
            new[]
            {
                new RotationPayment(firstId, 500m)
            },
            500m);

        Assert.Equal("paid", result[0].Status);
        Assert.Equal("next", result[1].Status);
        Assert.True(result[1].IsNext);
        Assert.Equal(50000L, result[1].AmountCents);
        Assert.Equal("upcoming", result[2].Status);
        Assert.False(result[2].IsNext);
        Assert.Single(result, e => e.IsNext);
    }

    [Fact]
    public void EmptyMembershipReturnsNoEntries()
    {
        var result = PayoutRotationCalculator.Calculate(
            Array.Empty<RotationMember>(),
            Array.Empty<RotationPayment>(),
            500m);

        Assert.Empty(result);
    }

    [Fact]
    public void FullyPaidRotationHasNoNextRecipient()
    {
        var memberId = Guid.NewGuid();

        var result = PayoutRotationCalculator.Calculate(
            new[]
            {
                new RotationMember(memberId, "Lerato", Joined)
            },
            new[]
            {
                new RotationPayment(memberId, 500m)
            },
            500m);

        Assert.Single(result);
        Assert.Equal("paid", result[0].Status);
        Assert.DoesNotContain(result, e => e.IsNext);
    }
}


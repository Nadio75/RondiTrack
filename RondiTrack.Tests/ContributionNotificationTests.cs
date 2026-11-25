using System;
using System.Linq;
using RondiTrack.Domain;
using RondiTrack.Services.Notifications;
using Xunit;

namespace RondiTrack.Tests;

public sealed class ContributionNotificationTests
{
    private static Contribution CreateContribution()
    {
        return new Contribution(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1234.56m,
            Guid.NewGuid());
    }

    [Fact]
    public void NewContribution_IsNotConfirmed()
    {
        var contribution = CreateContribution();

        Assert.False(contribution.IsConfirmed);
        Assert.Null(contribution.ConfirmedAtUtc);
        Assert.Null(contribution.ConfirmedByUserId);
    }

    [Fact]
    public void Confirm_RecordsTreasurerAndTimestamp()
    {
        var contribution = CreateContribution();
        var treasurerId = Guid.NewGuid();

        var before = DateTime.UtcNow;

        Assert.True(contribution.Confirm(treasurerId));
        Assert.True(contribution.IsConfirmed);

        Assert.Equal(
            treasurerId,
            contribution.ConfirmedByUserId);

        Assert.NotNull(contribution.ConfirmedAtUtc);

        Assert.InRange(
            contribution.ConfirmedAtUtc!.Value,
            before,
            DateTime.UtcNow);
    }

    [Fact]
    public void RepeatedConfirmation_DoesNotChangeOriginal()
    {
        var contribution = CreateContribution();
        var firstTreasurer = Guid.NewGuid();

        Assert.True(contribution.Confirm(firstTreasurer));

        var originalTimestamp = contribution.ConfirmedAtUtc;

        Assert.False(contribution.Confirm(Guid.NewGuid()));

        Assert.Equal(
            firstTreasurer,
            contribution.ConfirmedByUserId);

        Assert.Equal(
            originalTimestamp,
            contribution.ConfirmedAtUtc);
    }

    [Fact]
    public void Confirm_RejectsEmptyTreasurerId()
    {
        var contribution = CreateContribution();

        Assert.Throws<ArgumentException>(
            () => contribution.Confirm(Guid.Empty));

        Assert.False(contribution.IsConfirmed);
    }

    [Fact]
    public void Notification_ContainsOnlyAllowedFields()
    {
        var stokvelId = Guid.NewGuid();
        var cycleId = Guid.NewGuid();

        var notification = new PushNotification(
            stokvelId,
            cycleId);

        Assert.Equal("RondiTrack", notification.Title);

        Assert.Equal(
            "You have a new update.",
            notification.Body);

        Assert.Equal(3, notification.Data.Count);

        Assert.Equal(
            "contribution_confirmed",
            notification.Data["type"]);

        Assert.Equal(
            stokvelId.ToString("D"),
            notification.Data["stokvelId"]);

        Assert.Equal(
            cycleId.ToString("D"),
            notification.Data["cycleId"]);
    }

    [Fact]
    public void Notification_DoesNotExposePrivateInformation()
    {
        var notification = new PushNotification(
            Guid.NewGuid(),
            Guid.NewGuid());

        var content = string.Join(
            "|",
            new[]
            {
                notification.Title,
                notification.Body
            }.Concat(notification.Data.Keys)
             .Concat(notification.Data.Values));

        var forbidden = new[]
        {
            "123456",
            "1234.56",
            "R1,234.56",
            "R1 234,56",
            "R1\u00A0234,56",
            "Test Member",
            "member@example.com",
            "Private Savings Club",
            "Payment approved",
            "Payment rejected"
        };

        foreach (var value in forbidden)
        {
            Assert.False(content.Contains(
                value,
                StringComparison.OrdinalIgnoreCase));
        }
    }
}

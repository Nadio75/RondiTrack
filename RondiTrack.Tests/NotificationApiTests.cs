using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RondiTrack.Infrastructure;
using RondiTrack.Models.Dtos;
using RondiTrack.Services.Notifications;
using RondiTrack.Tests.TestSupport;

namespace RondiTrack.Tests;

[Collection("Postgres collection")]
public sealed class NotificationApiTests
{
    private readonly PostgresApiFactory _factory;

    public NotificationApiTests(PostgresApiFactory factory)
    {
        _factory = factory;
    }

    private sealed class RecordingSender : IPushNotificationSender
    {
        public ConcurrentQueue<(string InstallationId,
            PushNotification Notification)> Messages { get; } = new();

        public PushDeliveryResult Result { get; set; }
            = PushDeliveryResult.Delivered;

        public Task<PushDeliveryResult> SendAsync(
            string installationId,
            PushNotification notification,
            CancellationToken cancellationToken = default)
        {
            Messages.Enqueue((installationId, notification));
            return Task.FromResult(Result);
        }
    }

    private static Task<HttpResponseMessage> RegisterAsync(
        HttpClient client, string installationId)
    {
        return client.PostAsJsonAsync(
            "/api/v1/me/devices",
            new { installationId });
    }

    [Fact]
    public async Task Registration_RequiresAuthentication()
    {
        using var client = _factory.CreateClient();

        client.DefaultRequestHeaders.Add(
            "X-Test-Unauthenticated", "true");

        using var response = await RegisterAsync(
            client, Guid.NewGuid().ToString("N"));

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            response.StatusCode);
    }

    [Fact]
    public async Task SharedPhone_MovesOwnership_AndOldOwnerCannotDelete()
    {
        using var client = _factory.CreateClient();

        var first = await client.CreateUserAsync("First Owner");
        var second = await client.CreateUserAsync("Second Owner");
        var fid = Guid.NewGuid().ToString("N");

        client.UseTestUser(first.Id);

        using var firstRegistration =
            await RegisterAsync(client, fid);

        Assert.Equal(HttpStatusCode.NoContent,
            firstRegistration.StatusCode);

        client.UseTestUser(second.Id);

        using var secondRegistration =
            await RegisterAsync(client, fid);

        Assert.Equal(HttpStatusCode.NoContent,
            secondRegistration.StatusCode);

        client.UseTestUser(first.Id);

        using var oldOwnerDelete = await client.DeleteAsync(
            $"/api/v1/me/devices/{fid}");

        Assert.Equal(HttpStatusCode.NoContent,
            oldOwnerDelete.StatusCode);

        await using var scope =
            _factory.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<RondiTrackDbContext>();

        var device = await db.DeviceRegistrations
            .AsNoTracking()
            .SingleAsync(d => d.InstallationId == fid);

        Assert.Equal(second.Id, device.UserId);

        client.UseTestUser(second.Id);

        using var ownerDelete = await client.DeleteAsync(
            $"/api/v1/me/devices/{fid}");

        Assert.Equal(HttpStatusCode.NoContent,
            ownerDelete.StatusCode);

        Assert.False(await db.DeviceRegistrations
            .AnyAsync(d => d.InstallationId == fid));
    }

    [Theory]
    [InlineData(PushDeliveryResult.Delivered)]
    [InlineData(PushDeliveryResult.Failed)]
    [InlineData(PushDeliveryResult.InvalidInstallation)]
    public async Task Confirmation_NotifiesCorrectMember_OnlyOnce(
        PushDeliveryResult deliveryResult)
    {
        var sender = new RecordingSender
        {
            Result = deliveryResult
        };

        using var host = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPushNotificationSender>();

                services.AddSingleton<IPushNotificationSender>(
                    sender);
            });
        });

        using var client = host.CreateClient();

        var scenario =
            await client.ArrangeMemberWithCycleAsync(_factory);

        var memberId = scenario.User.Id;
        var stokvelId = scenario.Stokvel.Id;
        var cycleId = scenario.Cycle.Id;

        using var recorded = await client.PostContributionAsync(
            stokvelId,
            Guid.NewGuid().ToString(),
            ApiTestHelpers.ContributionBody(scenario, 1234.56m));

        Assert.Equal(HttpStatusCode.Created, recorded.StatusCode);

        var contribution = await recorded.Content
            .ReadFromJsonAsync<ContributionResponse>();

        Assert.NotNull(contribution);

        var memberFid = Guid.NewGuid().ToString("N");
        var treasurerFid = Guid.NewGuid().ToString("N");
        var transferredFid = Guid.NewGuid().ToString("N");

        client.UseTestUser(memberId);

        using var memberRegistration =
            await RegisterAsync(client, memberFid);

        Assert.Equal(HttpStatusCode.NoContent,
            memberRegistration.StatusCode);

        using var sharedRegistration =
            await RegisterAsync(client, transferredFid);

        Assert.Equal(HttpStatusCode.NoContent,
            sharedRegistration.StatusCode);

        var treasurer = await client.CreateUserAsync(
            "Notification Treasurer");

        await _factory.EnsureMembershipAsync(
            stokvelId, treasurer.Id, "Treasurer");

        var url = $"/api/v1/stokvels/{stokvelId}" +
            $"/contributions/{contribution.Id}/confirm";

        client.UseTestUser(memberId);

        using var forbidden = await client.PostAsync(url, null);

        Assert.Equal(HttpStatusCode.Forbidden,
            forbidden.StatusCode);

        Assert.Empty(sender.Messages);

        client.UseTestUser(treasurer.Id);

        using var treasurerRegistration =
            await RegisterAsync(client, treasurerFid);

        Assert.Equal(HttpStatusCode.NoContent,
            treasurerRegistration.StatusCode);

        using var transferredRegistration =
            await RegisterAsync(client, transferredFid);

        Assert.Equal(HttpStatusCode.NoContent,
            transferredRegistration.StatusCode);

        using var confirmed = await client.PostAsync(url, null);

        Assert.Equal(HttpStatusCode.NoContent,
            confirmed.StatusCode);

        var message = Assert.Single(sender.Messages);

        Assert.Equal(memberFid, message.InstallationId);

        Assert.Equal("RondiTrack", message.Notification.Title);

        Assert.Equal("You have a new update.",
            message.Notification.Body);

        Assert.Equal(3, message.Notification.Data.Count);

        Assert.Equal("contribution_confirmed",
            message.Notification.Data["type"]);

        Assert.Equal(stokvelId.ToString("D"),
            message.Notification.Data["stokvelId"]);

        Assert.Equal(cycleId.ToString("D"),
            message.Notification.Data["cycleId"]);

        using var repeated = await client.PostAsync(url, null);

        Assert.Equal(HttpStatusCode.NoContent,
            repeated.StatusCode);

        Assert.Single(sender.Messages);

        await using var scope =
            _factory.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<RondiTrackDbContext>();

        var saved = await db.Contributions
            .AsNoTracking()
            .SingleAsync(c => c.Id == contribution.Id);

        Assert.True(saved.IsConfirmed);
        Assert.Equal(treasurer.Id, saved.ConfirmedByUserId);

        var installationExists = await db.DeviceRegistrations
            .AnyAsync(d => d.InstallationId == memberFid);

        Assert.Equal(
            deliveryResult != PushDeliveryResult.InvalidInstallation,
            installationExists);

        Assert.True(await db.DeviceRegistrations
            .AnyAsync(d =>
                d.InstallationId == transferredFid &&
                d.UserId == treasurer.Id));
    }
}

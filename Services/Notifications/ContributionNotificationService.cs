using Microsoft.EntityFrameworkCore;
using RondiTrack.Infrastructure;

namespace RondiTrack.Services.Notifications;

public sealed class ContributionNotificationService
{
    private readonly RondiTrackDbContext _db;
    private readonly IPushNotificationSender _sender;
    private readonly ILogger<ContributionNotificationService> _logger;

    public ContributionNotificationService(
        RondiTrackDbContext db,
        IPushNotificationSender sender,
        ILogger<ContributionNotificationService> logger)
    {
        _db = db;
        _sender = sender;
        _logger = logger;
    }

    public async Task NotifyConfirmedAsync(
        Guid memberId,
        Guid stokvelId,
        Guid cycleId)
    {
        // Notification delivery is best effort.
        // It must never reverse a saved financial transaction.

        using var timeout = new CancellationTokenSource(
            TimeSpan.FromSeconds(10));

        var cancellationToken = timeout.Token;

        try
        {
            var devices = await _db.DeviceRegistrations
                .AsNoTracking()
                .Where(d => d.UserId == memberId)
                .ToListAsync(cancellationToken);

            var notification = new PushNotification(
                stokvelId,
                cycleId);

            foreach (var device in devices)
            {
                try
                {
                    // Recheck ownership before sending.
                    var stillOwned = await _db.DeviceRegistrations
                        .AsNoTracking()
                        .AnyAsync(d =>
                            d.InstallationId == device.InstallationId &&
                            d.UserId == memberId &&
                            d.UpdatedAtUtc == device.UpdatedAtUtc,
                            cancellationToken);

                    if (!stillOwned)
                        continue;

                    var result = await _sender.SendAsync(
                        device.InstallationId,
                        notification,
                        cancellationToken);

                    if (result ==
                        PushDeliveryResult.InvalidInstallation)
                    {
                        await _db.DeviceRegistrations
                            .Where(d =>
                                d.InstallationId == device.InstallationId &&
                                d.UserId == memberId &&
                                d.UpdatedAtUtc == device.UpdatedAtUtc)
                            .ExecuteDeleteAsync(cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception)
                {
                    _logger.LogWarning(
                        "A device notification could not be delivered.");
                }
            }
        }
        catch (Exception)
        {
            _logger.LogWarning(
                "Contribution notification processing did not complete.");
        }
    }
}

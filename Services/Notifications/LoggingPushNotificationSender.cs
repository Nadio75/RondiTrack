namespace RondiTrack.Services.Notifications;

public sealed class LoggingPushNotificationSender
    : IPushNotificationSender
{
    private readonly ILogger<LoggingPushNotificationSender> _logger;

    public LoggingPushNotificationSender(
        ILogger<LoggingPushNotificationSender> logger)
    {
        _logger = logger;
    }

    public Task<PushDeliveryResult> SendAsync(
        string installationId,
        PushNotification notification,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            "Push delivery skipped: Firebase is not configured.");

        return Task.FromResult(PushDeliveryResult.Disabled);
    }
}

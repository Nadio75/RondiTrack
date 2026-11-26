using FirebaseAdmin.Messaging;

namespace RondiTrack.Services.Notifications;

public sealed class FirebasePushNotificationSender
    : IPushNotificationSender
{
    private readonly FirebaseMessaging _messaging;
    private readonly ILogger<FirebasePushNotificationSender> _logger;

    public FirebasePushNotificationSender(
        FirebaseMessaging messaging,
        ILogger<FirebasePushNotificationSender> logger)
    {
        _messaging = messaging;
        _logger = logger;
    }

    public async Task<PushDeliveryResult> SendAsync(
        string installationId,
        PushNotification notification,
        CancellationToken cancellationToken = default)
    {
        var message = new Message
        {
            Fid = installationId,

            Notification = new Notification
            {
                Title = notification.Title,
                Body = notification.Body
            },

            Data = notification.Data,

            Android = new AndroidConfig
            {
                Notification = new AndroidNotification
                {
                    ChannelId = "ronditrack_updates"
                }
            }
        };

        try
        {
            await _messaging.SendAsync(
                message,
                cancellationToken);

            return PushDeliveryResult.Delivered;
        }
        catch (FirebaseMessagingException ex)
            when (ex.MessagingErrorCode ==
                MessagingErrorCode.Unregistered)
        {
            _logger.LogInformation(
                "Push installation is no longer registered.");

            return PushDeliveryResult.InvalidInstallation;
        }
        catch (FirebaseMessagingException)
        {
            _logger.LogWarning(
                "Firebase push delivery failed.");

            return PushDeliveryResult.Failed;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            _logger.LogWarning(
                "Unexpected push delivery failure.");

            return PushDeliveryResult.Failed;
        }
    }
}

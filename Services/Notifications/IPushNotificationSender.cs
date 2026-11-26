namespace RondiTrack.Services.Notifications;

public sealed record PushNotification(
    Guid StokvelId,
    Guid CycleId)
{
    public string Title => "RondiTrack";

    public string Body => "You have a new update.";

    public IReadOnlyDictionary<string, string> Data =>
        new Dictionary<string, string>
        {
            ["type"] = "contribution_confirmed",
            ["stokvelId"] = StokvelId.ToString("D"),
            ["cycleId"] = CycleId.ToString("D")
        };
}

public enum PushDeliveryResult
{
    Delivered,
    InvalidInstallation,
    Failed,
    Disabled
}

public interface IPushNotificationSender
{
    Task<PushDeliveryResult> SendAsync(
        string installationId,
        PushNotification notification,
        CancellationToken cancellationToken = default);
}

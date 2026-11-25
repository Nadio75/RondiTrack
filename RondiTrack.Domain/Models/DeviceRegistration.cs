namespace RondiTrack.Domain;

public sealed class DeviceRegistration
{
    public string InstallationId { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

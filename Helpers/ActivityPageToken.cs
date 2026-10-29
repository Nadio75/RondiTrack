namespace RondiTrack.Helpers;

using System.Text;
using System.Text.Json;

public static class ActivityPageToken
{
    private sealed record Payload(
        int Version,
        Guid UserId,
        DateTime OccurredAt,
        string ActivityType,
        Guid Id);

    public static string Encode(
        Guid userId,
        DateTime occurredAt,
        string activityType,
        Guid id)
    {
        var payload = new Payload(
            1,
            userId,
            occurredAt.ToUniversalTime(),
            activityType,
            id);

        var json = JsonSerializer.Serialize(payload);

        return Convert.ToBase64String(
            Encoding.UTF8.GetBytes(json));
    }

    public static bool TryDecode(
        string? token,
        Guid expectedUserId,
        out DateTime occurredAt,
        out string activityType,
        out Guid id)
    {
        occurredAt = default;
        activityType = "";
        id = default;

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            var json = Encoding.UTF8.GetString(
                Convert.FromBase64String(token));

            var payload = JsonSerializer.Deserialize<Payload>(json);

            if (payload is null ||
                payload.Version != 1 ||
                payload.UserId != expectedUserId ||
                payload.Id == Guid.Empty ||
                payload.OccurredAt.Kind != DateTimeKind.Utc ||
                payload.ActivityType is not ("contribution" or "payout"))
            {
                return false;
            }

            occurredAt = payload.OccurredAt;
            activityType = payload.ActivityType;
            id = payload.Id;

            return true;
        }
        catch (Exception ex) when (
            ex is FormatException ||
            ex is JsonException ||
            ex is ArgumentException)
        {
            return false;
        }
    }
}

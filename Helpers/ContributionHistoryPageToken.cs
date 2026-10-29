namespace RondiTrack.Helpers;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

/// <summary>
/// A pagination bookmark tied to one exact contribution-history filter.
/// </summary>
public static class ContributionHistoryPageToken
{
    private sealed record Payload(
        int Version,
        DateTime CreatedAt,
        Guid Id,
        string FilterHash);

    public static string CreateFilterHash(
        Guid stokvelId,
        Guid? memberId,
        DateTime? fromUtc,
        DateTime? toUtc,
        string sort = "newest")
    {
        static string FormatDate(DateTime? value) =>
            value?.ToUniversalTime().ToString(
                "O",
                CultureInfo.InvariantCulture) ?? "";

        var filter = string.Join("|",
            stokvelId.ToString("D"),
            memberId?.ToString("D") ?? "",
            FormatDate(fromUtc),
            FormatDate(toUtc),
            sort == "oldest" ? "createdAt-asc" : "createdAt-desc");

        var bytes = SHA256.HashData(
            Encoding.UTF8.GetBytes(filter));

        return Convert.ToHexString(bytes);
    }

    public static string Encode(
        DateTime createdAt,
        Guid id,
        string filterHash)
    {
        var payload = new Payload(
            1,
            createdAt.ToUniversalTime(),
            id,
            filterHash);

        var json = JsonSerializer.Serialize(payload);

        return Convert.ToBase64String(
            Encoding.UTF8.GetBytes(json));
    }

    public static bool TryDecode(
        string? token,
        string expectedFilterHash,
        out DateTime createdAt,
        out Guid id)
    {
        createdAt = default;
        id = default;

        if (string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            var json = Encoding.UTF8.GetString(
                Convert.FromBase64String(token));

            var payload = JsonSerializer.Deserialize<Payload>(json);

            if (payload is null ||
                payload.Version != 1 ||
                payload.Id == Guid.Empty ||
                payload.CreatedAt.Kind != DateTimeKind.Utc ||
                !string.Equals(
                    payload.FilterHash,
                    expectedFilterHash,
                    StringComparison.Ordinal))
            {
                return false;
            }

            createdAt = payload.CreatedAt;
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

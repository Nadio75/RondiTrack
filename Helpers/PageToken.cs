namespace RondiTrack.Helpers;

using System.Text;
using System.Text.Json;

public static class PageToken
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static string Encode(DateTime createdAt, Guid id)
    {
        var payload = new { createdAt = createdAt.ToUniversalTime().ToString("O"), id };
        var json = JsonSerializer.Serialize(payload, Options);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

   public static (DateTime CreatedAt, Guid Id)? Decode(string? token)
{
    if (string.IsNullOrWhiteSpace(token))
        return null;

    try
    {
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(token));
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Force UTC – PostgreSQL rejects Local/Unspecified for timestamptz
        var createdAt = DateTime.Parse(
            root.GetProperty("createdAt").GetString()!,
            null,
            System.Globalization.DateTimeStyles.RoundtripKind
        ).ToUniversalTime();

        var id = root.GetProperty("id").GetGuid();
        return (createdAt, id);
    }
    catch
    {
        return null;
    }
}
}
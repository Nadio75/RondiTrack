using System.Text.Json;
using RondiTrack.Models.Dtos;
using Xunit;

namespace RondiTrack.Tests;

public class PayoutRotationDtoTests
{
    [Fact]
    public void RotationResponse_UsesExpectedJsonContract()
    {
        var stokvelId = Guid.NewGuid();
        var cycleId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var response = new PayoutRotationResponse(
            stokvelId,
            cycleId,
            new[]
            {
                new PayoutRotationEntryResponse(
                    memberId,
                    "Lerato",
                    1,
                    50000,
                    null,
                    "next",
                    true)
            });

        var json = JsonSerializer.Serialize(
            response,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal(
            stokvelId.ToString(),
            root.GetProperty("stokvelId").GetString());

        Assert.Equal(
            cycleId.ToString(),
            root.GetProperty("cycleId").GetString());

        var entries = root.GetProperty("entries");

        Assert.Equal(1, entries.GetArrayLength());

        var entry = entries[0];

        Assert.Equal(
            memberId.ToString(),
            entry.GetProperty("memberId").GetString());

        Assert.Equal(
            "Lerato",
            entry.GetProperty("memberName").GetString());

        Assert.Equal(
            1,
            entry.GetProperty("position").GetInt32());

        Assert.Equal(
            50000,
            entry.GetProperty("amountCents").GetInt64());

        Assert.Equal(
            JsonValueKind.Null,
            entry.GetProperty("scheduledDate").ValueKind);

        Assert.Equal(
            "next",
            entry.GetProperty("status").GetString());

        Assert.True(entry.GetProperty("isNext").GetBoolean());
    }
}

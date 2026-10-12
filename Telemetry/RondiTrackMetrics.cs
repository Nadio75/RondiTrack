
using System.Diagnostics.Metrics;

namespace RondiTrack.Telemetry;

public static class RondiTrackMetrics
{
    public const string MeterName = "RondiTrack";

    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> ContributionsRecorded =
        Meter.CreateCounter<long>(
            "ronditrack.contributions.recorded",
            unit: "{contribution}",
            description: "New contributions successfully recorded.");

    public static readonly Counter<long> PayoutsProcessed =
        Meter.CreateCounter<long>(
            "ronditrack.payouts.processed",
            unit: "{attempt}",
            description: "Payout processing attempts by outcome.");

    public static readonly Counter<long> AuthorizationRefusals =
        Meter.CreateCounter<long>(
            "ronditrack.authorization.refusals",
            unit: "{refusal}",
            description: "Stokvel authorization refusals by outcome.");
}

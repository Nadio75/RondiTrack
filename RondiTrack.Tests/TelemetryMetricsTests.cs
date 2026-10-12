
using System.Diagnostics.Metrics;
using RondiTrack.Telemetry;

namespace RondiTrack.Tests;

public class TelemetryMetricsTests
{
    [Fact]
    public void ContributionCounter_RecordsMeasurement()
    {
        var measurements = new List<long>();

        using var listener = new MeterListener();

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == RondiTrackMetrics.MeterName &&
                instrument.Name == "ronditrack.contributions.recorded")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>(
            (instrument, measurement, tags, state) =>
            {
                if (instrument.Name == "ronditrack.contributions.recorded")
                {
                    measurements.Add(measurement);
                }
            });

        listener.Start();

        RondiTrackMetrics.ContributionsRecorded.Add(1);

        Assert.Single(measurements);
        Assert.Equal(1, measurements[0]);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("failure")]
    public void PayoutCounter_RecordsAllowedOutcome(string outcome)
    {
        var measurements = new List<(long Value, string Outcome)>();

        using var listener = new MeterListener();

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == RondiTrackMetrics.MeterName &&
                instrument.Name == "ronditrack.payouts.processed")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>(
            (instrument, measurement, tags, state) =>
            {
                if (instrument.Name != "ronditrack.payouts.processed")
                    return;

                var outcomeTag = "";

                foreach (var tag in tags)
                {
                    if (tag.Key == "outcome")
                    {
                        outcomeTag = tag.Value?.ToString() ?? "";
                    }
                }

                measurements.Add((measurement, outcomeTag));
            });

        listener.Start();

        RondiTrackMetrics.PayoutsProcessed.Add(
            1,
            new KeyValuePair<string, object?>("outcome", outcome));

        Assert.Single(measurements);
        Assert.Equal(1, measurements[0].Value);
        Assert.Equal(outcome, measurements[0].Outcome);
    }

    [Theory]
    [InlineData("not_found")]
    [InlineData("forbidden")]
    public void AuthorizationCounter_RecordsAllowedOutcome(string outcome)
    {
        var measurements = new List<(long Value, string Outcome)>();

        using var listener = new MeterListener();

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == RondiTrackMetrics.MeterName &&
                instrument.Name == "ronditrack.authorization.refusals")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>(
            (instrument, measurement, tags, state) =>
            {
                if (instrument.Name != "ronditrack.authorization.refusals")
                    return;

                var outcomeTag = "";

                foreach (var tag in tags)
                {
                    if (tag.Key == "outcome")
                    {
                        outcomeTag = tag.Value?.ToString() ?? "";
                    }
                }

                measurements.Add((measurement, outcomeTag));
            });

        listener.Start();

        RondiTrackMetrics.AuthorizationRefusals.Add(
            1,
            new KeyValuePair<string, object?>("outcome", outcome));

        Assert.Single(measurements);
        Assert.Equal(1, measurements[0].Value);
        Assert.Equal(outcome, measurements[0].Outcome);
    }
}

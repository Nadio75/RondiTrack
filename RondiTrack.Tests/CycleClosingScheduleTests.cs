
using RondiTrack.Domain;
using Xunit;

namespace RondiTrack.Tests;

public class CycleClosingScheduleTests
{
    [Fact]
    public void Closing_on_25th_becomes_due_at_22_utc()
    {
        DateTime result = CycleClosingSchedule.GetClosingInstantUtc(
            2026, 10, 25);

        Assert.Equal(
            new DateTime(2026, 10, 25, 22, 0, 0, DateTimeKind.Utc),
            result);
    }

    [Fact]
    public void Closing_day_31_uses_last_day_of_november()
    {
        DateTime result = CycleClosingSchedule.GetClosingInstantUtc(
            2026, 11, 31);

        Assert.Equal(
            new DateTime(2026, 11, 30, 22, 0, 0, DateTimeKind.Utc),
            result);
    }

    [Fact]
    public void Closing_day_31_uses_february_28_in_normal_year()
    {
        DateTime result = CycleClosingSchedule.GetClosingInstantUtc(
            2027, 2, 31);

        Assert.Equal(
            new DateTime(2027, 2, 28, 22, 0, 0, DateTimeKind.Utc),
            result);
    }

    [Fact]
    public void Closing_day_31_uses_february_29_in_leap_year()
    {
        DateTime result = CycleClosingSchedule.GetClosingInstantUtc(
            2028, 2, 31);

        Assert.Equal(
            new DateTime(2028, 2, 29, 22, 0, 0, DateTimeKind.Utc),
            result);
    }

    [Fact]
    public void Cycle_is_not_due_one_tick_before_closing()
    {
        DateTime closing = CycleClosingSchedule.GetClosingInstantUtc(
            2026, 10, 25);

        Assert.False(CycleClosingSchedule.IsDue(
            2026, 10, 25, closing.AddTicks(-1)));
    }

    [Fact]
    public void Cycle_is_due_at_exact_closing_instant()
    {
        DateTime closing = CycleClosingSchedule.GetClosingInstantUtc(
            2026, 10, 25);

        Assert.True(CycleClosingSchedule.IsDue(
            2026, 10, 25, closing));
    }

    [Fact]
    public void Invalid_closing_day_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CycleClosingSchedule.GetClosingInstantUtc(
                2026, 10, 32));
    }

    [Fact]
    public void Non_utc_current_time_is_rejected()
    {
        DateTime localTime = new DateTime(
            2026, 10, 25, 23, 0, 0, DateTimeKind.Local);

        Assert.Throws<ArgumentException>(() =>
            CycleClosingSchedule.IsDue(
                2026, 10, 25, localTime));
    }
}

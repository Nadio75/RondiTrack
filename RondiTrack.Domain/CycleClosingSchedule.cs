
namespace RondiTrack.Domain;

/// <summary>
/// Calculates when a monthly contribution cycle becomes due
/// for closing, using South African local time.
/// </summary>
public static class CycleClosingSchedule
{
    private static readonly TimeZoneInfo JohannesburgTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById(
            "Africa/Johannesburg");

    /// <summary>
    /// Returns the UTC instant when the specified monthly cycle
    /// becomes due for closing.
    ///
    /// For example, a closing day of 25 means contributions
    /// are accepted through the end of the 25th in Johannesburg.
    /// The cycle becomes due at midnight on the 26th.
    /// </summary>
    public static DateTime GetClosingInstantUtc(
        int year,
        int month,
        int closingDayOfMonth)
    {
        if (closingDayOfMonth < 1 || closingDayOfMonth > 31)
        {
            throw new ArgumentOutOfRangeException(
                nameof(closingDayOfMonth),
                "Closing day must be between 1 and 31.");
        }

        // A month may have fewer days than the configured day.
        int lastDayOfMonth = DateTime.DaysInMonth(year, month);

        int actualClosingDay = Math.Min(
            closingDayOfMonth,
            lastDayOfMonth);

        // Midnight at the start of the day AFTER closing.
        // Unspecified means this is a local wall-clock time,
        // not a UTC timestamp or the computer's local timezone.
        DateTime closingLocal = new DateTime(
            year,
            month,
            actualClosingDay,
            0,
            0,
            0,
            DateTimeKind.Unspecified).AddDays(1);

        return TimeZoneInfo.ConvertTimeToUtc(
            closingLocal,
            JohannesburgTimeZone);
    }

    /// <summary>
    /// Returns true when the cycle has reached or passed
    /// its scheduled closing instant.
    /// </summary>
    public static bool IsDue(
        int year,
        int month,
        int closingDayOfMonth,
        DateTime utcNow)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "The current time must be UTC.",
                nameof(utcNow));
        }

        DateTime closingInstantUtc = GetClosingInstantUtc(
            year,
            month,
            closingDayOfMonth);

        return utcNow >= closingInstantUtc;
    }
}

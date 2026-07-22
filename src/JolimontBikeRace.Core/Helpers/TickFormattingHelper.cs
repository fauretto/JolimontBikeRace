namespace JolimontBikeRace.Core.Helpers;

/// <summary>
/// Provides static helper methods that format durations and instants stored as .NET ticks (see
/// <see cref="DateTime.Ticks"/>) into the textual representations expected throughout the user
/// interface and the historical XML journal files.
/// </summary>
public static class TickFormattingHelper
{
    /// <summary>
    /// Formats a duration expressed in .NET ticks as an elapsed race time following the pattern
    /// "h:mm:ss", for example "1:23:45".
    /// </summary>
    /// <param name="elapsedTicks">The duration to format, expressed as .NET ticks.</param>
    /// <returns>The formatted elapsed time.</returns>
    public static string FormatElapsedTime(long elapsedTicks)
    {
        // A negative duration should never occur in practice, but it is clamped to zero so that
        // the formatted text always remains meaningful even if it does.
        var timeSpan = TimeSpan.FromTicks(Math.Max(0, elapsedTicks));
        var totalHours = (long)timeSpan.TotalHours;
        return $"{totalHours}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
    }

    /// <summary>
    /// Formats a gap duration expressed in .NET ticks as a leading-plus-sign textual
    /// representation, following the pattern "+h:mm:ss" when the gap reaches or exceeds one
    /// hour, or the shorter pattern "+m:ss" when the gap is below one hour.
    /// </summary>
    /// <param name="gapTicks">The gap duration to format, expressed as .NET ticks.</param>
    /// <returns>The formatted gap.</returns>
    public static string FormatGap(long gapTicks)
    {
        var timeSpan = TimeSpan.FromTicks(Math.Max(0, gapTicks));
        var totalHours = (long)timeSpan.TotalHours;

        // Below one hour, the leading hour component is trimmed away entirely so that the gap
        // reads as "+m:ss" instead of the more verbose "+0:mm:ss".
        if (totalHours <= 0)
        {
            var totalMinutes = (long)timeSpan.TotalMinutes;
            return $"+{totalMinutes}:{timeSpan.Seconds:D2}";
        }

        return $"+{totalHours}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
    }

    /// <summary>
    /// Converts a raw .NET tick count into a <see cref="DateTime"/> instance.
    /// </summary>
    /// <param name="ticks">The instant to convert, expressed as .NET ticks.</param>
    /// <returns>The corresponding date and time value.</returns>
    public static DateTime ToDateTime(long ticks)
    {
        return new DateTime(ticks);
    }

    /// <summary>
    /// Formats an instant expressed in .NET ticks as a time-of-day string following the pattern
    /// "HH:mm:ss", for example "14:23:07".
    /// </summary>
    /// <param name="ticks">The instant to format, expressed as .NET ticks.</param>
    /// <returns>The formatted time of day.</returns>
    public static string FormatTimeOfDay(long ticks)
    {
        return ToDateTime(ticks).ToString("HH:mm:ss");
    }
}

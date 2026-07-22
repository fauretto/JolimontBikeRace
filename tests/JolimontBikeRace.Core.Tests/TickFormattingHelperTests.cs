using JolimontBikeRace.Core.Helpers;

namespace JolimontBikeRace.Core.Tests;

/// <summary>
/// Contains unit tests for <see cref="TickFormattingHelper"/>.
/// </summary>
public class TickFormattingHelperTests
{
    [Fact]
    public void FormatElapsedTime_OneHourTwentyThreeMinutesFortyFiveSeconds_ReturnsHourMinuteSecondText()
    {
        var elapsedTicks = new TimeSpan(1, 23, 45).Ticks;

        var formattedElapsedTime = TickFormattingHelper.FormatElapsedTime(elapsedTicks);

        Assert.Equal("1:23:45", formattedElapsedTime);
    }

    [Fact]
    public void FormatGap_UnderOneHour_ReturnsShortMinuteSecondText()
    {
        var gapTicks = new TimeSpan(0, 1, 2).Ticks;

        var formattedGap = TickFormattingHelper.FormatGap(gapTicks);

        Assert.Equal("+1:02", formattedGap);
    }

    [Fact]
    public void FormatGap_OverOneHour_ReturnsHourMinuteSecondText()
    {
        var gapTicks = new TimeSpan(1, 2, 3).Ticks;

        var formattedGap = TickFormattingHelper.FormatGap(gapTicks);

        Assert.Equal("+1:02:03", formattedGap);
    }

    [Fact]
    public void FormatTimeOfDay_GivenTicks_ReturnsHourMinuteSecondOfDay()
    {
        var dateTime = new DateTime(2016, 8, 14, 14, 23, 7);

        var formattedTimeOfDay = TickFormattingHelper.FormatTimeOfDay(dateTime.Ticks);

        Assert.Equal("14:23:07", formattedTimeOfDay);
    }

    [Fact]
    public void ToDateTime_GivenTicks_ReturnsMatchingDateTime()
    {
        var originalDateTime = new DateTime(2016, 8, 14, 10, 1, 39);

        var convertedDateTime = TickFormattingHelper.ToDateTime(originalDateTime.Ticks);

        Assert.Equal(originalDateTime, convertedDateTime);
    }
}

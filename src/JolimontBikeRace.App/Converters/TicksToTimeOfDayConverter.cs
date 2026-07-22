using System.Globalization;
using System.Windows.Data;
using JolimontBikeRace.Core.Helpers;

namespace JolimontBikeRace.App.Converters;

/// <summary>
/// Converts a raw .NET tick count, as stored on race and crossing entities, into its formatted
/// time-of-day textual representation, for display purposes.
/// </summary>
public class TicksToTimeOfDayConverter : IValueConverter
{
    /// <summary>
    /// Converts a tick count into its formatted time-of-day text.
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is long ticks && ticks > 0)
        {
            return TickFormattingHelper.FormatTimeOfDay(ticks);
        }

        return string.Empty;
    }

    /// <summary>
    /// This converter only supports one-way conversion, so converting back is not supported.
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("Converting a formatted time of day back to ticks is not supported.");
    }
}

using System.Globalization;
using System.Windows.Data;

namespace JolimontBikeRace.App.Converters;

/// <summary>
/// Combines two bound values and returns true when they are equal, used to highlight the
/// navigation rail button that corresponds to the section view model currently displayed.
/// </summary>
public class ObjectEqualsConverter : IMultiValueConverter
{
    /// <summary>
    /// Returns true when the two supplied values are equal to each other.
    /// </summary>
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length != 2)
        {
            return false;
        }

        return Equals(values[0], values[1]);
    }

    /// <summary>
    /// This converter only supports one-way conversion, so converting back is not supported.
    /// </summary>
    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("Converting a boolean back to the original values is not supported.");
    }
}

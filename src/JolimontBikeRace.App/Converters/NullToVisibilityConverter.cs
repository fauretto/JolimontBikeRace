using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace JolimontBikeRace.App.Converters;

/// <summary>
/// Converts a value into a <see cref="Visibility"/>: <see cref="Visibility.Visible"/> when the
/// value is not null (and, for strings, not empty), <see cref="Visibility.Collapsed"/> otherwise.
/// Passing the string "Invert" as the converter parameter reverses this behavior.
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// Converts a value into a visibility.
    /// </summary>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isPresent = value switch
        {
            null => false,
            string text => !string.IsNullOrWhiteSpace(text),
            _ => true,
        };

        var shouldInvert = string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase);
        if (shouldInvert)
        {
            isPresent = !isPresent;
        }

        return isPresent ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// This converter only supports one-way conversion, so converting back is not supported.
    /// </summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("Converting a visibility back to a value is not supported.");
    }
}

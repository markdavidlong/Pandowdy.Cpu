using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Pandowdy.UI.Converters;

/// <summary>
/// Converts MHz values to formatted strings with variable precision based on magnitude.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Formatting Rules:</strong>
/// <list type="bullet">
/// <item>Values &lt; 10.0: Display with 3 decimal places (e.g., "1.023")</item>
/// <item>Values &gt;= 10.0: Display with 2 decimal places (e.g., "10.23")</item>
/// </list>
/// </para>
/// <para>
/// <strong>Usage:</strong> Bind to a double property representing MHz and use this converter
/// to get appropriately formatted display text.
/// </para>
/// </remarks>
public class MhzDisplayConverter : IValueConverter
{
    /// <summary>
    /// Singleton instance for use in XAML resources.
    /// </summary>
    public static readonly MhzDisplayConverter Instance = new();

    /// <summary>
    /// Converts a double MHz value to a formatted string.
    /// </summary>
    /// <param name="value">The MHz value (expected to be a double).</param>
    /// <param name="targetType">The target type (ignored).</param>
    /// <param name="parameter">Optional parameter (ignored).</param>
    /// <param name="culture">The culture for formatting.</param>
    /// <returns>Formatted string with appropriate decimal precision.</returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double mhz)
        {
            if (mhz < 0.0)
            {
                return "-.--";
            }
            // Use 2 decimal places for values >= 10, otherwise 3 decimal places
            return mhz >= 10.0 
                ? mhz.ToString("F1", culture) 
                : mhz.ToString("F2", culture);
        }
        
        return value?.ToString() ?? "-.--";
    }

    /// <summary>
    /// Not implemented - this is a one-way converter.
    /// </summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException("MhzDisplayConverter is one-way only.");
    }
}

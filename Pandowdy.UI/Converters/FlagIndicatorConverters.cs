// Copyright 2026 Mark D. Long
// Licensed under the Apache License, Version 2.0
// See LICENSE file for details

using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Pandowdy.UI.Converters;

/// <summary>
/// Converts a boolean flag to a background color for flag indicator display.
/// </summary>
public class FlagBackgroundConverter : IValueConverter
{
    /// <summary>
    /// Singleton instance for flag background (true = green, false = dark gray).
    /// </summary>
    public static readonly FlagBackgroundConverter Instance = new();

    private static readonly IBrush OnBrush = new SolidColorBrush(Color.Parse("#229922"));
    private static readonly IBrush OffBrush = new SolidColorBrush(Color.Parse("#333333"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool flag)
        {
            return flag ? OnBrush : OffBrush;
        }
        return OffBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a boolean flag to a foreground color for flag indicator display.
/// </summary>
public class FlagForegroundConverter : IValueConverter
{
    /// <summary>
    /// Singleton instance for flag foreground (true = black, false = dim gray).
    /// </summary>
    public static readonly FlagForegroundConverter Instance = new();

    private static readonly IBrush OnBrush = new SolidColorBrush(Color.Parse("#000000"));
    private static readonly IBrush OffBrush = new SolidColorBrush(Color.Parse("#999999"));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool flag)
        {
            return flag ? OnBrush : OffBrush;
        }
        return OffBrush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

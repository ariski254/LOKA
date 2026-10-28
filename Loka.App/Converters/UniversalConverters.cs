using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Loka.App.Converters;

/// <summary>
/// Universal converter supporting Boolean, Integer (count > 0), and Null/Empty checks,
/// with full support for "Inverse" or "inverse" parameter.
/// </summary>
[ValueConversion(typeof(object), typeof(Visibility))]
public class UniversalBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isVisible = false;

        if (value is bool b)
        {
            isVisible = b;
        }
        else if (value is int i)
        {
            isVisible = i > 0;
        }
        else if (value is long l)
        {
            isVisible = l > 0;
        }
        else if (value is string s)
        {
            isVisible = !string.IsNullOrWhiteSpace(s);
        }
        else if (value != null)
        {
            isVisible = true;
        }

        if (parameter is string paramStr && paramStr.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
        {
            isVisible = !isVisible;
        }

        return isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Visibility vis)
        {
            bool result = vis == Visibility.Visible;
            if (parameter is string paramStr && paramStr.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
            {
                result = !result;
            }
            return result;
        }
        return false;
    }
}

/// <summary>
/// Inverts a boolean value (useful for IsEnabled bindings).
/// </summary>
[ValueConversion(typeof(bool), typeof(bool))]
public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return !b;
        }
        return true;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return !b;
        }
        return false;
    }
}

/// <summary>
/// Converts a boolean/truthy value to inverted Visibility (True => Collapsed, False => Visible).
/// </summary>
[ValueConversion(typeof(object), typeof(Visibility))]
public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isVisible = false;

        if (value is bool b)
        {
            isVisible = b;
        }
        else if (value is int i)
        {
            isVisible = i > 0;
        }
        else if (value is long l)
        {
            isVisible = l > 0;
        }
        else if (value is string s)
        {
            isVisible = !string.IsNullOrWhiteSpace(s);
        }
        else if (value != null)
        {
            isVisible = true;
        }

        return isVisible ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Visibility vis)
        {
            return vis != Visibility.Visible;
        }
        return false;
    }
}

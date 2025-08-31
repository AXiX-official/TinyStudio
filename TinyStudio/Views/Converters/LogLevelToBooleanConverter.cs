using System;
using System.Globalization;
using Avalonia.Data.Converters;
using TinyStudio.Models;

namespace TinyStudio.Views.Converters;

public class LogLevelToBooleanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is LogLevel currentLevel && parameter is LogLevel itemLevel)
        {
            return currentLevel == itemLevel;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

using System;
using System.Globalization;
using System.Windows.Data;

namespace SPS.App.Views.Converters;

public sealed class SensorSelectionStateConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2)
        {
            return false;
        }

        var selected = values[0];
        var current = values[1];
        return ReferenceEquals(selected, current);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        if (targetTypes == null || targetTypes.Length == 0)
        {
            return Array.Empty<object>();
        }

        var values = new object[targetTypes.Length];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = Binding.DoNothing;
        }

        return values;
    }
}

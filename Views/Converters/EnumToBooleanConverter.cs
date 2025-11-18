using System;
using System.Globalization;
using System.Windows.Data;

namespace SPS.App.Views.Converters
{
    public class EnumToBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            string enumValue = value.ToString() ?? string.Empty;
            string targetValue = parameter.ToString() ?? string.Empty;
            return enumValue.Equals(targetValue, StringComparison.InvariantCultureIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null || targetType == null)
                return Binding.DoNothing;

            bool useValue = (bool)value;
            if (!useValue) return Binding.DoNothing;
            string targetValue = parameter.ToString() ?? string.Empty;
            return Enum.Parse(targetType, targetValue);
        }
    }
}

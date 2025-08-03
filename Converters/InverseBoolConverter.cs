// Converters/InverseBoolConverter.cs
using System.Globalization;

namespace LoQA.Converters
{
    public class InverseBoolConverter : IValueConverter
    {
        // Add nullable annotations '?' to match the interface
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return !b;
            }
            return false;
        }

        // Add nullable annotations '?' to match the interface
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b)
            {
                return !b;
            }
            return false;
        }
    }
}
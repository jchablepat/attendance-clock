using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace VTACheckClock.Converters
{
    public class CustomConverter
    {

    }

    public class IsDirtyColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // Si el valor es nulo o cadena vacía, devuelve un color de fondo (por ejemplo, rojo claro)
            if (value == null || (value is string strValue && string.IsNullOrWhiteSpace(strValue)))
            {
                return new SolidColorBrush(Colors.LightPink);
            }

            return new SolidColorBrush(Colors.Transparent);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

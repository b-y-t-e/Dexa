using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Dexa.Converters;

public class BooleanToDoubleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var parameterStr = parameter?.ToString() ?? "";
        var values = parameterStr.Split('_', ';', ' ');


        if (value is bool b && b)
        {
            if (values?.Length > 0)
                return double.Parse(values[0], CultureInfo.InvariantCulture);

            return 1.0;
        }

        if (values?.Length > 1)
            return double.Parse(values[1], CultureInfo.InvariantCulture);
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

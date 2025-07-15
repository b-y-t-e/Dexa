using System.Globalization;
using System.Windows.Data;
using Color = System.Windows.Media.Color;

namespace Dexa.Converters;

public class BooleanToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var parameterStr = (parameter?.ToString() ?? "").Replace("__", " ");
        var values = parameterStr.Split(new[] { '_', ';' }, 2);


        if (value is bool b && b)
        {
            if (values?.Length > 0)
                return values[0];

            return "true";
        }

        if (values?.Length > 1)
            return values[1];
        return "false";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

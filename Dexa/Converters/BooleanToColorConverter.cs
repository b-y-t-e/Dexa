using System;
using System.Globalization;
using System.Windows.Data;

namespace Dexa.Converters
{
    public class BooleanToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var parameterStr = parameter?.ToString() ?? "";
            var values = parameterStr.Split(new[] { '_', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);

            var colorStrForTrue = "Black"; // Default for true
            var colorStrForFalse = "Black"; // Default for false

            if (values.Length > 0)
            {
                colorStrForTrue = values[0];
            }
            if (values.Length > 1)
            {
                colorStrForFalse = values[1];
            }

            var targetColorStr = (value is bool b && b) ? colorStrForTrue : colorStrForFalse;

            return ParseColor(targetColorStr);
        }

        private System.Windows.Media.Brush ParseColor(string colorStr)
        {
            if (string.IsNullOrWhiteSpace(colorStr))
                return System.Windows.Media.Brushes.Black;

            // HEX color (e.g., #FF00FF, #80FF00FF)
            if (colorStr.StartsWith("#"))
            {
                try
                {
                    var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorStr);
                    return new System.Windows.Media.SolidColorBrush(color);
                }
                catch (FormatException)
                {
                    return System.Windows.Media.Brushes.Black; // Fallback for invalid hex
                }
            }

            // RGB/ARGB color (e.g., 255.0.255, 128.255.0.255)
            if (colorStr.Contains("."))
            {
                var parts = colorStr.Split('.');
                try
                {
                    if (parts.Length == 3) // RRR.GGG.BBB
                    {
                        var r = byte.Parse(parts[0]);
                        var g = byte.Parse(parts[1]);
                        var b = byte.Parse(parts[2]);
                        return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
                    }
                    if (parts.Length == 4) // AAA.RRR.GGG.BBB
                    {
                        var a = byte.Parse(parts[0]);
                        var r = byte.Parse(parts[1]);
                        var g = byte.Parse(parts[2]);
                        var b = byte.Parse(parts[3]);
                        return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(a, r, g, b));
                    }
                }
                catch (Exception) // Catches FormatException, OverflowException etc.
                {
                    return System.Windows.Media.Brushes.Black; // Fallback for invalid RGB/ARGB
                }
            }

            // Named color (e.g., Red, Green)
            try
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorStr);
                return new System.Windows.Media.SolidColorBrush(color);
            }
            catch (FormatException)
            {
                // Fallback for invalid color name
                return System.Windows.Media.Brushes.Black;
            }
            return System.Windows.Media.Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
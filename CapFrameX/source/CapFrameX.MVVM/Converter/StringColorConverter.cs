using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace CapFrameX.MVVM.Converter
{
    public class StringColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (string.IsNullOrWhiteSpace((string)value))
                return (Color?)Color.FromArgb(0, 0, 0, 0);

            return (Color?)ColorConverter.ConvertFromString("#" + value.ToString());
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return "000000";

            var color = (Color?)value;
            return color.Value.R.ToString("X2") + color.Value.G.ToString("X2") + color.Value.B.ToString("X2");
        }
    }
}

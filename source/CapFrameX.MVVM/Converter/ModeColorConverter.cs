using System;
using System.Globalization;
using System.Windows.Data;

namespace CapFrameX.MVVM.Converter
{
    public class ModeColorConverter : IValueConverter
    {
        public string ActiveColor { get; set; } = "Green";

        public string InactiveColor { get; set; } = "Red";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is bool))
                return InactiveColor;

            bool active = (bool)value;

            if (active)
                return ActiveColor;
            else
                return InactiveColor;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("Cannot convert back");
        }
    }
}

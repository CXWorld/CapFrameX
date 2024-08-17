using CapFrameX.Contracts.PMD;
using System;
using System.Globalization;
using System.Windows.Data;

namespace CapFrameX.MVVM.Converter
{
    public class PmdStatusColorConverter : IValueConverter
    {
        public string ReadyColor { get; set; } = "Orange";

        public string ErrorColor { get; set; } = "OrangeRed";

        public string ConnectedColor { get; set; } = "LimeGreen";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is EPmdDriverStatus))
                return ErrorColor;

            EPmdDriverStatus status = (EPmdDriverStatus)value;

            var color = string.Empty;
            switch (status)
            {
                case EPmdDriverStatus.Ready:
                    color = ReadyColor;
                    break;
                case EPmdDriverStatus.Connected:
                    color = ConnectedColor;
                    break;
                case EPmdDriverStatus.Error:
                    color = ErrorColor;
                    break;
            }

            return color;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("Cannot convert back");
        }
    }
}
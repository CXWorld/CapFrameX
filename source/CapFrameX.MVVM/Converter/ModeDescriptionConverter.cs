using System;
using System.Globalization;
using System.Windows.Data;

namespace CapFrameX.MVVM.Converter
{
    public class ModeDescriptionConverter : IValueConverter
    {
        public char Seperator { get; set; } = '|';

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string description = "NaN";

            if (!(value is bool))
                return description;

            bool active = (bool)value;

            string parameterString = parameter as string;
            if (!string.IsNullOrEmpty(parameterString))
            {
                string[] parameters = parameterString.Split(Seperator);

                if (parameters.Length == 2)
                {
                    if (active)
                        description = parameters[0];
                    else
                        description = parameters[1];
                }
            }

            return description;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("Cannot convert back");
        }
    }
}

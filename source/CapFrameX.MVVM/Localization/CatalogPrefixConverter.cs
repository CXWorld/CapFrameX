using System;
using System.Globalization;
using System.Windows.Data;
using CapFrameX.Contracts.Localization;

namespace CapFrameX.MVVM.Converter
{
    public class CatalogPrefixConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;
            var prefix = parameter as string ?? string.Empty;
            return CxLang.T(prefix + value);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}

using System;
using System.Globalization;
using System.Windows.Data;
using CapFrameX.Contracts.Localization;

namespace CapFrameX.MVVM.Converter
{
    /// <summary>Translates sensor labels in the interface language, preserving the stored name.</summary>
    public class UiLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => CxLang.Instance.TranslateUiText(value as string ?? value?.ToString() ?? string.Empty);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}

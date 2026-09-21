using System;
using System.Globalization;
using System.Windows.Data;
using CapFrameX.Contracts.Localization;

namespace CapFrameX.MVVM.Converter
{
    /// <summary>Translates an overlay or sensor label without changing the stored English name.</summary>
    public class OverlayLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => CxLang.Instance.TranslateOverlay(value as string ?? string.Empty);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}

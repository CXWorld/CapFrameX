using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using CapFrameX.Contracts.Localization;

namespace CapFrameX.MVVM.Localization
{
    /// <summary>
    /// Binds a control to the current interface translation of an English string.
    /// Usage: Text="{loc:Tr 'Running processes'}"
    /// </summary>
    public class TrExtension : MarkupExtension
    {
        public TrExtension()
        {
        }

        public TrExtension(string text)
        {
            Text = text;
        }

        public string Text { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var binding = new Binding(nameof(CxLang.UiLanguage))
            {
                Source = CxLang.Instance,
                Mode = BindingMode.OneWay,
                Converter = TranslateConverter.Instance,
                ConverterParameter = Text ?? string.Empty
            };

            // Only a real element can host the binding. Inside a template the target does not
            // exist yet. A Binding object returned from here is written into the template as
            // the property value and the page fails to load, so fall back to the translated text.
            if (serviceProvider?.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget target
                && target.TargetObject is DependencyObject
                && target.TargetProperty is DependencyProperty)
                return binding.ProvideValue(serviceProvider);

            return CxLang.T(Text ?? string.Empty);
        }

        private sealed class TranslateConverter : IValueConverter
        {
            public static readonly TranslateConverter Instance = new TranslateConverter();

            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
                => CxLang.T(parameter as string);

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
                => throw new NotSupportedException();
        }
    }
}

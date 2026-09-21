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

            // A normal element can take the binding immediately. Inside a DataTemplate the
            // target is not created yet: return the Binding itself so WPF applies it when the
            // visual is built. Returning this extension there makes the template fail to load
            // and the page stays blank.
            if (serviceProvider?.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget target
                && target.TargetObject is DependencyObject
                && target.TargetProperty is DependencyProperty)
                return binding.ProvideValue(serviceProvider);

            return binding;
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

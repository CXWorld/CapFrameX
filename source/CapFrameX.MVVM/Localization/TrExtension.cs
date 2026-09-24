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
            var target = serviceProvider?.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;

            // When evaluated inside a Style or ControlTemplate Setter, WPF passes an internal
            // SharedDp object as the target. Returning this defers evaluation until the template
            // is instantiated on a real visual element, allowing live language switching to work
            // for templated controls.
            if (target?.TargetObject != null && target.TargetObject.GetType().Name == "SharedDp")
                return this;

            var binding = new Binding(nameof(CxLang.UiLanguage))
            {
                Source = CxLang.Instance,
                Mode = BindingMode.OneWay,
                Converter = TranslateConverter.Instance,
                ConverterParameter = Text ?? string.Empty
            };

            if (target?.TargetObject is DependencyObject && target.TargetProperty is DependencyProperty)
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

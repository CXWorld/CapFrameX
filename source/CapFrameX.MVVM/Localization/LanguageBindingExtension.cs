using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using CapFrameX.Contracts.Localization;

namespace CapFrameX.MVVM.Localization
{
    /// <summary>Re-runs a display converter when its language changes, even if the value is unchanged.</summary>
    public class LanguageBindingExtension : MarkupExtension
    {
        private MultiBinding _binding;

        public LanguageBindingExtension(Binding value)
        {
            Value = value;
        }

        [ConstructorArgument("value")]
        public Binding Value { get; set; }

        public bool Overlay { get; set; }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var target = serviceProvider?.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;
            if (target?.TargetObject?.GetType().Name == "SharedDp")
                return this;

            if (_binding == null)
            {
                _binding = new MultiBinding
                {
                    Mode = BindingMode.OneWay,
                    Converter = new DisplayConverter(Value.Converter),
                    ConverterParameter = Value.ConverterParameter,
                    ConverterCulture = Value.ConverterCulture,
                    StringFormat = Value.StringFormat,
                    TargetNullValue = Value.TargetNullValue,
                    FallbackValue = Value.FallbackValue
                };
                Value.Converter = null;
                Value.ConverterParameter = null;
                Value.StringFormat = null;
                Value.TargetNullValue = DependencyProperty.UnsetValue;
                Value.FallbackValue = DependencyProperty.UnsetValue;
                Value.Mode = BindingMode.OneWay;
                _binding.Bindings.Add(Value);
                _binding.Bindings.Add(new Binding(Overlay ? nameof(CxLang.OverlayLanguage) : nameof(CxLang.UiLanguage))
                {
                    Source = CxLang.Instance,
                    Mode = BindingMode.OneWay
                });
            }
            return _binding.ProvideValue(serviceProvider);
        }

        private sealed class DisplayConverter : IMultiValueConverter
        {
            private readonly IValueConverter _converter;

            public DisplayConverter(IValueConverter converter)
            {
                _converter = converter;
            }

            public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
            {
                var value = values[0];
                if (value == DependencyProperty.UnsetValue)
                    return value;
                return _converter == null ? value : _converter.Convert(value, targetType, parameter, culture);
            }

            public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
                => throw new NotSupportedException();
        }
    }
}

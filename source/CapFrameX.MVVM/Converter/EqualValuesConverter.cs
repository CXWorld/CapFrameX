using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace CapFrameX.MVVM.Converter
{
    /// <summary>True when every bound value is set and equal to the first, such as a list item's ID and the ID of the item in use.</summary>
    public class EqualValuesConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
            => values != null && values.Length > 1 && values[0] != null && values[0] != DependencyProperty.UnsetValue
                && values.Skip(1).All(value => Equals(values[0], value));

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}

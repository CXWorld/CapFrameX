using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CapFrameX.MVVM.Converter
{
	[ValueConversion(typeof(object), typeof(Visibility))]
	public sealed class NullToVisibilityConverter : IValueConverter
	{
		public Visibility NullValue { get; set; }
		public Visibility NotNullValue { get; set; }

		public NullToVisibilityConverter()
		{
			// set defaults
			NullValue = Visibility.Collapsed;
			NotNullValue = Visibility.Visible;
		}

		public object Convert(object value, Type targetType,
			object parameter, CultureInfo culture)
		{
			return value == null ? NullValue : NotNullValue;
		}

		public object ConvertBack(object value, Type targetType,
			object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}

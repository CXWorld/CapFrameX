using System;
using System.Globalization;
using System.Windows.Data;

namespace CapFrameX.MVVM.Converter
{
	[ValueConversion(typeof(bool), typeof(bool))]
	public class NegateBoolConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			if (!(value is bool))
				return null;
			return !(bool)value;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return null;
		}
	}
}

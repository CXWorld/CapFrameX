using LiveCharts;
using System;
using System.Globalization;
using System.Windows.Data;

namespace CapFrameX.MVVM.Converter
{
	public class ZoomingModeConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			switch ((ZoomingOptions)value)
			{
				case ZoomingOptions.None:
					return "None";
				case ZoomingOptions.X:
					return "X";
				case ZoomingOptions.Y:
					return "Y";
				case ZoomingOptions.Xy:
					return "XY";
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			throw new NotImplementedException();
		}
	}
}

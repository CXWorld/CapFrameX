using CapFrameX.Extensions;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace CapFrameX.MVVM.Converter
{
	public class SelectedAreaWidthRatioConverter : IMultiValueConverter
	{
		/// <summary>
		/// Converts a value. 
		/// </summary>
		/// <returns>
		/// A converted value. If the method returns null, the valid null value is used.
		/// </returns>
		/// <param name="values">The values produced by the binding source.</param><param name="targetType">The type of the binding target property.</param><param name="parameter">The converter parameter to use.</param><param name="culture">The culture to use in the converter.</param>
		public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
		{
			try
			{
				var parentWidth = (double)values[0];
				var fullScale = (double)values[1];
				var selection = (double)values[2];

				var res = parentWidth * (selection / fullScale);

				if (targetType == typeof(double))
					return res;
				if (targetType == typeof(Thickness))
					return (string)parameter == "Left" ? new Thickness(res, 0, 0, 0) : new Thickness(0, 0, res, 0);

				throw new NotSupportedException();
			}
			catch
			{
				return targetType.GetDefaultValue();
			}
		}

		/// <summary>
		/// Converts a value. 
		/// </summary>
		/// <returns>
		/// A converted value. If the method returns null, the valid null value is used.
		/// </returns>
		/// <param name="value">The value that is produced by the binding target.</param><param name="targetTypes">The type to convert to.</param><param name="parameter">The converter parameter to use.</param><param name="culture">The culture to use in the converter.</param>
		public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}

	}
}

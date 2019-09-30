using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace CapFrameX.MVVM.Converter
{
	public class PlusHalfDifferenceConverter : IMultiValueConverter
	{
		#region Implementation of IMultiValueConverter

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
				var val = (double)values[0];
				var total = (double)values[1];
				var selectable = (double)values[2];

				return val - (total - selectable) / 2;
			}
			catch
			{
				return values.FirstOrDefault();
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

		#endregion
	}
}

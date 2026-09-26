using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using CapFrameX.Contracts.Localization;

namespace CapFrameX.MVVM.Converter
{
	/// <summary>
	/// Returns the last path segment (leaf folder name) of a path so a
	/// breadcrumb chip can show "Captures" instead of the full path.
	/// </summary>
	[ValueConversion(typeof(string), typeof(string))]
	public class PathLeafConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			var path = value as string;
			if (string.IsNullOrWhiteSpace(path))
				return string.Empty;

			var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			var leaf = Path.GetFileName(trimmed);
			if (string.Equals(leaf, "Captures", StringComparison.OrdinalIgnoreCase))
				return CxLang.T("ControlView_Captures");

			return string.IsNullOrEmpty(leaf) ? trimmed : leaf;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return Binding.DoNothing;
		}
	}
}

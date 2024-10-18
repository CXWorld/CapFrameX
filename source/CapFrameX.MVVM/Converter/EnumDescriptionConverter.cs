using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows.Data;

namespace CapFrameX.MVVM.Converter
{
	public class EnumDescriptionConverter : IValueConverter
	{
		private string GetEnumDescription(Enum enumObj)
		{
			FieldInfo fieldInfo = enumObj.GetType().GetField(enumObj.ToString());

			object[] attribArray = fieldInfo.GetCustomAttributes(false);

			if (attribArray.Length == 0)
			{
				return enumObj.ToString();
			}
			else
			{
				DescriptionAttribute attrib = attribArray[0] as DescriptionAttribute;
				return attrib.Description;
			}
		}

		object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			try
			{
                if (!(value is Enum myEnum))
                    return string.Empty;

                string description = GetEnumDescription(myEnum);
				return description;
			}
			catch { return string.Empty; }
		}

		object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return string.Empty;
		}
	}
}

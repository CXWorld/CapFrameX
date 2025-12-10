using CapFrameX.Extensions.NetStandard.Attributes;
using System;
using System.Globalization;
using System.Reflection;
using System.Windows.Data;

namespace CapFrameX.MVVM.Converter
{
    public class EnumShortDescriptionConverter : IValueConverter
    {
        object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (!(value is Enum myEnum))
                    return string.Empty;

                string description = GetEnumShortDescription(myEnum);
                return description;
            }
            catch { return string.Empty; }
        }

        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.Empty;
        }

        private string GetEnumShortDescription(Enum enumObj)
        {
            FieldInfo fieldInfo = enumObj.GetType().GetField(enumObj.ToString());

            object[] attribArray = fieldInfo.GetCustomAttributes(false);

            if (attribArray.Length == 0)
            {
                return enumObj.ToString();
            }
            else
            {
                var attrib = attribArray[1] as ShortDescriptionAttribute;
                return attrib.Description;
            }
        }
    }
}

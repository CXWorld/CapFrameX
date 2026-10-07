using CapFrameX.Contracts.Localization;
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

                return CxLang.TranslateEnum(myEnum, useShortDescription: true);
            }
            catch { return string.Empty; }
        }

        object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.Empty;
        }
    }
}

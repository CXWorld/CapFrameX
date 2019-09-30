using System;

namespace CapFrameX.Extensions
{
	public static class EnumExtensions
	{
		public static string ConvertToString(this Enum eff)
		{
			return Enum.GetName(eff.GetType(), eff);
		}

		public static EnumType ConverToEnum<EnumType>(this string enumValue)
		{
			return (EnumType)Enum.Parse(typeof(EnumType), enumValue);
		}
	}
}

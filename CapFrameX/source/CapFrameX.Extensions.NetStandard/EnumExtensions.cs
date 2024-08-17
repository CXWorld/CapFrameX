﻿using CapFrameX.Extensions.NetStandard.Attributes;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace CapFrameX.Extensions.NetStandard
{
    public static class EnumExtensions
    {
        public static TAttribute GetAttribute<TAttribute>(this Enum value)
            where TAttribute : Attribute
        {
            var type = value.GetType();
            var name = Enum.GetName(type, value);
            var attributes = type.GetField(name).GetCustomAttributes(false);
            return attributes.OfType<TAttribute>().Where(a => a.GetType() == typeof(TAttribute)).SingleOrDefault();
        }

		public static string ConvertToString(this Enum eff)
		{
			return Enum.GetName(eff.GetType(), eff);
		}

		public static EnumType ConvertToEnum<EnumType>(this string enumValue)
		{
			return (EnumType)Enum.Parse(typeof(EnumType), enumValue);
		}

		public static string GetDescription<T>(this T e) where T : IConvertible
		{
			if (e is Enum)
			{
				Type type = e.GetType();
				Array values = Enum.GetValues(type);

				foreach (int val in values)
				{
					if (val == e.ToInt32(CultureInfo.InvariantCulture))
					{
						var memInfo = type.GetMember(type.GetEnumName(val));

						if (memInfo[0].GetCustomAttributes(typeof(DescriptionAttribute), false)
							.FirstOrDefault() is DescriptionAttribute descriptionAttribute)
						{
							return descriptionAttribute.Description;
						}
					}
				}
			}

			return e.ToString();
		}

		public static string GetShortDescription<T>(this T e) where T : IConvertible
		{
			if (e is Enum)
			{
				Type type = e.GetType();
				Array values = Enum.GetValues(type);

				foreach (int val in values)
				{
					if (val == e.ToInt32(CultureInfo.InvariantCulture))
					{
						var memInfo = type.GetMember(type.GetEnumName(val));

						if (memInfo[0].GetCustomAttributes(typeof(ShortDescriptionAttribute), false)
							.FirstOrDefault() is ShortDescriptionAttribute descriptionAttribute)
						{
							return descriptionAttribute.Description;
						}
					}
				}
			}

			return e.ToString();
		}
	}
}

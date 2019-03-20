using System;

namespace CapFrameX.Extensions
{
	public static class ObjectExtensions
	{
		public static TProp GetOrFallbackOnNull<TObj, TProp>(this TObj obj, Func<TObj, TProp> propertyGetter, TProp fallback) where TObj : class
		{
			return obj == null ? fallback : propertyGetter(obj);
		}

		public static object GetDefaultValue(this Type t)
		{
			if (t.IsValueType && Nullable.GetUnderlyingType(t) == null)
				return Activator.CreateInstance(t);

			return null;
		}
	}
}

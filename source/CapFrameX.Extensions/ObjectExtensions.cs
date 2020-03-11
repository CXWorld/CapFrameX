using System;
using System.Collections.Generic;
using System.Linq;

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

		public static bool IsEither<T>(this T obj, IEnumerable<T> variants,
			IEqualityComparer<T> comparer)
		{
			variants.GuardNotNull(nameof(variants));
			comparer.GuardNotNull(nameof(comparer));

			return variants.Contains(obj, comparer);
		}

		public static bool IsEither<T>(this T obj, IEnumerable<T> variants)
			=> IsEither(obj, variants, EqualityComparer<T>.Default);

		public static bool IsEither<T>(this T obj, params T[] variants) => IsEither(obj, (IEnumerable<T>)variants);

		public static T GuardNotNull<T>(this T o, string argName = null)
		{
			if (o == null)
				throw new ArgumentNullException(argName);

			return o;
		}

		public static bool IsAllNotNull(params object[] objects)
		{
			return objects.All(o => !(o == null));
		}
	}
}

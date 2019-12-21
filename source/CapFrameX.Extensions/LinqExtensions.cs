using System;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Extensions
{
	public static class LinqExtensions
	{
		public static bool AllEqual<T>(this IEnumerable<T> values)
		{
			if (!values.Any()) return true;

			var first = values.First();
			return values.Skip(1).All(v => first.Equals(v));
		}

		public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
		{
			if (source == null || !source.Any())
				return;

			foreach (T element in source)
				action(element);
		}
	}
}

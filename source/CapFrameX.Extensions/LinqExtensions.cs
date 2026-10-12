using System;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Extensions
{
	public static class LinqExtensions
	{
		public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
		{
			if (source == null || !source.Any())
				return;

			foreach (T element in source)
				action(element);
		}
    }
}

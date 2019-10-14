using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Extensions
{
	public static class CollectionExtensions
	{
		public static IList<T> Move<T>(this IList<T> values, int sourceIndex, int targetIndex)
		{
			if (values == null || !values.Any())
				return values;

			var currentValue = values[sourceIndex];
			values.RemoveAt(sourceIndex);
			if (targetIndex < values.Count)
				values.Insert(targetIndex, currentValue);
			else
				values.Add(currentValue);

			return values;
		}
	}
}

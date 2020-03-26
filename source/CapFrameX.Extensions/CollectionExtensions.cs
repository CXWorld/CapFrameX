using System.Collections.Concurrent;
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

		public static BlockingCollection<T> Move<T>(this BlockingCollection<T> values, int sourceIndex, int targetIndex)
		{
			if (values == null || !values.Any())
				return values;

			var list = values.ToList();
			var currentValue = list[sourceIndex];
			list.RemoveAt(sourceIndex);
			if (targetIndex < list.Count)
				list.Insert(targetIndex, currentValue);
			else
				list.Add(currentValue);

			return new BlockingCollection<T>(new ConcurrentQueue<T>(list));
		}

		public static BlockingCollection<T> ToBlockingCollection<T>(this IEnumerable<T> values)
		{
			if (values == null || !values.Any())
				return null;

			return new BlockingCollection<T>(new ConcurrentQueue<T>(values));
		}
	}
}

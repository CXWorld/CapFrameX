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

        public static T MaxBy<T, U>(this IEnumerable<T> items, Func<T, U> selector)
        {
            if (!items.Any())
            {
                throw new InvalidOperationException("Empty input sequence");
            }

            var comparer = Comparer<U>.Default;
            T maxItem = items.First();
            U maxValue = selector(maxItem);

            foreach (T item in items.Skip(1))
            {
                // Get the value of the item and compare it to the current max.
                U value = selector(item);
                if (comparer.Compare(value, maxValue) > 0)
                {
                    maxValue = value;
                    maxItem = item;
                }
            }

            return maxItem;
        }
    }
}

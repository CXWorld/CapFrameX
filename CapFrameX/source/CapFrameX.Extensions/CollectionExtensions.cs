using System;
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
            list.Move(sourceIndex, targetIndex);

            while (values.TryTake(out _)) { }
            list.ForEach(values.Add);
            return values;
        }

        public static BlockingCollection<T> ToBlockingCollection<T>(this IEnumerable<T> values)
        {
            if (values == null || !values.Any())
                return null;

            return new BlockingCollection<T>(new ConcurrentQueue<T>(values));
        }

        public static bool IsNullOrEmpty<T>(this IEnumerable<T> values)
        {
            if (values == null || !values.Any())
                return true;

            return false;
        }

        public static IEnumerable<TSource> DistinctBy<TSource, TKey>
            (this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            HashSet<TKey> seenKeys = new HashSet<TKey>();
            foreach (TSource element in source)
            {
                if (seenKeys.Add(keySelector(element)))
                {
                    yield return element;
                }
            }
        }

        public static IEnumerable<T> TakeLast<T>(this IEnumerable<T> source, int count)
        {
            if (null == source)
                throw new ArgumentNullException(nameof(source));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            if (0 == count)
                yield break;

            if (source is ICollection<T>)
            {
                foreach (T item in source.Skip(((ICollection<T>)source).Count - count))
                    yield return item;

                yield break;
            }

            if (source is IReadOnlyCollection<T>)
            {
                foreach (T item in source.Skip(((IReadOnlyCollection<T>)source).Count - count))
                    yield return item;

                yield break;
            }

            // General case, we have to enumerate source
            Queue<T> result = new Queue<T>();

            foreach (T item in source)
            {
                if (result.Count == count)
                    result.Dequeue();

                result.Enqueue(item);
            }

            foreach (T item in result)
                yield return result.Dequeue();
        }


        public static bool IsEquivalent<T>(this IEnumerable<T> source, IEnumerable<T> compareValues)
        {
            if (source.Count() != compareValues.Count())
                return false;

            return source.OrderBy(x => x).SequenceEqual(
                 compareValues.OrderBy(x => x));
        }
    }
}

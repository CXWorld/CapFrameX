using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;


namespace CapFrameX.Extensions
{
    public static class ObservableCollectionExtensions
    {
        /// <summary>
        /// Extension method on the Observable collection for enabling sorting mechanism
        /// </summary>
        /// <param name="source"></param>
        /// <param name="selector"></param>
        /// <param name="direction"></param>
        /// <typeparam name="TSource"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        public static void Sort<TSource, TValue>(this ObservableCollection<TSource> source, Func<TSource, TValue> selector, ListSortDirection? direction)
        {
            for (int i = source.Count - 1; i >= 0; i--)
            {
                for (int j = 1; j <= i; j++)
                {
                    var row1 = source.ElementAt(j - 1);
                    var row2 = source.ElementAt(j);

                    var cell1 = selector(row1);
                    var cell2 = selector(row2);
                    int sortResult = (cell1 as IComparable).CompareTo(cell2 as IComparable);

                    sortResult = direction == ListSortDirection.Ascending ? sortResult * 1 : sortResult * -1;

                    if (sortResult > 0)
                    {
                        // Position the item correctly
                        source.Move(j - 1, j);
                    }
                }
            }
        }
    }

}

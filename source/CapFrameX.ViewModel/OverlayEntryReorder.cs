using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CapFrameX.ViewModel
{
    internal static class OverlayEntryReorder
    {
        internal static IReadOnlyList<T> GetDraggedItems<T>(object data)
        {
            if (data is T item)
                return new[] { item };

            if (data is System.Collections.IEnumerable items)
                return items.Cast<object>().OfType<T>().ToList();

            return Array.Empty<T>();
        }

        internal static IReadOnlyList<T> CreateOrder<T>(
            IReadOnlyList<T> currentItems,
            IEnumerable<T> draggedItems,
            int insertIndex)
        {
            if (currentItems == null)
                throw new ArgumentNullException(nameof(currentItems));

            var draggedItemSet = new HashSet<T>(draggedItems ?? Enumerable.Empty<T>());
            var orderedDraggedItems = currentItems
                .Where(draggedItemSet.Contains)
                .ToList();

            if (orderedDraggedItems.Count == 0)
                return currentItems.ToList();

            int boundedInsertIndex = Math.Max(0, Math.Min(insertIndex, currentItems.Count));
            int draggedItemsBeforeTarget = currentItems
                .Take(boundedInsertIndex)
                .Count(draggedItemSet.Contains);

            var reorderedItems = currentItems
                .Where(item => !draggedItemSet.Contains(item))
                .ToList();
            reorderedItems.InsertRange(
                boundedInsertIndex - draggedItemsBeforeTarget,
                orderedDraggedItems);

            return reorderedItems;
        }

        internal static void ApplyOrder<T>(
            ObservableCollection<T> items,
            IReadOnlyList<T> reorderedItems,
            Action<int, int> moveMirror)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));
            if (reorderedItems == null)
                throw new ArgumentNullException(nameof(reorderedItems));
            if (items.Count != reorderedItems.Count)
                throw new ArgumentException("The reordered list must contain all current items.", nameof(reorderedItems));

            for (int targetIndex = 0; targetIndex < reorderedItems.Count; targetIndex++)
            {
                int sourceIndex = items.IndexOf(reorderedItems[targetIndex]);
                if (sourceIndex < 0)
                    throw new ArgumentException("The reordered list contains an unknown item.", nameof(reorderedItems));
                if (sourceIndex == targetIndex)
                    continue;

                moveMirror?.Invoke(sourceIndex, targetIndex);
                items.Move(sourceIndex, targetIndex);
            }
        }
    }
}

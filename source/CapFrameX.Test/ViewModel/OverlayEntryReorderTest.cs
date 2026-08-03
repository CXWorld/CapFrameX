using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using CapFrameX.ViewModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.ViewModel
{
    [TestClass]
    public class OverlayEntryReorderTest
    {
        [TestMethod]
        public void GetDraggedItems_UntypedMultiSelection_ReturnsAllMatchingItems()
        {
            var draggedData = new ArrayList { "B", 42, "D" };

            var draggedItems = OverlayEntryReorder.GetDraggedItems<string>(draggedData);

            CollectionAssert.AreEqual(new[] { "B", "D" }, draggedItems.ToArray());
        }

        [TestMethod]
        public void CreateOrder_NonContiguousSelectionMovedDown_PreservesSelectionOrder()
        {
            var currentItems = new[] { "A", "B", "C", "D", "E" };

            var reorderedItems = OverlayEntryReorder.CreateOrder(
                currentItems,
                new[] { "D", "B" },
                currentItems.Length);

            CollectionAssert.AreEqual(
                new[] { "A", "C", "E", "B", "D" },
                reorderedItems.ToArray());
        }

        [TestMethod]
        public void CreateOrder_NonContiguousSelectionMovedUp_PreservesSelectionOrder()
        {
            var currentItems = new[] { "A", "B", "C", "D", "E" };

            var reorderedItems = OverlayEntryReorder.CreateOrder(
                currentItems,
                new[] { "D", "B" },
                0);

            CollectionAssert.AreEqual(
                new[] { "B", "D", "A", "C", "E" },
                reorderedItems.ToArray());
        }

        [TestMethod]
        public void ApplyOrder_UsesMoveNotificationsAndKeepsMirrorInSync()
        {
            var items = new ObservableCollection<string>(new[] { "A", "B", "C", "D" });
            var mirror = items.ToList();
            var collectionChanges = new List<NotifyCollectionChangedAction>();
            items.CollectionChanged += (_, args) => collectionChanges.Add(args.Action);

            var reorderedItems = new[] { "C", "A", "D", "B" };
            OverlayEntryReorder.ApplyOrder(
                items,
                reorderedItems,
                (sourceIndex, targetIndex) => Move(mirror, sourceIndex, targetIndex));

            CollectionAssert.AreEqual(reorderedItems, items.ToArray());
            CollectionAssert.AreEqual(reorderedItems, mirror.ToArray());
            Assert.IsTrue(collectionChanges.Count > 0);
            Assert.IsTrue(collectionChanges.All(action => action == NotifyCollectionChangedAction.Move),
                "Reordering must not reset the collection because that loses the viewport and selection.");
        }

        private static void Move<T>(IList<T> items, int sourceIndex, int targetIndex)
        {
            T item = items[sourceIndex];
            items.RemoveAt(sourceIndex);
            items.Insert(targetIndex, item);
        }
    }
}

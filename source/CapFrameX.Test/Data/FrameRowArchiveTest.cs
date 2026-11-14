using CapFrameX.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Test.Data
{
    /// <summary>
    /// CaptureManager's archive of the rows before a capture is a ring now; it shifted a list of
    /// 500 rows on every PresentMon row. It must hand out the same rows in the same order.
    /// </summary>
    [TestClass]
    public class FrameRowArchiveTest
    {
        [TestMethod]
        public void KeepsTheNewestRowsInOrderLikeTheShiftedList()
        {
            foreach (int capacity in new[] { 1, 2, 7, 500 })
            {
                var archive = new FrameRowArchive(capacity);
                var reference = new List<string[]>();
                Assert.IsNull(archive.Last);
                for (int i = 0; i < capacity * 3 + 5; i++)
                {
                    var row = new[] { i.ToString() };
                    archive.Add(row);
                    if (reference.Count == capacity) reference.RemoveAt(0);
                    reference.Add(row);

                    Assert.AreEqual(reference.Count, archive.Count);
                    Assert.AreSame(reference[reference.Count - 1], archive.Last);
                    CollectionAssert.AreEqual(reference, archive.ToList(), $"capacity {capacity}, row {i}");
                }

                archive.Clear();
                Assert.AreEqual(0, archive.Count);
                Assert.IsNull(archive.Last);
                Assert.AreEqual(0, archive.Count());
                archive.Add(new[] { "after clear" });
                Assert.AreEqual("after clear", archive.Single()[0]);
            }
        }
    }
}

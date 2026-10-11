using System;
using System.Collections.Generic;
using System.Linq;
using CapFrameX.OSD.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class OverlayHybridBarModelTest
    {
        [DataTestMethod]
        [DataRow(8, 16, 0, "E", 1, 8, "8,8,8")]
        [DataRow(6, 8, 2, "E", 1, 8, "6,8,2")]
        [DataRow(4, 8, 0, "D", 1, 4, "4,4,4")]
        [DataRow(8, 16, 0, "E", 2, 8, "8,8,8,8")]
        public void InitialHybridLayoutUsesPhysicalCoresAndStartsEachCoreClassOnANewRow(
            int performance, int efficiency, int lowPower, string efficiencyType, int performanceThreads,
            int columns, string expectedRows)
        {
            var document = CreateBound(performance, efficiency, lowPower, efficiencyType, performanceThreads);
            var tile = document.Tiles.Single();
            var layout = document.GetGroupBarLayout(tile);
            Assert.AreEqual(true, tile.GroupByCoreType);
            Assert.AreEqual(columns, layout.Columns);
            CollectionAssert.AreEqual(expectedRows.Split(',').Select(int.Parse).ToArray(), layout.RowCounts.ToArray());
            Assert.AreEqual(layout.RowCounts.Count, layout.Rows);
            Assert.AreEqual(performance * performanceThreads + efficiency + lowPower, layout.Members.Count);
            AssertSeparateCoreClasses(layout);
            Assert.IsTrue(layout.Members.Take(performance * performanceThreads).All(member => member.CoreType == "P"));
            Assert.AreEqual(performance, layout.Members.Where(member => member.CoreType == "P")
                .Select(member => member.CoreIndex).Distinct().Count());
        }

        [TestMethod]
        public void RowChoicesCountEachCoreClassSeparatelyAndRejectImpossibleTwoRows()
        {
            var document = CreateBound(6, 8, 2);
            var tile = document.Tiles.Single();
            CollectionAssert.AreEqual(new[] { 3, 4, 5, 6, 8, 16 }, document.GetGroupBarRowChoices(tile).ToArray());

            document.SetGroupBarRows(tile, 6);
            Assert.AreEqual(3, document.GetGroupBarLayout(tile).Columns);
            document.SetGroupBarRows(tile, 3);
            var layout = document.GetGroupBarLayout(tile);
            Assert.AreEqual(8, layout.Columns, "Eight columns are the smallest choice that fits all three core classes in three rows.");
            CollectionAssert.AreEqual(new[] { 6, 8, 2 }, layout.RowCounts.ToArray());
            string before = document.ToJson();
            Assert.ThrowsException<FormatException>(() => document.SetGroupBarRows(tile, 2));
            Assert.AreEqual(before, document.ToJson());
        }

        [DataTestMethod]
        [DataRow("vertical")]
        [DataRow("horizontal")]
        public void ExplicitBarWidthWrapsWithinEachCoreClassWithoutMixingRows(string orientation)
        {
            var document = CreateBound(6, 8, 2);
            var tile = document.Tiles.Single();
            tile.GroupBarOrientation = orientation;
            document.SetGroupBarRows(tile, 3);
            document.SetGroupBarWidth(tile, 150);

            var layout = document.GetGroupBarLayout(tile);
            Assert.AreEqual(3, layout.Columns);
            Assert.AreEqual(6, layout.Rows);
            CollectionAssert.AreEqual(new[] { 3, 3, 3, 3, 2, 2 }, layout.RowCounts.ToArray());
            Assert.AreEqual(150, layout.EffectiveWidth, .001);
            AssertSeparateCoreClasses(layout);
        }

        [DataTestMethod]
        [DataRow("vertical", false)]
        [DataRow("horizontal", false)]
        [DataRow("vertical", true)]
        [DataRow("horizontal", true)]
        public void TileResizePreservesClassRowsAndDistributesHeightAcrossActualRows(string orientation, bool resizeHeight)
        {
            var document = CreateBound(6, 8, 2);
            var tile = document.Tiles.Single();
            tile.GroupBarOrientation = orientation;
            document.SetGroupBarRows(tile, 3);
            document.SetGroupBarHeight(tile, 24);
            double height = tile.FrameHeight.Value;
            document.ResizeGroupBars(tile, 800, height + (resizeHeight ? 90 : 0));

            var layout = document.GetGroupBarLayout(tile);
            Assert.AreEqual(8, layout.Columns);
            CollectionAssert.AreEqual(new[] { 6, 8, 2 }, layout.RowCounts.ToArray());
            Assert.AreEqual(resizeHeight ? 54 : 24, layout.TrackHeight, .001,
                "The additional height must be divided over three class rows, not two flat rows.");
            Assert.AreEqual(height + (resizeHeight ? 90 : 0), tile.FrameHeight.Value, .001);
            Assert.AreEqual(800 - 2 * document.EffectivePadding(tile),
                layout.Columns * layout.EffectiveWidth + (layout.Columns - 1) * layout.Gap, .001);
            AssertSeparateCoreClasses(layout);
        }

        [TestMethod]
        public void DisabledMembersDoNotCreateEmptyClassRows()
        {
            var document = CreateBound(6, 8, 2);
            var tile = document.Tiles.Single();
            foreach (var member in tile.GroupMembers.Where(member => member.CoreType == "P")) member.IsEnabled = false;
            tile.GroupMembers.First(member => member.CoreType == "E").IsEnabled = false;
            document.EnsureGroupBarFits(tile);

            var layout = document.GetGroupBarLayout(tile);
            Assert.AreEqual(2, layout.Rows);
            CollectionAssert.AreEqual(new[] { 7, 2 }, layout.RowCounts.ToArray());
            Assert.IsTrue(layout.Members.All(member => member.IsEnabled && member.CoreType != "P"));
            AssertSeparateCoreClasses(layout);
        }

        [TestMethod]
        public void LayoutOrdersCoreThenThreadWithoutMutatingTheSavedMemberOrder()
        {
            var document = CreateBound(2, 2, 1, performanceThreads: 2);
            var tile = document.Tiles.Single();
            tile.GroupMembers.Reverse();
            string[] before = tile.GroupMembers.Select(member => member.Key).ToArray();
            var layout = document.GetGroupBarLayout(tile);

            CollectionAssert.AreEqual(new[] { 1, 1, 2, 2 }, layout.Members.Take(4).Select(member => member.CoreIndex.Value).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2, 1, 2 }, layout.Members.Take(4).Select(member => member.ThreadIndex.Value).ToArray());
            CollectionAssert.AreEqual(before, tile.GroupMembers.Select(member => member.Key).ToArray());
            AssertSeparateCoreClasses(layout);
        }

        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void CopyAndJsonRoundTripKeepTopologyAndExplicitGroupingChoice(bool grouped)
        {
            var document = CreateBound(8, 16, 2, performanceThreads: 2);
            var tile = document.Tiles.Single();
            document.SetGroupByCoreType(tile, grouped);
            var copy = tile.Copy();
            var restored = OsdDesignDocument.FromJson(document.ToJson());
            var restoredTile = restored.Tiles.Single();
            foreach (var result in new[] { copy, restoredTile })
            {
                Assert.AreEqual(grouped, result.GroupByCoreType);
                Assert.AreEqual(tile.GroupMembers.Count, result.GroupMembers.Count);
                for (int index = 0; index < tile.GroupMembers.Count; index++)
                {
                    var expected = tile.GroupMembers[index];
                    var actual = result.GroupMembers[index];
                    Assert.AreNotSame(expected, actual);
                    Assert.AreEqual(expected.Key, actual.Key);
                    Assert.AreEqual(expected.CoreType, actual.CoreType);
                    Assert.AreEqual(expected.CoreIndex, actual.CoreIndex);
                    Assert.AreEqual(expected.ThreadIndex, actual.ThreadIndex);
                }
            }
            CollectionAssert.AreEqual(document.GetGroupBarLayout(tile).RowCounts.ToArray(),
                restored.GetGroupBarLayout(restoredTile).RowCounts.ToArray());
        }

        [TestMethod]
        public void EnablingCoreGroupingRollsBackTheFlagAndGeometryWhenAddedRowsExceedCanvasLimit()
        {
            var document = CreateBound(6, 8, 2, grouped: false);
            var tile = document.Tiles.Single();
            document.CanvasHeight = 4096;
            document.SetGroupBarHeight(tile, 1950);
            Assert.AreEqual(2, document.GetGroupBarLayout(tile).Rows);
            string before = document.ToJson();

            Assert.ThrowsException<FormatException>(() => document.SetGroupByCoreType(tile, true));

            Assert.AreSame(tile, document.Tiles.Single());
            Assert.AreEqual(false, tile.GroupByCoreType);
            Assert.AreEqual(before, document.ToJson(), "A failed grouping edit must preserve dimensions, canvas height and member state.");
        }

        [TestMethod]
        public void ExplicitFalseKeepsInitialFlatRowsEvenWhenSourcesCarryHybridMetadata()
        {
            var document = CreateBound(6, 8, 2, grouped: false);
            var tile = document.Tiles.Single();
            var layout = document.GetGroupBarLayout(tile);
            Assert.AreEqual(false, tile.GroupByCoreType);
            Assert.AreEqual(8, layout.Columns);
            CollectionAssert.AreEqual(new[] { 8, 8 }, layout.RowCounts.ToArray());
            Assert.AreEqual(2, layout.Members.Take(8).Select(member => member.CoreType).Distinct().Count(),
                "Explicit flat mode is allowed to mix the end of one core class with the next class.");
        }

        private static OsdDesignDocument CreateBound(int performance, int efficiency, int lowPower,
            string efficiencyType = "E", int performanceThreads = 1, bool? grouped = null)
        {
            var document = new OsdDesignDocument
            {
                Name = "Hybrid bar model", IsCanvas = true, Width = 1000, CanvasHeight = 800,
                Padding = 0, GridSize = 4
            };
            document.Tiles.Add(new OsdDesignTile
            {
                Kind = "group", GroupDisplay = "bar", GroupBarOrientation = "vertical",
                Key = "role:cpu-effective-clocks", GroupKey = "role:cpu-effective-clocks",
                GroupColumns = 8, GroupByCoreType = grouped,
                GroupMembers = new List<OsdGroupMember>(), GroupShowLabels = false, GroupShowValues = false,
                Label = "Effective clocks", Unit = "MHz", Maximum = 6000, Height = 24,
                X = 0, Y = 0, FrameWidth = 600, FrameHeight = 300, Padding = 8
            });
            var sources = new List<OsdTelemetrySource>();
            int nextCore = 1;
            AddCores("P", performance, performanceThreads);
            AddCores(efficiencyType, efficiency, 1);
            AddCores("LPE", lowPower, 1);
            document.BindPresetSources(sources.AsEnumerable().Reverse());
            document.Validate();
            return document;

            void AddCores(string type, int count, int threads)
            {
                for (int index = 0; index < count; index++, nextCore++)
                {
                    for (int thread = 1; thread <= threads; thread++)
                    {
                        sources.Add(new OsdTelemetrySource
                        {
                            Key = $"cpu/effective/{nextCore}/{thread}",
                            Name = $"Core #{nextCore} {type}" + (threads > 1 ? $" Thread #{thread}" : "") + " (Effective)",
                            Category = "Processor", DeviceKey = "cpu", DeviceName = "Hybrid CPU", Unit = "MHz",
                            GroupKey = "cpu/cpu-effective-clocks", GroupName = "CPU effective clocks",
                            GroupOrder = nextCore * 1024 + thread, CoreType = type, CoreIndex = nextCore,
                            ThreadIndex = threads > 1 ? thread : (int?)null
                        });
                    }
                }
            }
        }

        private static void AssertSeparateCoreClasses(GroupBarLayout layout)
        {
            Assert.AreEqual(layout.Members.Count, layout.MemberRows.Count);
            Assert.AreEqual(layout.Members.Count, layout.MemberColumns.Count);
            foreach (int row in layout.MemberRows.Distinct())
            {
                var indices = Enumerable.Range(0, layout.Members.Count).Where(index => layout.MemberRows[index] == row).ToArray();
                Assert.AreEqual(1, indices.Select(index => layout.Members[index].CoreType).Distinct().Count(), "Core classes must never share a row.");
                CollectionAssert.AreEqual(Enumerable.Range(0, indices.Length).ToArray(), indices.Select(index => layout.MemberColumns[index]).ToArray());
                Assert.AreEqual(layout.RowCounts[row], indices.Length);
            }
        }
    }
}

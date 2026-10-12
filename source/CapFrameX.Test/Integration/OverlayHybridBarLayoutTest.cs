using System;
using System.Collections.Generic;
using System.Linq;
using CapFrameX.OSD.Controls;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class OverlayHybridBarLayoutTest
    {
        [TestMethod]
        [DataRow(8, 16, 0, "E", 8, 3)]
        [DataRow(6, 8, 2, "E", 8, 3)]
        [DataRow(4, 8, 0, "D", 4, 3)]
        public void InitialTemplateRowsFollowPhysicalCoreClasses(int performance, int efficiency,
            int lowPower, string efficiencyType, int columns, int rows)
        {
            var catalog = Sources(performance, efficiency, lowPower, efficiencyType: efficiencyType);
            var document = Document("vertical");
            var runtime = BindAndAssertParity(document, catalog);
            var tile = Group(document);
            var layout = document.GetGroupBarLayout(tile);
            Assert.AreEqual(true, tile.GroupByCoreType);
            Assert.AreEqual(columns, layout.Columns);
            Assert.AreEqual(rows, layout.Rows);
            AssertSeparatedRows(runtime, catalog, rows);
            Assert.AreEqual(performance, Members(runtime).Count(member => ((string)member["label"]).StartsWith("P ")));
            Assert.AreEqual(efficiency, Members(runtime).Count(member => ((string)member["label"]).StartsWith(efficiencyType + " ")));
        }

        [TestMethod]
        public void HyperthreadingKeepsEveryReadingAndUsesPhysicalCoreCountsForColumns()
        {
            var catalog = Sources(8, 16, performanceThreads: 2);
            var document = Document("vertical");
            var runtime = BindAndAssertParity(document, catalog);
            Assert.AreEqual(8, document.GetGroupBarLayout(Group(document)).Columns);
            AssertSeparatedRows(runtime, catalog, 4);
            CollectionAssert.AreEqual(catalog.Select(source => source.Id).ToArray(),
                Members(runtime).Select(member => (string)member["key"]).ToArray(),
                "Every P-core thread must remain adjacent to the other thread on its core.");
            Assert.AreEqual("P C1 T1", (string)Members(runtime)[0]["label"]);
            Assert.AreEqual("P C1 T2", (string)Members(runtime)[1]["label"]);
            Assert.AreEqual("E C9", (string)Members(runtime)[16]["label"]);
        }

        [TestMethod]
        [DataRow("vertical", .5, false)]
        [DataRow("vertical", 1.0, false)]
        [DataRow("vertical", 2.0, false)]
        [DataRow("horizontal", .5, false)]
        [DataRow("horizontal", 1.0, false)]
        [DataRow("horizontal", 2.0, false)]
        [DataRow("vertical", .5, true)]
        [DataRow("vertical", 1.0, true)]
        [DataRow("vertical", 2.0, true)]
        [DataRow("horizontal", .5, true)]
        [DataRow("horizontal", 1.0, true)]
        [DataRow("horizontal", 2.0, true)]
        public void HybridGroupsKeepEditorRuntimeParityAcrossScaleOrientationAndNesting(
            string orientation, double scale, bool nested)
        {
            var document = Document(orientation, nested);
            document.Scale = scale;
            var catalog = Sources(8, 16, 2, performanceThreads: 2).Reverse().ToArray();
            var runtime = BindAndAssertParity(document, catalog);
            AssertSeparatedRows(runtime, catalog, document.GetGroupBarLayout(Group(document)).Rows);
        }

        [TestMethod]
        [DataRow("vertical", .5)]
        [DataRow("vertical", 1.0)]
        [DataRow("vertical", 2.0)]
        [DataRow("horizontal", .5)]
        [DataRow("horizontal", 1.0)]
        [DataRow("horizontal", 2.0)]
        public void ExplicitWidthsLeavePartialRowsAtCoreTypeBoundaries(string orientation, double scale)
        {
            var document = Document(orientation);
            document.Scale = scale;
            var tile = Group(document);
            tile.FrameWidth = 256;
            tile.GroupBarWidth = orientation == "vertical" ? 61 : 58;
            tile.GroupBarHeight = 18;
            tile.GroupByCoreType = true;
            var catalog = Sources(3, 5, 2);
            var runtime = BindAndAssertParity(document, catalog);
            Assert.AreEqual(4, document.GetGroupBarLayout(tile).Columns);
            AssertSeparatedRows(runtime, catalog, 4);
            CollectionAssert.AreEqual(new[] { 3, 4, 1, 2 }, Members(runtime).GroupBy(member => (double)member["y"])
                .Select(row => row.Count()).ToArray());
            foreach (var member in Members(runtime))
                Assert.AreEqual(tile.GroupBarWidth.Value * scale, (double)member["frameWidth"], .000001);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ManualFlatLayoutIsPreservedForBothSavedAndNewDimensions(bool explicitDimensions)
        {
            var document = Document("vertical");
            var tile = Group(document);
            tile.FrameWidth = 256;
            tile.GroupByCoreType = false;
            if (explicitDimensions)
            {
                tile.GroupBarWidth = 61;
                tile.GroupBarHeight = 18;
            }
            var catalog = Sources(3, 5);
            var runtime = BindAndAssertParity(document, catalog);
            Assert.AreEqual(false, tile.GroupByCoreType);
            Assert.AreEqual(2, document.GetGroupBarLayout(tile).Rows);
            Assert.AreEqual(4, Members(runtime).Count(member => (double)member["y"] == 0));
            Assert.AreEqual("E C4", (string)Members(runtime)[3]["label"], "Disabling row separation must retain readable core type labels.");
        }

        [TestMethod]
        public void ExistingExplicitDimensionsDoNotOptIntoNewGroupingImplicitly()
        {
            var document = Document("vertical");
            var tile = Group(document);
            tile.FrameWidth = 256;
            tile.GroupBarWidth = 61;
            tile.GroupBarHeight = 18;
            var runtime = BindAndAssertParity(document, Sources(3, 5));
            Assert.IsNull(tile.GroupByCoreType);
            Assert.AreEqual(2, document.GetGroupBarLayout(tile).Rows);
            Assert.AreEqual(4, Members(runtime).Count(member => (double)member["y"] == 0));
        }

        [TestMethod]
        public void UnknownMetadataIsKeptAsItsOwnTrailingSectionWithoutInventingCoreTypes()
        {
            var catalog = Sources(3, 5).Concat(new[]
            {
                Reading("unknown-1", "CPU Reading A", null, null, null, 1),
                Reading("unknown-2", "CPU Reading B", "future-kind", null, null, 2)
            }).Reverse().ToArray();
            var document = Document("vertical");
            var runtime = BindAndAssertParity(document, catalog);
            AssertSeparatedRows(runtime, catalog, 4);
            var members = Members(runtime);
            Assert.AreEqual("Reading A", (string)members[^2]["label"]);
            Assert.AreEqual("Reading B", (string)members[^1]["label"]);
            Assert.AreEqual((double)members[^2]["y"], (double)members[^1]["y"], .000001);
            Assert.AreEqual(10, members.Length);
        }

        [TestMethod]
        public void MissingCoreIndicesFallBackToReadingsWithoutDroppingAnySource()
        {
            var catalog = Sources(3, 5).Select(source => Reading(source.Id, source.Name,
                source.CoreType, null, null, source.GroupOrder)).Reverse().ToArray();
            var document = Document("vertical");
            var runtime = BindAndAssertParity(document, catalog);
            Assert.AreEqual(4, document.GetGroupBarLayout(Group(document)).Columns);
            AssertSeparatedRows(runtime, catalog, 3);
        }

        [TestMethod]
        public void HomogeneousSensorsRetainBalancedRowsAndTheirExistingLabels()
        {
            var catalog = Enumerable.Range(1, 16).Select(index => Reading("plain-" + index,
                "CPU Core #" + index, null, index, null, index)).ToArray();
            var document = Document("vertical");
            var runtime = BindAndAssertParity(document, catalog);
            Assert.IsNull(Group(document).GroupByCoreType);
            Assert.AreEqual(8, document.GetGroupBarLayout(Group(document)).Columns);
            Assert.AreEqual(2, document.GetGroupBarLayout(Group(document)).Rows);
            Assert.AreEqual("C1", (string)Members(runtime)[0]["label"]);
        }

        [TestMethod]
        public void BoundCoreMetadataAndGroupingSurviveSavingAndReloading()
        {
            var document = Document("vertical");
            var catalog = Sources(8, 16, performanceThreads: 2);
            BindAndAssertParity(document, catalog);
            var roundTrip = OsdDesignDocument.FromJson(document.ToJson());
            var tile = Group(roundTrip);
            Assert.AreEqual(true, tile.GroupByCoreType);
            Assert.AreEqual("P", tile.GroupMembers[0].CoreType);
            Assert.AreEqual(1, tile.GroupMembers[0].CoreIndex);
            Assert.AreEqual(1, tile.GroupMembers[0].ThreadIndex);
            Assert.AreEqual("E", tile.GroupMembers[16].CoreType);
            Assert.AreEqual(9, tile.GroupMembers[16].CoreIndex);
            Assert.IsNull(tile.GroupMembers[16].ThreadIndex);
            Assert.AreEqual(4, roundTrip.GetGroupBarLayout(tile).Rows);
            AssertTokens(JToken.Parse(document.ToJson()), JToken.Parse(roundTrip.ToJson()));
        }

        [TestMethod]
        public void WidthHeightRowsAndOuterResizePreserveCoreClassSeparation()
        {
            var document = Document("vertical");
            var tile = Group(document);
            tile.FrameWidth = 256;
            var catalog = Sources(3, 5, 2);
            document.BindPresetSources(catalog.Select(EditorSource));
            document.SetGroupBarWidth(tile, 126);
            Assert.AreEqual(2, document.GetGroupBarLayout(tile).Columns);
            Assert.AreEqual(6, document.GetGroupBarLayout(tile).Rows);
            document.SetGroupBarHeight(tile, 20);
            Assert.AreEqual(20, document.GetGroupBarLayout(tile).TrackHeight, .000001);
            Assert.IsFalse(document.GetGroupBarRowChoices(tile).Contains(2), "Three core classes cannot share two rows.");
            document.SetGroupBarRows(tile, 4);
            Assert.AreEqual(4, document.GetGroupBarLayout(tile).Rows);
            int columns = document.GetGroupBarLayout(tile).Columns;
            document.ResizeGroupBars(tile, 384, tile.FrameHeight.Value);
            Assert.AreEqual(columns, document.GetGroupBarLayout(tile).Columns);
            Assert.AreEqual(4, document.GetGroupBarLayout(tile).Rows);
            Assert.AreEqual(true, tile.GroupByCoreType);
            var design = OverlayRuntimeDesign.Parse("edited", document.Name, document.ToJson());
            AssertSeparatedRows(design, catalog, 4);
        }

        [TestMethod]
        public void ExplicitGroupingWithoutDimensionsUsesLegacySizingAndRetainsClassBoundaries()
        {
            var document = Document("vertical");
            var tile = Group(document);
            tile.GroupByCoreType = true;
            tile.GroupColumns = 4;
            var catalog = Sources(3, 5, 2);
            var runtime = BindAndAssertParity(document, catalog);
            Assert.IsNull(tile.GroupBarWidth);
            Assert.IsNull(tile.GroupBarHeight);
            Assert.AreEqual(4, document.GetGroupBarLayout(tile).Columns);
            AssertSeparatedRows(runtime, catalog, 4);
        }

        private static OsdDesignDocument Document(string orientation, bool nested = false)
        {
            var prototype = OsdDesignDocument.FromJson(OverlayDesignStarterCatalog.Profiles
                .Single(profile => profile.PresetId == "vertical-thread-strip").DesignJson);
            var group = prototype.Tiles.Single(tile => tile.Kind == "group").Copy();
            group.X = 0;
            group.Y = 0;
            group.Padding = 0;
            group.FrameWidth = 1280;
            group.FrameHeight = 112;
            group.GroupColumns = 8;
            group.GroupBarOrientation = orientation;
            group.GroupBarWidth = null;
            group.GroupBarHeight = null;
            group.Height = 24;
            group.GroupShowValues = false;
            var document = new OsdDesignDocument
            {
                Name = "Hybrid processor", IsCanvas = true, Width = 1408, CanvasHeight = 720,
                Padding = 0, GridSize = 4
            };
            var following = new OsdDesignTile { X = 0, Y = 120, FrameWidth = 1280, FrameHeight = 48 };
            if (nested)
            {
                var container = new OsdDesignTile
                {
                    Kind = "container", Padding = 8, X = 0, Y = 0, FrameWidth = 1296, FrameHeight = 184
                };
                container.Children.Add(group);
                container.Children.Add(following);
                document.Tiles.Add(container);
                document.Tiles.Add(new OsdDesignTile { X = 0, Y = 192, FrameWidth = 1296, FrameHeight = 48 });
            }
            else
            {
                document.Tiles.Add(group);
                document.Tiles.Add(following);
            }
            return document;
        }

        private static OsdDesignTile Group(OsdDesignDocument document) => document.Tiles[0].Kind == "container"
            ? document.Tiles[0].Children[0] : document.Tiles[0];

        private static OverlayTelemetrySource[] Sources(int performance, int efficiency, int lowPower = 0,
            int performanceThreads = 1, string efficiencyType = "E")
        {
            var sources = new List<OverlayTelemetrySource>();
            for (int core = 1; core <= performance + efficiency + lowPower; core++)
            {
                string type = core <= performance ? "P" : core <= performance + efficiency ? efficiencyType : "LPE";
                int threads = core <= performance ? performanceThreads : 1;
                for (int thread = 1; thread <= threads; thread++)
                {
                    string name = "CPU Core #" + core + (threads > 1 ? " Thread #" + thread : "");
                    sources.Add(Reading("core-" + core + "-" + thread, name, type, core,
                        threads > 1 ? thread : null, sources.Count + 1));
                }
            }
            return sources.ToArray();
        }

        private static OverlayTelemetrySource Reading(string key, string name, string type, int? core, int? thread, int order)
            => new OverlayTelemetrySource
            {
                Id = key, Name = name, CoreType = type, CoreIndex = core, ThreadIndex = thread,
                DeviceKey = "cpu0", HardwareName = "Processor", GroupKey = "cpu0/cpu-loads",
                GroupOrder = order, Category = "CPU", Unit = "%"
            };

        private static OsdTelemetrySource EditorSource(OverlayTelemetrySource source) => new OsdTelemetrySource
        {
            Key = source.Id, Name = source.Name, DeviceKey = source.DeviceKey, DeviceName = source.HardwareName,
            Category = source.Category, Unit = source.Unit, GroupKey = source.GroupKey, GroupOrder = source.GroupOrder,
            CoreType = source.CoreType, CoreIndex = source.CoreIndex, ThreadIndex = source.ThreadIndex,
            IsAvailable = source.IsAvailable
        };

        private static OverlayRuntimeDesign BindAndAssertParity(OsdDesignDocument document, OverlayTelemetrySource[] catalog)
        {
            string saved = document.ToJson();
            var design = OverlayRuntimeDesign.Parse("hybrid", document.Name, saved);
            string original = design.AuthoringJson;
            var actual = OverlayRuntimeSourceBinder.Bind(design, catalog);
            document.BindPresetSources(catalog.Select(EditorSource));
            var expected = OverlayRuntimeDesign.Parse("hybrid", document.Name, document.ToJson());
            AssertTokens(JToken.Parse(expected.TemplateJson), JToken.Parse(actual.TemplateJson));
            Assert.AreEqual(original, design.AuthoringJson, "Runtime binding must not modify the saved profile.");
            CollectionAssert.AreEqual(expected.MetricKeys.ToArray(), actual.MetricKeys.ToArray());
            CollectionAssert.AreEquivalent(catalog.Select(source => source.Id).ToArray(), Members(actual).Select(member => (string)member["key"]).ToArray());
            return actual;
        }

        private static JObject[] Members(OverlayRuntimeDesign design) => JObject.Parse(design.TemplateJson)
            .Descendants().OfType<JObject>().Where(node => (string)node["type"] == "bar").ToArray();

        private static void AssertSeparatedRows(OverlayRuntimeDesign design, OverlayTelemetrySource[] sources, int rows)
        {
            var lookup = sources.ToDictionary(source => source.Id);
            var members = Members(design);
            Assert.AreEqual(sources.Length, members.Length);
            var actualRows = members.GroupBy(member => (double)member["y"]).ToArray();
            Assert.AreEqual(rows, actualRows.Length);
            foreach (var row in actualRows)
            {
                Assert.AreEqual(1, row.Select(member => lookup[(string)member["key"]].CoreType is "P" or "E" or "LPE" or "D" or "LP"
                    ? lookup[(string)member["key"]].CoreType : null).Distinct().Count(), "A row mixes core types.");
                Assert.AreEqual(0, (double)row.First()["x"], .000001);
            }
            CollectionAssert.AreEquivalent(sources.Select(source => source.Id).ToArray(), members.Select(member => (string)member["key"]).ToArray());
        }

        private static void AssertTokens(JToken expected, JToken actual)
        {
            Assert.IsNotNull(actual, expected.Path);
            if (expected.Type == JTokenType.Integer || expected.Type == JTokenType.Float)
            {
                Assert.AreEqual((double)expected, (double)actual, .0000001, expected.Path);
                return;
            }
            Assert.AreEqual(expected.Type, actual.Type, expected.Path);
            if (expected is JObject obj)
            {
                Assert.AreEqual(obj.Count, ((JObject)actual).Count, expected.Path);
                foreach (var property in obj.Properties()) AssertTokens(property.Value, actual[property.Name]);
            }
            else if (expected is JArray array)
            {
                Assert.AreEqual(array.Count, ((JArray)actual).Count, expected.Path);
                for (int index = 0; index < array.Count; index++) AssertTokens(array[index], actual[index]);
            }
            else Assert.IsTrue(JToken.DeepEquals(expected, actual), expected.Path);
        }
    }
}

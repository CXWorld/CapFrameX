using System;
using System.Linq;
using CapFrameX.OSD.Controls;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class OverlayTemplateBarBalanceTest
    {
        [TestMethod]
        [DataRow(8, 4, 2)]
        [DataRow(16, 8, 2)]
        [DataRow(24, 12, 2)]
        [DataRow(32, 16, 2)]
        [DataRow(64, 32, 2)]
        [DataRow(128, 32, 4)]
        public void ThermalEffectiveClocksStartInEvenlyPopulatedRows(int count, int columns, int rows)
        {
            var document = OsdDesignPresets.Find("vertical-thermal-panel").CreateDocument();
            var result = BindAndAssertParity(document, Sources("cpu-effective-clocks", count, "MHz"));
            var group = document.Tiles.Single(tile => tile.GroupKey?.Contains("cpu-effective-clocks") == true);
            var layout = document.GetGroupBarLayout(group);
            Assert.AreEqual(columns, layout.Columns);
            Assert.AreEqual(rows, layout.Rows);
            Assert.AreEqual(columns, group.GroupColumns);
            AssertBalancedNativeGroup(result, "cpu0/cpu-effective-clocks/", count, columns);
        }

        [TestMethod]
        [DataRow(8)]
        [DataRow(16)]
        [DataRow(24)]
        [DataRow(32)]
        [DataRow(64)]
        [DataRow(128)]
        public void EveryTemplateAndStarterBalancesInitialBarRows(int count)
        {
            var catalog = Sources("cpu-loads", count, "%")
                .Concat(Sources("cpu-clocks", count, "MHz"))
                .Concat(Sources("cpu-effective-clocks", count, "MHz"))
                .Concat(Sources("cpu-temperatures", count, "°C"))
                .Concat(Sources("cpu-core-power", count, "W")).ToArray();
            var documents = OsdDesignPresets.All.Select(preset => preset.CreateDocument())
                .Concat(OverlayDesignStarterCatalog.Profiles.Select(starter => OsdDesignDocument.FromJson(starter.DesignJson)))
                .Where(document => document.EnumerateTiles().Any(tile => tile.Kind == "group" && tile.GroupDisplay == "bar"));
            int checkedGroups = 0, checkedSensorGroups = 0;
            foreach (var document in documents)
            {
                // Discovery only rebuilds preset roles. Panther Lake binds its groups to concrete
                // sensors, which keep their authored members whatever the catalog holds.
                var sensorGroups = document.EnumerateTiles()
                    .Where(tile => tile.Kind == "group" && tile.GroupDisplay == "bar" && tile.GroupKey?.StartsWith("role:", StringComparison.Ordinal) != true)
                    .ToDictionary(tile => tile, tile => tile.GroupMembers.Select(member => member.Key).ToArray());
                var result = BindAndAssertParity(document, catalog);
                foreach (var group in document.EnumerateTiles().Where(tile => tile.Kind == "group" && tile.GroupDisplay == "bar"))
                {
                    // A role of one core type ("role:cpu-loads|P") binds hybrid CPUs only; the FUSION
                    // starter tests cover them with the core counts of their CPUs.
                    if (group.GroupKey?.StartsWith("role:", StringComparison.Ordinal) == true && group.GroupKey.Contains('|')) continue;
                    var layout = document.GetGroupBarLayout(group);
                    bool sensorBound = sensorGroups.TryGetValue(group, out var sensorKeys);
                    int bars = sensorBound ? sensorKeys.Length : count;
                    Assert.IsTrue(group.HasExplicitGroupBarLayout, document.Name);
                    Assert.AreEqual(0, bars % layout.Columns, document.Name + ": every row must have the same number of bars.");
                    Assert.AreEqual(0, layout.Columns % 2, document.Name + ": initial rows must contain even numbers of bars.");
                    Assert.IsTrue(layout.Rows == 1 || layout.Rows % 2 == 0, document.Name + ": no unintended third row.");
                    if (sensorBound)
                    {
                        CollectionAssert.AreEqual(sensorKeys, group.GroupMembers.Select(member => member.Key).ToArray(),
                            document.Name + ": discovery must not rebind concrete sensors.");
                        AssertBalancedRows(result, NativeBars(result, sensorKeys.Contains), bars, layout.Columns);
                        checkedSensorGroups++;
                    }
                    else
                    {
                        AssertBalancedNativeGroup(result, group.GroupMembers[0].Key[..group.GroupMembers[0].Key.LastIndexOf('/')], count, layout.Columns);
                        checkedGroups++;
                    }
                }
            }
            Assert.IsTrue(checkedGroups >= 17, "Both the template gallery and shipped Saved tile profiles must be covered.");
            Assert.IsTrue(checkedSensorGroups >= 3, "Panther Lake's per-core-type groups must be covered.");
        }

        [TestMethod]
        [DataRow(.5, 208)]
        [DataRow(1.0, 208)]
        [DataRow(2.0, 208)]
        [DataRow(.5, 32)]
        [DataRow(1.0, 32)]
        [DataRow(2.0, 32)]
        public void NarrowNestedGroupsKeepEveryBarAndMatchScaledRuntime(double scale, int width)
        {
            var document = new OsdDesignDocument
            {
                Name = "Nested balanced bars", IsCanvas = true, Width = 640,
                CanvasHeight = 720, Padding = 0, Scale = scale, GridSize = 4
            };
            var parent = new OsdDesignTile
            {
                Kind = "container", X = 0, Y = 0, FrameWidth = 320, FrameHeight = 120, Padding = 0
            };
            var group = OsdDesignPresets.Find("vertical-thread-strip").CreateDocument().Tiles.Single(tile => tile.Kind == "group").Copy();
            group.X = 0; group.Y = 0; group.FrameWidth = width; group.FrameHeight = 80;
            group.Padding = 0; group.GroupShowLabels = false; group.GroupShowValues = false; group.Height = 32;
            parent.Children.Add(group);
            parent.Children.Add(new OsdDesignTile { X = 0, Y = 88, FrameWidth = 208, FrameHeight = 24 });
            document.Tiles.Add(parent);
            var result = BindAndAssertParity(document, Sources("cpu-loads", 128, "%"));
            var layout = document.GetGroupBarLayout(group);
            Assert.AreEqual(0, 128 % layout.Columns);
            Assert.AreEqual(0, layout.Columns % 2);
            Assert.IsTrue(layout.EffectiveWidth >= 8);
            AssertBalancedNativeGroup(result, "cpu0/cpu-loads/", 128, layout.Columns);
        }

        [TestMethod]
        [DataRow(7)]
        [DataRow(17)]
        [DataRow(31)]
        public void OddSensorCountsRemainCompleteWithoutInventingBars(int count)
        {
            var document = OsdDesignPresets.Find("vertical-thermal-panel").CreateDocument();
            var catalog = Sources("cpu-effective-clocks", count, "MHz");
            var result = BindAndAssertParity(document, catalog);
            var members = NativeBars(result, "cpu0/cpu-effective-clocks/");
            CollectionAssert.AreEquivalent(catalog.Select(source => source.Id).ToArray(),
                members.Select(member => (string)member["key"]).ToArray());
            Assert.AreEqual(count, members.Length);
        }

        [TestMethod]
        [DataRow("vertical", 100.0, 18.0)]
        [DataRow("horizontal", 100.0, 18.0)]
        public void ExplicitManualDimensionsKeepTheirIntentionalRaggedRows(string orientation, double width, double height)
        {
            var document = OsdDesignPresets.Find("vertical-thread-strip").CreateDocument();
            var group = document.Tiles.Single(tile => tile.Kind == "group");
            group.GroupBarOrientation = orientation;
            group.GroupBarWidth = width; group.GroupBarHeight = height;
            var result = BindAndAssertParity(document, Sources("cpu-loads", 16, "%"));
            Assert.AreEqual(width, group.GroupBarWidth.Value);
            Assert.AreEqual(height, group.GroupBarHeight.Value);
            var layout = document.GetGroupBarLayout(group);
            Assert.AreEqual(5, layout.Columns);
            Assert.AreEqual(4, layout.Rows);
            var members = NativeBars(result, "cpu0/cpu-loads/");
            CollectionAssert.AreEqual(new[] { 5, 5, 5, 1 }, members.GroupBy(member => (double)member["y"])
                .Select(row => row.Count()).ToArray());
        }

        private static OverlayRuntimeDesign BindAndAssertParity(OsdDesignDocument document, OverlayTelemetrySource[] catalog)
        {
            string original = document.ToJson();
            var saved = OverlayRuntimeDesign.Parse("balanced", document.Name, original);
            string savedAuthoringJson = saved.AuthoringJson;
            var result = OverlayRuntimeSourceBinder.Bind(saved, catalog);
            document.BindPresetSources(catalog.Select(source => new OsdTelemetrySource
            {
                Key = source.Id, Name = source.Name, DeviceKey = source.DeviceKey, DeviceName = source.HardwareName,
                Category = source.Category, Unit = source.Unit, GroupKey = source.GroupKey, GroupOrder = source.GroupOrder,
                IsAvailable = source.IsAvailable
            }));
            var expected = OverlayRuntimeDesign.Parse("balanced", document.Name, document.ToJson());
            AssertTokens(JToken.Parse(expected.TemplateJson), JToken.Parse(result.TemplateJson), document.Name);
            Assert.AreEqual(savedAuthoringJson, saved.AuthoringJson, "Source discovery must not overwrite a saved profile.");
            return result;
        }

        private static OverlayTelemetrySource[] Sources(string family, int count, string unit) => Enumerable.Range(1, count)
            .Select(index => new OverlayTelemetrySource
            {
                Id = "cpu0/" + family + "/" + index, DeviceKey = "cpu0", HardwareName = "CPU",
                Name = "Core #" + ((index + 1) / 2) + " Thread #" + (index % 2 == 1 ? 1 : 2),
                GroupKey = "cpu0/" + family, Category = "CPU", Unit = unit, GroupOrder = index, IsAvailable = true
            }).ToArray();

        private static JObject[] NativeBars(OverlayRuntimeDesign design, string keyPrefix) =>
            NativeBars(design, key => key.StartsWith(keyPrefix, StringComparison.Ordinal));

        private static JObject[] NativeBars(OverlayRuntimeDesign design, Func<string, bool> includesKey) =>
            JObject.Parse(design.TemplateJson).DescendantsAndSelf().OfType<JObject>()
                .Where(node => (string)node["type"] == "bar" && (string)node["key"] is string key && includesKey(key)).ToArray();

        private static void AssertBalancedNativeGroup(OverlayRuntimeDesign design, string keyPrefix, int count, int columns) =>
            AssertBalancedRows(design, NativeBars(design, keyPrefix), count, columns);

        private static void AssertBalancedRows(OverlayRuntimeDesign design, JObject[] members, int count, int columns)
        {
            Assert.AreEqual(count, members.Length, design.Name);
            var rows = members.GroupBy(member => (double?)member["y"] ?? -1).ToArray();
            Assert.AreEqual(count / columns, rows.Length, design.Name);
            Assert.IsTrue(rows.All(row => row.Count() == columns), design.Name);
        }

        private static void AssertTokens(JToken expected, JToken actual, string context)
        {
            Assert.IsNotNull(actual, context + ": " + expected.Path);
            if (expected.Type == JTokenType.Integer || expected.Type == JTokenType.Float)
            {
                Assert.AreEqual((double)expected, (double)actual, .0000001, context + ": " + expected.Path);
                return;
            }
            Assert.AreEqual(expected.Type, actual.Type, context + ": " + expected.Path);
            if (expected is JObject obj)
            {
                Assert.AreEqual(obj.Count, ((JObject)actual).Count, context + ": " + expected.Path);
                foreach (var property in obj.Properties()) AssertTokens(property.Value, actual[property.Name], context);
            }
            else if (expected is JArray array)
            {
                Assert.AreEqual(array.Count, ((JArray)actual).Count, context + ": " + expected.Path);
                for (int index = 0; index < array.Count; index++) AssertTokens(array[index], actual[index], context);
            }
            else Assert.IsTrue(JToken.DeepEquals(expected, actual), context + ": " + expected.Path);
        }
    }
}

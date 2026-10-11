using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Monitoring.Contracts;
using CapFrameX.OSD.Controls;
using CapFrameX.OSD.Integration;
using CapFrameX.Sensor;
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Hardware.Cpu;
using LibreHardwareMonitor.Hardware.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json.Linq;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class OverlayCoreTypeCoverageTest
    {
        private static readonly string[] TemplateIds =
        {
            "core-types-overview", "core-types-compact", "core-types-thermal",
            "core-types-intel-desktop", "core-types-intel-mobile", "core-types-amd"
        };

        [DataTestMethod]
        [DataRow(CpuCoreType.Standard, null)]
        [DataRow(CpuCoreType.PerformanceCore, "P")]
        [DataRow(CpuCoreType.EfficiencyCore, "E")]
        [DataRow(CpuCoreType.LowPowerEfficiencyCore, "LPE")]
        [DataRow(CpuCoreType.DenseCore, "D")]
        [DataRow(CpuCoreType.LowPowerCore, "LP")]
        public async Task EveryDeclaredCoreTypeSurvivesActualSensorBuildersTelemetryAndSceneBinding(CpuCoreType type, string label)
        {
            Assert.AreEqual(label ?? string.Empty, SimulatedCpuModel.GetCoreLabel(type));
            var model = Model(new SimulatedCoreClass(type, 8, 1000, 5000, 85));
            bool intel = type is CpuCoreType.PerformanceCore or CpuCoreType.EfficiencyCore or CpuCoreType.LowPowerEfficiencyCore;
            var cpu = CreateCpu(model, intel);
            try
            {
                var catalog = await Discover(cpu);
                var families = catalog.Where(source => source.GroupKey != null).GroupBy(source => source.GroupKey).ToArray();
                Assert.AreEqual(intel ? 4 : 2, families.Length,
                    "Use the simulated driver's real family set; do not manufacture unsupported sensor families.");
                foreach (var family in families)
                {
                    var readings = family.OrderBy(source => source.GroupOrder).ToArray();
                    Assert.AreEqual(8, readings.Length);
                    Assert.IsTrue(readings.All(source => source.CoreType == label));
                    CollectionAssert.AreEqual(Enumerable.Range(1, 8).ToArray(), readings.Select(source => source.CoreIndex.Value).ToArray());
                    Assert.IsTrue(readings.All(source => source.ThreadIndex == null));

                    string familyId = family.Key[(family.Key.LastIndexOf('/') + 1)..];
                    var document = FamilyDocument(familyId, readings[0].Unit);
                    AssertParity(document, catalog);
                    var tile = document.Tiles.Single();
                    CollectionAssert.AreEquivalent(readings.Select(source => source.Id).ToArray(), tile.GroupMembers.Select(member => member.Key).ToArray());
                    Assert.IsTrue(tile.GroupMembers.All(member => member.CoreType == label));
                    Assert.IsNull(tile.GroupByCoreType, "One core class alone does not need hybrid section breaks.");
                    AssertLabels(tile, catalog);
                }
                Assert.IsTrue(catalog.Where(source => source.GroupKey == null).All(source => source.CoreType == null && source.CoreIndex == null),
                    "Aggregate sensors must not acquire per-core topology from their processor's model.");
            }
            finally
            {
                cpu.Close();
            }
        }

        [TestMethod]
        public void CoveredCoreTypesMatchTheCompleteNativeEnum()
        {
            CollectionAssert.AreEquivalent(new[]
            {
                CpuCoreType.Standard, CpuCoreType.PerformanceCore, CpuCoreType.EfficiencyCore,
                CpuCoreType.LowPowerEfficiencyCore, CpuCoreType.DenseCore, CpuCoreType.LowPowerCore
            }, Enum.GetValues<CpuCoreType>(), "A new native core type needs end-to-end coverage before it can be considered supported.");
        }

        [DataTestMethod]
        [DataRow("standard", 16, "")]
        [DataRow("intel-desktop", 24, "P,E")]
        [DataRow("intel-mobile", 16, "P,E,LPE")]
        [DataRow("amd-hybrid", 12, "P,D")]
        [DataRow("amd-lp-code-value", 14, "P,D,LP")]
        public async Task EveryCoreTypeTemplateMatchesRuntimeForActualAvailableSimulatedFamilies(
            string topology, int coreCount, string expectedTypes)
        {
            var cpu = CreateTopology(topology);
            try
            {
                var catalog = await Discover(cpu);
                var clocks = catalog.Where(source => source.GroupKey?.EndsWith("/cpu-clocks", StringComparison.Ordinal) == true).ToArray();
                Assert.AreEqual(coreCount, clocks.Length);
                CollectionAssert.AreEquivalent(expectedTypes.Split(','), clocks.Select(source => source.CoreType ?? "").Distinct().ToArray());
                int boundGroups = 0;
                foreach (string id in TemplateIds)
                {
                    var preset = OsdDesignPresets.Find(id);
                    Assert.AreEqual(id, preset.Id, "The architecture template must not fall back to the default preset.");
                    var document = preset.CreateDocument();
                    var rolesBeforeBinding = document.EnumerateTiles()
                        .Where(tile => tile.Kind == "group" && tile.GroupKey?.StartsWith("role:", StringComparison.Ordinal) == true)
                        .Select(tile => tile.GroupKey.Substring(5)).ToArray();
                    Assert.IsTrue(rolesBeforeBinding.Length > 0, id + " must retain portable sensor families.");
                    AssertParity(document, catalog);
                    var boundTiles = document.EnumerateTiles().Where(tile => tile.Kind == "group" &&
                        tile.GroupKey?.StartsWith("role:", StringComparison.Ordinal) == false).ToArray();
                    int expectedBoundGroups = rolesBeforeBinding.Count(role => catalog.Any(source =>
                        source.GroupKey?.EndsWith("/" + role, StringComparison.Ordinal) == true));
                    Assert.AreEqual(expectedBoundGroups, boundTiles.Length, id + " must bind every available family and retain missing families as unbound.");
                    foreach (var tile in boundTiles)
                    {
                        boundGroups++;
                        Assert.AreEqual(coreCount, tile.GroupMembers.Count, id);
                        AssertLabels(tile, catalog);
                        if (tile.GroupDisplay != "bar") continue;
                        var layout = document.GetGroupBarLayout(tile);
                        CollectionAssert.AreEquivalent(tile.GroupMembers.Select(member => member.Key).ToArray(), layout.Members.Select(member => member.Key).ToArray());
                        if (topology == "standard")
                        {
                            Assert.IsTrue(tile.GroupMembers.All(member => member.CoreType == null), id);
                        }
                        else
                        {
                            Assert.AreEqual(true, tile.GroupByCoreType, id);
                            foreach (int row in layout.MemberRows.Distinct())
                            {
                                var indices = Enumerable.Range(0, layout.Members.Count).Where(index => layout.MemberRows[index] == row);
                                Assert.AreEqual(1, indices.Select(index => layout.Members[index].CoreType).Distinct().Count(), id + " mixed core classes in a row.");
                            }
                        }
                    }
                }
                Assert.IsTrue(boundGroups >= TemplateIds.Length, "Each architecture template must bind at least one real simulated sensor family.");
            }
            finally
            {
                cpu.Close();
            }
        }

        private static SimulatedCpuBase CreateTopology(string topology) => topology switch
        {
            "standard" => CreateCpu(SimulatedCpuModels.AmdRyzen9_7950X, false),
            "intel-desktop" => CreateCpu(SimulatedCpuModels.IntelCoreI9_14900K, true),
            "intel-mobile" => CreateCpu(SimulatedCpuModels.IntelCoreUltra9_285H, true),
            "amd-hybrid" => CreateCpu(SimulatedCpuModels.AmdRyzenAi9Hx370, false),
            // Exercise the already supported LP enum value, without asserting a shipping CPU layout.
            "amd-lp-code-value" => CreateCpu(Model(
                new SimulatedCoreClass(CpuCoreType.PerformanceCore, 4, 2000, 5000, 85),
                new SimulatedCoreClass(CpuCoreType.DenseCore, 8, 1500, 3500, 80),
                new SimulatedCoreClass(CpuCoreType.LowPowerCore, 2, 700, 2000, 70)), false),
            _ => throw new ArgumentOutOfRangeException(nameof(topology))
        };

        private static SimulatedCpuModel Model(params SimulatedCoreClass[] classes) =>
            new SimulatedCpuModel("Core type coverage fixture", classes, 15, 100, 30, 85);

        private static SimulatedCpuBase CreateCpu(SimulatedCpuModel model, bool intel)
        {
            var settings = new Mock<ISettings>();
            settings.Setup(value => value.GetValue(It.IsAny<string>(), It.IsAny<string>()))
                .Returns<string, string>((name, fallback) => fallback);
            return intel ? new SimulatedIntelCpu(model, 0, settings.Object) : new SimulatedAmd17Cpu(model, 0, settings.Object);
        }

        private static async Task<OverlayTelemetrySource[]> Discover(IHardware cpu)
        {
            var entries = cpu.Sensors.Select(sensor => (ISensorEntry)new SensorEntry
            {
                Identifier = sensor.Identifier.ToString(), Name = sensor.Name,
                SensorType = sensor.SensorType.ToString(), HardwareType = cpu.HardwareType.ToString(),
                HardwareName = cpu.Name, IsPresentationDefault = sensor.IsPresentationDefault
            }).ToArray();
            var provider = new Mock<ISensorService>();
            provider.Setup(service => service.GetSensorEntries()).ReturnsAsync(entries);
            provider.SetupGet(service => service.SensorSnapshotStream)
                .Returns(Observable.Never<(DateTime, Dictionary<ISensorEntry, float>)>());
            using var service = new OverlayTelemetryService(provider.Object, Mock.Of<ISensorConfig>());
            return (await service.GetSourcesAsync()).ToArray();
        }

        private static OsdDesignDocument FamilyDocument(string family, string unit)
        {
            var document = new OsdDesignDocument
            {
                Name = "Core type family", IsCanvas = true, Width = 640, CanvasHeight = 500, Padding = 0, GridSize = 4
            };
            document.Tiles.Add(new OsdDesignTile
            {
                Kind = "group", Key = "role:" + family, GroupKey = "role:" + family,
                GroupDisplay = "bar", GroupBarOrientation = "vertical", GroupColumns = 4,
                GroupMembers = new List<OsdGroupMember>
                {
                    new OsdGroupMember { Key = "placeholder", Label = "Core", IsEnabled = true }
                },
                Label = family, Unit = unit, Maximum = unit == "MHz" ? 6000 : 100,
                Height = 24, X = 0, Y = 0, FrameWidth = 600, FrameHeight = 200, Padding = 8
            });
            return document;
        }

        private static void AssertLabels(OsdDesignTile tile, OverlayTelemetrySource[] catalog)
        {
            var lookup = catalog.ToDictionary(source => source.Id);
            foreach (var member in tile.GroupMembers)
            {
                var source = lookup[member.Key];
                Assert.AreEqual(source.CoreType, member.CoreType);
                Assert.AreEqual(source.CoreIndex, member.CoreIndex);
                Assert.AreEqual(source.ThreadIndex, member.ThreadIndex);
                string prefix = source.CoreType == "LPE" ? "LP-E " : source.CoreType == null ? "" : source.CoreType + " ";
                Assert.AreEqual(prefix + "C" + source.CoreIndex, member.Label);
            }
        }

        private static void AssertParity(OsdDesignDocument document, OverlayTelemetrySource[] catalog)
        {
            string original = document.ToJson();
            var saved = OverlayRuntimeDesign.Parse("core-types", document.Name, original);
            string savedAuthoring = saved.AuthoringJson;
            var runtime = OverlayRuntimeSourceBinder.Bind(saved, catalog);
            document.BindPresetSources(catalog.Select(source => new OsdTelemetrySource
            {
                Key = source.Id, Name = source.Name, Category = source.Category, Subcategory = source.Subcategory,
                DeviceName = source.HardwareName, DeviceKey = source.DeviceKey, Unit = source.Unit,
                GroupKey = source.GroupKey, GroupName = source.GroupName, GroupOrder = source.GroupOrder,
                CoreType = source.CoreType, CoreIndex = source.CoreIndex, ThreadIndex = source.ThreadIndex,
                SemanticKey = source.SemanticKey, IsAvailable = source.IsAvailable, IsText = source.IsText
            }));
            var expected = OverlayRuntimeDesign.Parse("core-types", document.Name, document.ToJson());
            AssertTokens(JToken.Parse(expected.TemplateJson), JToken.Parse(runtime.TemplateJson));
            CollectionAssert.AreEquivalent(expected.MetricKeys.ToArray(), runtime.MetricKeys.ToArray());
            Assert.AreEqual(savedAuthoring, saved.AuthoringJson, "Runtime discovery must leave the saved design unchanged.");
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

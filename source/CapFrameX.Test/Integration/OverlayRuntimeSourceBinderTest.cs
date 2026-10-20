using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using CapFrameX.Contracts.Configuration;
using CapFrameX.OSD.Controls;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json.Linq;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class OverlayRuntimeSourceBinderTest
    {
        [TestMethod]
        [DataRow(.5, 304)]
        [DataRow(1.0, 304)]
        [DataRow(2.0, 304)]
        [DataRow(.5, 32)]
        [DataRow(1.0, 32)]
        [DataRow(2.0, 32)]
        public void NestedSensorGroupsGrowTheirContainersAndBindBothMetricSources(double scale, int groupWidth)
        {
            var document = new OsdDesignDocument
            {
                Name = "Composite hardware", IsCanvas = true, Width = 640, CanvasHeight = 720,
                Padding = 4, GridSize = 4, Scale = scale
            };
            var container = new OsdDesignTile
            {
                Kind = "container", Label = "Processor", X = 0, Y = 0, FrameWidth = 320, FrameHeight = 240
            };
            var group = ThreadStrip().Tiles.Single(tile => tile.Kind == "group").Copy();
            group.X = 0; group.Y = 0; group.FrameWidth = groupWidth; group.FrameHeight = 120;
            group.GroupColumns = 8;
            group.GroupShowLabels = false;
            group.GroupShowValues = false;
            group.Height = 32;
            container.Children.Add(group);
            container.Children.Add(new OsdDesignTile
            {
                Kind = "metric", Key = "cpuClock", Unit = "GHz", ValueScale = .001,
                SecondaryKey = "ramTotal", SecondaryUnit = "GB", SecondaryScale = 1,
                X = 0, Y = 128, FrameWidth = 304, FrameHeight = 64
            });
            document.Tiles.Add(container);
            document.Tiles.Add(new OsdDesignTile { X = 0, Y = 248, FrameWidth = 320, FrameHeight = 64 });
            var catalog = Sources("cpu-loads", 128)
                .Append(new OverlayTelemetrySource { Id = "cpu/clock", SemanticKey = "cpuClock", Unit = "MHz" })
                .Append(new OverlayTelemetrySource { Id = "ram/total", SemanticKey = "ramTotal", Unit = "GB" }).ToArray();
            AssertEditorParity(document, catalog, "Nested group expansion and scaled sources");
        }

        [TestMethod]
        [DataRow(.5)]
        [DataRow(1.0)]
        [DataRow(2.0)]
        public void EveryStarterBindsTheSameSourcesAndGeometryAsTheEditor(double scale)
        {
            var catalog = Sources("cpu-loads", 32).Concat(Sources("cpu-clocks", 32, "MHz"))
                .Concat(Sources("cpu-effective-clocks", 32, "MHz")).Concat(Sources("cpu-temperatures", 32, "°C"))
                .Append(new OverlayTelemetrySource { Id = "cpu/total", Name = "CPU total", SemanticKey = "cpuLoad", Unit = "%" })
                .Append(new OverlayTelemetrySource { Id = "gpu/total", Name = "GPU total", SemanticKey = "gpuLoad", Unit = "%" }).ToArray();
            foreach (var starter in OverlayDesignStarterCatalog.Profiles)
            {
                var document = OsdDesignDocument.FromJson(starter.DesignJson);
                document.Scale = scale;
                AssertEditorParity(document, catalog, starter.PresetId);
            }
        }

        [TestMethod]
        [DataRow(true, "P", 16)]
        [DataRow(true, "e", 8)]
        [DataRow(true, "LPE", 0)]
        [DataRow(true, "X", 0)]
        [DataRow(false, "P", 0)]
        public void CoreTypeRolesBindOneCoreTypeOfTheFamilyLikeTheEditor(bool hybrid, string coreType, int expected)
        {
            var document = SizedBarDocument("vertical", 12, 32);
            var group = document.Tiles.Single(tile => tile.Kind == "group");
            group.Key = group.GroupKey = "role:cpu-loads|" + coreType;
            var catalog = hybrid ? HybridLoads() : Sources("cpu-loads", 16);
            var design = OverlayRuntimeDesign.Parse("profile", document.Name, document.ToJson());
            AssertEditorParity(document, catalog, coreType + " hybrid=" + hybrid);

            // The editor keeps the type in the bound key; without a matching core the role stays unbound.
            string type = coreType.ToUpperInvariant();
            Assert.AreEqual(expected > 0, !group.GroupKey.StartsWith("role:", StringComparison.Ordinal));
            if (expected > 0)
            {
                Assert.IsTrue(group.GroupKey.EndsWith("|" + type, StringComparison.Ordinal), group.GroupKey);
                Assert.AreEqual(expected, group.GroupMembers.Count);
                Assert.IsTrue(group.GroupMembers.All(member => member.CoreType == type));
            }
            var bound = OverlayRuntimeSourceBinder.Bind(design, catalog);
            var keys = GroupMembers(bound).Select(member => (string)member["key"]).ToArray();
            if (expected > 0)
                CollectionAssert.AreEquivalent(catalog.Where(source => source.CoreType == type).Select(source => source.Id).ToArray(), keys);
            else Assert.IsTrue(keys.All(key => key.StartsWith("role:", StringComparison.Ordinal)), "The placeholders stay unbound.");
        }

        [TestMethod]
        [DataRow("bar", "horizontal", true)]
        [DataRow("bar", "vertical", true)]
        [DataRow("bar", "vertical", false)]
        [DataRow("metric", "horizontal", true)]
        public void CustomGroupFormatsKeepEditorLayoutAndMoveFollowingTiles(string display, string orientation, bool showValues)
        {
            var document = ThreadStrip();
            var group = document.Tiles.Single(tile => tile.Kind == "group");
            group.GroupDisplay = display;
            group.GroupBarOrientation = orientation;
            group.GroupShowValues = showValues;
            group.GroupColumns = 8;
            group.Digits = 2;
            group.ValueSize = 16;
            document.GridSize = 5;
            AssertEditorParity(document, Sources("cpu-loads", 32), display + "/" + orientation + "/" + showValues);
        }

        [TestMethod]
        [DataRow("horizontal", 32, 120.0, 12.0, .5)]
        [DataRow("horizontal", 32, 120.0, 12.0, 1.0)]
        [DataRow("horizontal", 128, 120.0, 12.0, 2.0)]
        [DataRow("vertical", 32, 40.0, 32.0, .5)]
        [DataRow("vertical", 32, 40.0, 32.0, 1.0)]
        [DataRow("vertical", 128, 40.0, 32.0, 2.0)]
        public void ExplicitBarDimensionsWrapDiscoveredSourcesAndPreserveEditorGeometry(
            string orientation, int sourceCount, double barWidth, double barHeight, double scale)
        {
            var document = SizedBarDocument(orientation, barWidth, barHeight);
            document.Scale = scale;
            var group = document.Tiles.Single(tile => tile.Kind == "group");
            // A legacy column preference must not cap the explicit bar layout.
            group.GroupColumns = 1;
            var catalog = Sources("cpu-loads", sourceCount);
            var bound = OverlayRuntimeSourceBinder.Bind(OverlayRuntimeDesign.Parse("sized", "Sized bars", document.ToJson()), catalog);
            var members = GroupMembers(bound).ToArray();
            double gap = orientation == "vertical" ? 4 : 8;
            int columns = Math.Min(sourceCount, (int)Math.Floor((256 + gap) / (barWidth + gap)));
            Assert.AreEqual(sourceCount, members.Length);
            Assert.AreEqual(0, (double)members[columns]["x"], .000001, "The next bar must start a new row.");
            Assert.IsTrue((double)members[columns]["y"] > 0);
            foreach (var member in members)
            {
                Assert.AreEqual(barWidth * scale, (double)member["width"], .000001);
                Assert.AreEqual(barWidth * scale, (double)member["frameWidth"], .000001);
                Assert.AreEqual(barHeight * scale, (double)member["height"], .000001);
                Assert.IsTrue((double)member["x"] + (double)member["frameWidth"] <= 256 * scale + .000001);
            }
            AssertEditorParity(document, catalog, orientation + " fixed bars at scale " + scale);
        }

        [TestMethod]
        [DataRow("horizontal", 124.0, 2)]
        [DataRow("horizontal", 124.01, 1)]
        [DataRow("vertical", 126.0, 2)]
        [DataRow("vertical", 126.01, 1)]
        [DataRow("vertical", 320.0, 1)]
        public void ExplicitBarsWrapAtTheAvailableWidthAndClampAnOversizedSingleBar(string orientation, double width, int columns)
        {
            var document = SizedBarDocument(orientation, width, 8);
            var group = document.Tiles.Single(tile => tile.Kind == "group");
            group.GroupShowLabels = false;
            group.GroupShowValues = false;
            var catalog = Sources("cpu-loads", 32);
            var bound = OverlayRuntimeSourceBinder.Bind(OverlayRuntimeDesign.Parse("wrap", "Wrap", document.ToJson()), catalog);
            var members = GroupMembers(bound).ToArray();
            Assert.AreEqual(0, (double)members[columns]["x"], .000001);
            Assert.IsTrue((double)members[columns]["y"] > 0);
            Assert.AreEqual(Math.Min(width, 256), (double)members[0]["frameWidth"], .000001);
            AssertEditorParity(document, catalog, "Wrap boundary " + orientation + "/" + width);
        }

        [TestMethod]
        [DataRow("horizontal", true)]
        [DataRow("horizontal", false)]
        [DataRow("vertical", true)]
        [DataRow("vertical", false)]
        public void OneExplicitBarDimensionUsesTheOtherLegacyDimension(string orientation, bool widthOnly)
        {
            var document = SizedBarDocument(orientation, widthOnly ? 60 : null, widthOnly ? null : 20);
            AssertEditorParity(document, Sources("cpu-loads", 32), orientation + "/widthOnly=" + widthOnly);
        }

        [TestMethod]
        [DataRow(.5)]
        [DataRow(1.0)]
        [DataRow(2.0)]
        public void ExplicitBarsGrowNestedContainersAfter128ThreadDiscovery(double scale)
        {
            var document = SizedBarDocument("vertical", 24, 32);
            document.Scale = scale;
            var group = document.Tiles.Single(tile => tile.Kind == "group");
            var following = document.Tiles.Last();
            document.Tiles.Clear();
            var container = new OsdDesignTile
            {
                Kind = "container", Padding = 0, X = 0, Y = 0, FrameWidth = 280, FrameHeight = 176
            };
            container.Children.Add(group);
            container.Children.Add(following);
            document.Tiles.Add(container);
            document.Tiles.Add(new OsdDesignTile { X = 0, Y = 184, FrameWidth = 280, FrameHeight = 48 });
            AssertEditorParity(document, Sources("cpu-loads", 128), "Nested fixed bars at scale " + scale);
        }

        [TestMethod]
        [DataRow(.5)]
        [DataRow(1.0)]
        [DataRow(2.0)]
        public void RuntimeRejectsGrowthThatWouldExhaustTheCanvasGridMargin(double scale)
        {
            var document = SizedBarDocument("vertical", 320, 27.828125);
            document.Scale = scale;
            document.Tiles.RemoveAt(1);
            var group = document.Tiles[0];
            group.GroupShowLabels = false;
            group.GroupShowValues = false;
            var catalog = Sources("cpu-loads", 128);
            var design = OverlayRuntimeDesign.Parse("limit", "Grid margin", document.ToJson());
            string original = design.AuthoringJson;
            // The last bar fits within 4096 px, but the required trailing grid margin does not.
            Assert.ThrowsExactly<FormatException>(() => OverlayRuntimeSourceBinder.Bind(design, catalog));
            Assert.ThrowsExactly<FormatException>(() => document.BindPresetSources(catalog.Select(EditorSource)));
            Assert.AreEqual(original, design.AuthoringJson);
        }

        [TestMethod]
        public void All128ThreadsResolveInNaturalOrderWithoutChangingSavedRoles()
        {
            var document = ThreadStrip();
            var catalog = Sources("cpu-loads", 128, naturalOrder: true).Reverse().ToArray();
            var design = OverlayRuntimeDesign.Parse("profile", "Threads", document.ToJson());
            string original = design.AuthoringJson;
            var bound = OverlayRuntimeSourceBinder.Bind(design, catalog);
            var members = GroupMembers(bound).ToArray();
            Assert.AreEqual(128, members.Length);
            CollectionAssert.AreEqual(Enumerable.Range(1, 128).Select(index => "cpu0/cpu-loads/" + index).ToArray(),
                members.Select(member => (string)member["key"]).ToArray());
            Assert.AreEqual("C10", (string)members[9]["label"]);
            Assert.AreEqual(original, design.AuthoringJson);
            StringAssert.Contains(design.AuthoringJson, "role:cpu-loads/1");
            Assert.IsFalse(bound.TemplateJson.Contains("role:cpu-loads"));
            AssertEditorParity(document, catalog, "128 threads");
        }

        [TestMethod]
        public void MissingUnavailableAmbiguousOrOversizedFamiliesStayUnbound()
        {
            var design = OverlayRuntimeDesign.Parse("profile", "Threads", ThreadStrip().ToJson());
            foreach (var catalog in new[]
            {
                Array.Empty<OverlayTelemetrySource>(), Sources("cpu-loads", 1), Sources("cpu-loads", 129),
                Sources("cpu-loads", 32, available: false),
                Sources("cpu-loads", 32).Concat(Sources("cpu-loads", 32, device: "cpu1")).ToArray()
            }) Assert.AreSame(design, OverlayRuntimeSourceBinder.Bind(design, catalog));
        }

        [TestMethod]
        public void ExplicitMemberSelectionsAndLabelsAreNeverReplacedByDiscovery()
        {
            var document = ThreadStrip();
            var catalog = Sources("cpu-loads", 32);
            document.BindPresetSources(catalog.Select(EditorSource));
            var group = document.Tiles.Single(tile => tile.Kind == "group");
            group.GroupMembers[0].IsEnabled = false;
            group.GroupMembers[1].Label = "Favorite thread";
            var design = OverlayRuntimeDesign.Parse("profile", "Threads", document.ToJson());
            Assert.AreSame(design, OverlayRuntimeSourceBinder.Bind(design, Sources("cpu-loads", 64)));
            Assert.IsFalse(design.MetricKeys.Contains(catalog[0].Id));
            Assert.AreEqual(31, GroupMembers(design).Count());
            StringAssert.Contains(design.TemplateJson, "Favorite thread");
        }

        [TestMethod]
        public void DiscoverySupportsDeviceScopedGpuFamiliesAndDeduplicatesReadings()
        {
            var document = ThreadStrip();
            var group = document.Tiles.Single(tile => tile.Kind == "group");
            group.Key = group.GroupKey = "role:gpu-engines";
            var catalog = Sources("gpu-engines", 8, device: "gpu0");
            AssertEditorParity(document, catalog.Concat(catalog).ToArray(), "GPU engines");
            var bound = OverlayRuntimeSourceBinder.Bind(OverlayRuntimeDesign.Parse("id", "GPU", document.ToJson()), catalog);
            Assert.AreEqual(8, GroupMembers(bound).Count());
        }

        [TestMethod]
        public async Task DelayedDiscoveryPublishesResolvedGroupsAndNumericUpdatesDoNotRebuildTheScene()
        {
            string folder = Path.Combine(Path.GetTempPath(), "CapFrameX-runtime-source-tests", Guid.NewGuid().ToString("N"));
            try
            {
                var configuration = new Mock<IAppConfiguration>();
                configuration.SetupAllProperties();
                configuration.Object.EnableHookOverlay = true;
                configuration.Object.IsOverlayActive = true;
                using var changes = new Subject<(string, object)>();
                configuration.SetupGet(value => value.OnValueChanged).Returns(changes);
                using var telemetry = new DelayedTelemetry();
                using var pids = new Subject<int>();
                using var ticks = new Subject<long>();
                var service = new OverlayDesignService(folder, configuration.Object);
                var starter = OverlayDesignStarterCatalog.Profiles.Single(profile => profile.PresetId == "vertical-thread-strip");
                service.Activate(starter.Id);
                var channel = new Channel();
                using var publisher = new OverlayDesignRuntimePublisher(service, telemetry, configuration.Object, pids,
                    null, _ => true, () => channel, ticks);
                pids.OnNext(42);
                Assert.AreEqual(1, telemetry.Leases);
                Assert.IsTrue(publisher.Current.Design.MetricKeys.Contains("role:cpu-loads/1"));
                var ready = new TaskCompletionSource<OverlayDesignRuntimeSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
                using var subscription = publisher.Snapshots.Where(snapshot => snapshot.Design?.MetricKeys.Contains("cpu0/cpu-loads/32") == true)
                    .Take(1).Subscribe(snapshot => ready.TrySetResult(snapshot));
                var catalog = Sources("cpu-loads", 32);
                telemetry.Catalog.SetResult(catalog);
                var resolved = await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.AreEqual(32, GroupMembers(resolved.Design).Count());
                Assert.IsFalse(channel.Template.Contains("role:cpu-loads"));
                Assert.IsTrue(service.CurrentDesign.MetricKeys.Contains("role:cpu-loads/1"), "Runtime binding must not rewrite the saved profile.");
                ulong revision = resolved.DesignRevision;
                telemetry.Values.OnNext(new OverlayTelemetrySnapshot(DateTime.UtcNow,
                    catalog.ToDictionary(source => source.Id, _ => (double?)42)));
                Assert.AreSame(resolved.Design, publisher.Current.Design);
                Assert.AreEqual(revision, publisher.Current.DesignRevision);
                Assert.IsTrue(publisher.Current.MetricsRevision > resolved.MetricsRevision);
                Assert.AreEqual(42, (double)JObject.Parse(channel.Metrics)["cpu0/cpu-loads/32"]);
                ticks.OnNext(1);
                Assert.AreSame(resolved.Design, publisher.Current.Design);
                Assert.AreEqual(revision, publisher.Current.DesignRevision);
            }
            finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        }

        [TestMethod]
        [DataRow("P16", 1)]
        [DataRow("P4 E8 LPE4", 1)]
        [DataRow("P8 E16", 2)]
        [DataRow("P16 E16", 2)]
        [DataRow("P8 E12", 2)]
        [DataRow("-24", 2)]
        [DataRow("-48", 3)]
        public void HeatmapRowsKeepEveryCoreTypeInABlockLikeTheRenderer(string runs, int rows)
        {
            // Mirrors the renderer's ArrangeHeatmap cases in the OSD design test.
            string[] types = runs.Split(' ').SelectMany(run =>
            {
                int digits = run.IndexOfAny("0123456789".ToCharArray());
                return Enumerable.Repeat(run.Substring(0, digits), int.Parse(run.Substring(digits)));
            }).ToArray();
            Assert.AreEqual(rows, OverlayRuntimeSourceBinder.HeatmapRows(types));
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void HeatmapsDefaultToTenSecondsAndDesignsSavedWithTheFormerDefaultKeepIt(bool former)
        {
            var document = ThreadStrip();
            document.Tiles.Single(tile => tile.Kind == "group").GroupDisplay = "heatmap";
            string json = document.ToJson();
            Assert.IsTrue(json.Contains("\"avg:10:"), "A heatmap without a window averages over ten seconds.");
            // The editor before the 10 s default stored no window either and wrote its cells as avg:60.
            if (former) json = json.Replace("\"avg:10:", "\"avg:60:");
            int seconds = former ? 60 : 10;

            var loaded = OsdDesignDocument.FromJson(json);
            Assert.AreEqual(former ? 60 : null, loaded.Tiles.Single(tile => tile.Kind == "group").GroupWindowSeconds,
                "The editor opens the design with the window it was saved with.");
            Assert.IsTrue(loaded.ToJson().Contains($"\"avg:{seconds}:"));

            var bound = OverlayRuntimeSourceBinder.Bind(OverlayRuntimeDesign.Parse("profile", document.Name, json), Sources("cpu-loads", 24));
            var keys = JObject.Parse(bound.TemplateJson).Descendants().OfType<JObject>()
                .Single(node => (string)node["type"] == "heatmap")["keys"].Values<string>().ToArray();
            Assert.AreEqual(24, keys.Length);
            Assert.IsTrue(keys.All(key => key.StartsWith($"avg:{seconds}:", StringComparison.Ordinal)),
                "The runtime averages over the window the design was saved with.");
        }

        [TestMethod]
        public void A_HeatmapWindowNoEditorWroteStillFailsTheRoundTrip()
        {
            var document = ThreadStrip();
            document.Tiles.Single(tile => tile.Kind == "group").GroupDisplay = "heatmap";
            string json = document.ToJson().Replace("\"avg:10:", "\"avg:30:");
            Assert.ThrowsExactly<FormatException>(() => OsdDesignDocument.FromJson(json));
        }

        [TestMethod]
        [DataRow(true, 1.0)]
        [DataRow(true, 2.0)]
        [DataRow(false, 1.0)]
        public void HeatmapGroupsOrderCoreTypesAndAverageEveryCellLikeTheEditor(bool hybrid, double scale)
        {
            OsdDesignDocument Heatmap()
            {
                var document = ThreadStrip();
                document.Scale = scale;
                var group = document.Tiles.Single(tile => tile.Kind == "group");
                group.GroupDisplay = "heatmap";
                group.GroupWindowSeconds = 30;
                return document;
            }
            var catalog = hybrid ? HybridLoads() : Sources("cpu-loads", 24);
            var source = Heatmap();
            var bound = OverlayRuntimeSourceBinder.Bind(OverlayRuntimeDesign.Parse("profile", source.Name, source.ToJson()), catalog);
            var heatmap = JObject.Parse(bound.TemplateJson).Descendants().OfType<JObject>()
                .Single(node => (string)node["type"] == "heatmap");
            var expected = hybrid
                ? Enumerable.Range(1, 8).SelectMany(core => new[] { $"cpu0/cpu-loads/{core}/1", $"cpu0/cpu-loads/{core}/2" })
                    .Concat(Enumerable.Range(9, 8).Select(core => $"cpu0/cpu-loads/{core}/0"))
                : Enumerable.Range(1, 24).Select(index => "cpu0/cpu-loads/" + index);
            // P-cores first with both threads of a core together, although discovery listed the E-cores first.
            CollectionAssert.AreEqual(expected.Select(key => "avg:30:" + key).ToArray(), heatmap["keys"].Values<string>().ToArray());
            if (hybrid)
                CollectionAssert.AreEqual(Enumerable.Repeat("P", 16).Concat(Enumerable.Repeat("E", 8)).ToArray(),
                    heatmap["types"].Values<string>().ToArray());
            else Assert.IsNull(heatmap["types"], "Readings without a core class carry no types.");
            AssertEditorParity(Heatmap(), catalog, "heatmap hybrid=" + hybrid + " scale=" + scale);
        }

        // Eight hyper-threaded P-cores and eight E-cores, reported E-cores first.
        private static OverlayTelemetrySource[] HybridLoads()
        {
            var readings = Enumerable.Range(1, 8).SelectMany(core => new[] { 1, 2 }.Select(thread => (Type: "P", Core: core, Thread: thread)))
                .Concat(Enumerable.Range(9, 8).Select(core => (Type: "E", Core: core, Thread: 0))).Reverse().ToArray();
            return readings.Select((reading, index) => new OverlayTelemetrySource
            {
                Id = $"cpu0/cpu-loads/{reading.Core}/{reading.Thread}", DeviceKey = "cpu0", HardwareName = "cpu0",
                Name = reading.Thread > 0 ? $"Core #{reading.Core} {reading.Type} Thread #{reading.Thread}" : $"Core #{reading.Core} {reading.Type}",
                GroupKey = "cpu0/cpu-loads", Category = "CPU", Unit = "%", GroupOrder = index, IsAvailable = true,
                CoreType = reading.Type, CoreIndex = reading.Core, ThreadIndex = reading.Thread
            }).ToArray();
        }

        private static OsdDesignDocument ThreadStrip() => OsdDesignDocument.FromJson(
            OverlayDesignStarterCatalog.Profiles.Single(profile => profile.PresetId == "vertical-thread-strip").DesignJson);

        private static OsdDesignDocument SizedBarDocument(string orientation, double? width, double? height)
        {
            var group = ThreadStrip().Tiles.Single(tile => tile.Kind == "group").Copy();
            group.X = 0;
            group.Y = 0;
            group.FrameWidth = 256;
            group.FrameHeight = 112;
            group.Padding = 0;
            group.GroupBarOrientation = orientation;
            group.GroupBarWidth = width;
            group.GroupBarHeight = height;
            var document = new OsdDesignDocument
            {
                Name = "Sized bars", IsCanvas = true, Width = 640, CanvasHeight = 720,
                Padding = 0, GridSize = 4
            };
            document.Tiles.Add(group);
            document.Tiles.Add(new OsdDesignTile { X = 0, Y = 120, FrameWidth = 256, FrameHeight = 48 });
            return document;
        }

        private static IEnumerable<JToken> GroupMembers(OverlayRuntimeDesign design) => JObject.Parse(design.TemplateJson)["root"]["children"]
            .Select(tile => tile["children"][0]).Where(widget => (string)widget["type"] == "panel")
            .SelectMany(widget => widget["children"][1]["children"]);

        private static OverlayTelemetrySource[] Sources(string family, int count, string unit = "%", string device = "cpu0",
            bool available = true, bool naturalOrder = false) => Enumerable.Range(1, count).Select(index => new OverlayTelemetrySource
            {
                Id = device + "/" + family + "/" + index, DeviceKey = device, HardwareName = device,
                Name = "CPU Core #" + index, GroupKey = device + "/" + family, Category = "CPU", Unit = unit,
                GroupOrder = naturalOrder ? 0 : index, IsAvailable = available
            }).ToArray();

        private static OsdTelemetrySource EditorSource(OverlayTelemetrySource source) => new OsdTelemetrySource
        {
            Key = source.Id, Name = source.Name, DeviceKey = source.DeviceKey, DeviceName = source.HardwareName,
            Category = source.Category, Unit = source.Unit, GroupKey = source.GroupKey, GroupOrder = source.GroupOrder,
            IsAvailable = source.IsAvailable, IsText = source.IsText, SemanticKey = source.SemanticKey,
            CoreType = source.CoreType, CoreIndex = source.CoreIndex, ThreadIndex = source.ThreadIndex
        };

        private static void AssertEditorParity(OsdDesignDocument document, OverlayTelemetrySource[] catalog, string context)
        {
            string savedJson = document.ToJson();
            var design = OverlayRuntimeDesign.Parse("profile", document.Name, savedJson);
            string originalAuthoringJson = design.AuthoringJson;
            var actual = OverlayRuntimeSourceBinder.Bind(design, catalog);
            document.BindPresetSources(catalog.Select(EditorSource));
            var expected = OverlayRuntimeDesign.Parse("profile", document.Name, document.ToJson());
            AssertTokens(JToken.Parse(expected.TemplateJson), JToken.Parse(actual.TemplateJson), context);
            Assert.AreEqual(originalAuthoringJson, design.AuthoringJson);
            CollectionAssert.AreEqual(expected.MetricKeys.ToArray(), actual.MetricKeys.ToArray(), context);
        }

        private static void AssertTokens(JToken expected, JToken actual, string context)
        {
            Assert.IsNotNull(actual, context + ": " + expected.Path);
            if (expected.Type == JTokenType.Integer || expected.Type == JTokenType.Float)
            {
                Assert.AreEqual((double)expected, (double)actual, 0.0000001, context + ": " + expected.Path);
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

        private sealed class DelayedTelemetry : IOverlayTelemetryService
        {
            public readonly TaskCompletionSource<IReadOnlyList<OverlayTelemetrySource>> Catalog = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public readonly Subject<OverlayTelemetrySnapshot> Values = new();
            public int Leases;
            public IObservable<OverlayTelemetrySnapshot> Snapshots => Values;
            public IDisposable AcquirePreview() { Leases++; return Disposable.Create(() => Leases--); }
            public Task<IReadOnlyList<OverlayTelemetrySource>> GetSourcesAsync() => Catalog.Task;
            public void Dispose() => Values.Dispose();
        }

        private sealed class Channel : IHookDesignChannel
        {
            public string Template, Metrics;
            public void Publish(int targetPid, string templateJson, string metricsJson, ulong designRevision, ulong metricsRevision)
            { Template = templateJson; Metrics = metricsJson; }
            public void Disable() { }
            public void Dispose() { }
        }
    }
}

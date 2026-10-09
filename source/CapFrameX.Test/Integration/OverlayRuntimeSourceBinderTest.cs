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
                    null, null, _ => true, () => channel, ticks);
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

        private static OsdDesignDocument ThreadStrip() => OsdDesignDocument.FromJson(
            OverlayDesignStarterCatalog.Profiles.Single(profile => profile.PresetId == "vertical-thread-strip").DesignJson);

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
            IsAvailable = source.IsAvailable, IsText = source.IsText, SemanticKey = source.SemanticKey
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

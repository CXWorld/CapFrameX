using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using CapFrameX.Contracts.Configuration;
using CapFrameX.OSD.Controls;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class OverlayDesignRuntimeTest
    {
        private string _folder;
        private Mock<IAppConfiguration> _configuration;
        private Subject<(string key, object value)> _changes;

        [TestInitialize]
        public void Initialize()
        {
            _folder = Path.Combine(Path.GetTempPath(), "CapFrameX-runtime-design-tests", Guid.NewGuid().ToString("N"));
            _configuration = new Mock<IAppConfiguration>();
            _configuration.SetupAllProperties();
            _configuration.Object.EnableHookOverlay = true;
            _changes = new Subject<(string, object)>();
            _configuration.SetupGet(config => config.OnValueChanged).Returns(_changes);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _changes.Dispose();
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        [TestMethod]
        public void ActivationIsPersistedIndependentlyOfTheLastEditedProfile()
        {
            var store = Store();
            var first = store.Create("First", Scene("fps"));
            var second = store.Create("Second", Scene("gpuLoad"));
            var service = Service();
            Assert.IsFalse(service.IsEnabled);
            service.Activate(first.Id);
            Assert.AreEqual(first.Id, _configuration.Object.ActiveOverlayDesignProfileId);
            Assert.AreEqual(second.Id, Store().ActiveProfileId);
            Assert.AreEqual(first.Id, Service().ActiveProfileId);
            service.Deactivate();
            Assert.IsNull(_configuration.Object.ActiveOverlayDesignProfileId);
            Assert.IsFalse(Service().IsEnabled);
            Assert.AreEqual(second.Id, Store().ActiveProfileId);
        }

        [TestMethod]
        public void SavedEditsRefreshTheActiveSceneAndDeletingItRestoresClassic()
        {
            var store = Store();
            var first = store.Create("First", Scene("fps"));
            store.Create("Second", Scene("cpuLoad"));
            var service = Service();
            service.Activate(first.Id);
            store.Save(first.Id, Scene("gpuLoad"));
            service.RefreshProfiles();
            CollectionAssert.AreEqual(new[] { "gpuLoad" }, service.CurrentDesign.MetricKeys.ToArray());
            store.Delete(first.Id);
            service.RefreshProfiles();
            Assert.IsFalse(service.IsEnabled);
            Assert.IsNull(_configuration.Object.ActiveOverlayDesignProfileId);
            StringAssert.Contains(service.Status, "deleted");
        }

        [TestMethod]
        public void BrokenLibraryRetainsPreviouslyValidatedDesignAndDoesNotOverwriteFiles()
        {
            var profile = Store().Create("First", Scene("fps"));
            var service = Service();
            service.Activate(profile.Id);
            string path = Path.Combine(_folder, "OverlayDesigns", "profiles.json");
            File.WriteAllText(path, "invalid");
            service.RefreshProfiles();
            Assert.AreEqual(profile.Id, service.ActiveProfileId);
            Assert.AreEqual("invalid", File.ReadAllText(path));
            Assert.AreEqual(profile.Id, _configuration.Object.ActiveOverlayDesignProfileId);
            Assert.IsFalse(Service().IsEnabled, "A fresh service has no previously validated scene to retain.");
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("not-a-profile-id")]
        public void EmptyOrMalformedPersistedSelectionKeepsTheClassicOverlay(string selected)
        {
            Store().Create("First", Scene("fps"));
            _configuration.Object.ActiveOverlayDesignProfileId = selected;
            var service = Service();
            Assert.IsFalse(service.IsEnabled);
            Assert.IsNull(service.ActiveProfileId);
            Assert.AreEqual(OverlayDesignStarterCatalog.Profiles.Count + 1, service.Profiles.Count);
            Assert.IsTrue(string.IsNullOrEmpty(_configuration.Object.ActiveOverlayDesignProfileId));
        }

        [TestMethod]
        public void RuntimeRecoveryPreservesLibraryWarningsAndDoesNotHideNewLibraryFailures()
        {
            var store = Store();
            var profile = store.Create("First", Scene("fps"));
            store.Save(profile.Id, Scene("cpuLoad"));
            _configuration.Object.ActiveOverlayDesignProfileId = profile.Id;
            string path = Path.Combine(_folder, "OverlayDesigns", "profiles.json");
            File.WriteAllText(path, "invalid primary");
            var service = Service();
            StringAssert.Contains(service.Status, "recovered");
            string warning = service.Status;
            service.ReportRuntimeError("Transient channel error");
            service.ReportRuntimeError("Another transient channel error");
            service.ReportRuntimeReady();
            Assert.AreEqual(warning, service.Status);
            service.ReportRuntimeError("Transient channel error");
            File.WriteAllText(path + ".bak", "invalid backup");
            service.RefreshProfiles();
            string failure = service.Status;
            StringAssert.Contains(failure, "could not be loaded");
            service.ReportRuntimeReady();
            Assert.AreEqual(failure, service.Status);
        }

        [TestMethod]
        public void OnlyRenderedWidgetsRequestKeysAndEditorMetadataIsNotPublished()
        {
            var document = JObject.Parse(Scene("enabled/thread/1"));
            document["editor"] = new JObject { ["hidden"] = "editor-private" };
            document["root"]["children"][0]["editor"] = new JObject
            {
                ["groupMembers"] = new JArray(new JObject { ["key"] = "disabled/thread/2", ["isEnabled"] = false })
            };
            ((JArray)document["root"]["children"]).Add(new JObject { ["type"] = "chart", ["series"] = "displaytimes" });
            ((JArray)document["root"]["children"]).Add(new JObject { ["type"] = "chart", ["series"] = "framerates" });
            var design = OverlayRuntimeDesign.Parse("id", "Test", document.ToString());
            CollectionAssert.AreEqual(new[] { "enabled/thread/1" }, design.MetricKeys.ToArray());
            Assert.IsTrue(design.NeedsDisplayTimes);
            Assert.IsTrue(design.NeedsFrametimes);
            Assert.IsFalse(design.TemplateJson.Contains("editor"));
            Assert.IsFalse(design.TemplateJson.Contains("disabled/thread/2"));
        }

        [TestMethod]
        [DataRow(32, true)]
        [DataRow(32, false)]
        [DataRow(128, true)]
        [DataRow(128, false)]
        public void SavedVerticalGroupsKeepTheirGeometryAndPublishOnlyEnabledReadings(int memberCount, bool showValues)
        {
            var document = new OsdDesignDocument { Name = "Vertical loads", Columns = 1, Width = 960, Scale = 1.25 };
            var group = new OsdDesignTile
            {
                Kind = "group", Key = "cpu/loads", GroupKey = "cpu/loads", Label = "CPU threads", Unit = "%",
                GroupDisplay = "bar", GroupColumns = 8, GroupBarOrientation = "vertical", GroupShowValues = showValues,
                Height = 48, ValueSize = 14,
                GroupMembers = Enumerable.Range(0, memberCount).Select(index => new OsdGroupMember
                {
                    Key = "cpu/thread/" + index, Label = "T" + index, IsEnabled = index != 0
                }).ToList()
            };
            document.Tiles.Add(group);
            document.EnableCanvas();
            var editorStore = new OverlayDesignProfileStore(_folder, json => OsdDesignDocument.FromJson(json).ToJson());
            var saved = editorStore.Create(document.Name, document.ToJson());
            var reopened = new OverlayDesignProfileStore(_folder, json => OsdDesignDocument.FromJson(json).ToJson()).Get(saved.Id);
            var reopenedGroup = OsdDesignDocument.FromJson(reopened.DesignJson).Tiles.Single();
            Assert.AreEqual("vertical", reopenedGroup.GroupBarOrientation);
            Assert.AreEqual(showValues, reopenedGroup.GroupShowValues ?? true);
            Assert.AreEqual(memberCount, reopenedGroup.GroupMembers.Count);
            Assert.IsFalse(reopenedGroup.GroupMembers[0].IsEnabled);

            var service = Service();
            service.Activate(saved.Id);
            Assert.AreEqual(saved.Id, Service().ActiveProfileId, "The active vertical design must survive application restart.");
            _configuration.Object.EnableHookOverlay = true;
            _configuration.Object.IsOverlayActive = true;
            using var telemetry = new FakeTelemetry();
            using var pids = new Subject<int>();
            using var ticks = new Subject<long>();
            var channels = new List<FakeChannel>();
            using var publisher = Publisher(service, telemetry, pids, ticks, channels);
            pids.OnNext(42);
            var values = Enumerable.Range(0, memberCount).ToDictionary(index => "cpu/thread/" + index, index => (double?)index);
            values["cpu/thread/1"] = null;
            telemetry.Emit(new OverlayTelemetrySnapshot(DateTime.UtcNow, values));

            var channel = channels.Single();
            var template = JObject.Parse(channel.Template);
            var bars = template.Descendants().OfType<JObject>().Where(node => (string)node["type"] == "bar").ToArray();
            var savedBars = JObject.Parse(reopened.DesignJson).Descendants().OfType<JObject>()
                .Where(node => (string)node["type"] == "bar").ToArray();
            Assert.AreEqual(memberCount - 1, bars.Length);
            for (int index = 0; index < bars.Length; index++)
            {
                Assert.IsTrue(JToken.DeepEquals(savedBars[index], bars[index]), "Publication must preserve native group geometry and labels.");
                Assert.AreEqual("vertical", (string)bars[index]["orientation"]);
                Assert.AreEqual(60d, (double)bars[index]["height"]);
                Assert.AreEqual(showValues, (bool)bars[index]["showValue"]);
            }
            Assert.IsFalse(template.Descendants().OfType<JProperty>().Any(property => property.Name == "editor"));
            Assert.AreEqual(service.CurrentDesign.TemplateJson, publisher.Current.Design.TemplateJson,
                "Hook-free and in-game output must consume the same compiled group.");
            var metrics = JObject.Parse(channel.Metrics);
            Assert.AreEqual(memberCount - 1, metrics.Count);
            Assert.IsNull(metrics["cpu/thread/0"], "Excluded members must stay out of the runtime payload.");
            Assert.AreEqual(JTokenType.Null, metrics["cpu/thread/1"].Type, "A missing reading must remain unavailable, not zero.");
            Assert.AreEqual(memberCount - 1, (int)metrics["cpu/thread/" + (memberCount - 1)]);
        }

        [TestMethod]
        public void MetricsBindSemanticAliasesKeepTextAndRepresentMissingReadingsAsNull()
        {
            var document = JObject.Parse(Scene("cpuLoad"));
            var children = (JArray)document["root"]["children"];
            foreach (string key in new[] { "processName", "missing", "nonfinite" })
                children.Add(new JObject { ["type"] = "metric", ["key"] = key });
            var design = OverlayRuntimeDesign.Parse("id", "Test", document.ToString());
            var snapshot = new OverlayTelemetrySnapshot(DateTime.UtcNow,
                new Dictionary<string, double?> { ["hardware/cpu/load"] = 42, ["nonfinite"] = double.NaN, ["unused"] = 100 },
                new Dictionary<string, object> { ["processName"] = "Game α" });
            var metrics = JObject.Parse(design.CreateMetrics(snapshot, new Dictionary<string, string> { ["cpuLoad"] = "hardware/cpu/load" }));
            Assert.AreEqual(42, (int)metrics["cpuLoad"]);
            Assert.AreEqual("Game α", (string)metrics["processName"]);
            Assert.AreEqual(JTokenType.Null, metrics["missing"].Type);
            Assert.AreEqual(JTokenType.Null, metrics["nonfinite"].Type);
            Assert.IsNull(metrics["unused"]);
        }

        [TestMethod]
        public void RuntimeParserRejectsMalformedUnboundedAndDuplicateScenes()
        {
            Assert.ThrowsExactly<InvalidDataException>(() => OverlayRuntimeDesign.Parse("id", "Bad", "{\"version\":2,\"root\":{}}"));
            Assert.ThrowsExactly<JsonReaderException>(() => OverlayRuntimeDesign.Parse("id", "Bad", "{\"version\":1,\"version\":1,\"root\":{}}"));
            var document = JObject.Parse(Scene("fps"));
            ((JArray)document["root"]["children"]).Clear();
            for (int index = 0; index < 512; index++) ((JArray)document["root"]["children"]).Add(new JObject { ["type"] = "text" });
            Assert.ThrowsExactly<InvalidDataException>(() => OverlayRuntimeDesign.Parse("id", "Bad", document.ToString()));
            Assert.ThrowsExactly<ArgumentException>(() => HookDesignChannel.Encode(new string('ü', 524289)));
            Assert.ThrowsExactly<ArgumentException>(() => HookDesignChannel.Encode("{}\0"));
            Assert.ThrowsExactly<InvalidDataException>(() => OverlayRuntimeDesign.Parse("id", "Bad", "{\"version\":{},\"root\":{}}"));
            Assert.ThrowsExactly<InvalidDataException>(() => OverlayRuntimeDesign.Parse("id", "Bad", "{\"version\":1,\"root\":{\"type\":[]}}"));
            Assert.ThrowsExactly<InvalidDataException>(() => OverlayRuntimeDesign.Parse("id", "Bad", "{\"version\":1,\"root\":{\"type\":\"metric\",\"key\":[]}}"));
        }

        [TestMethod]
        public void PublisherDemandsTelemetryOnlyForVisibleAllowedSavedDesignAndClearsOnHide()
        {
            var service = ActiveService();
            _configuration.Object.IsOverlayActive = false;
            using var telemetry = new FakeTelemetry();
            using var pids = new Subject<int>();
            using var ticks = new Subject<long>();
            var channels = new List<FakeChannel>();
            _configuration.Object.EnableHookOverlay = true;
            using var publisher = Publisher(service, telemetry, pids, ticks, channels);
            pids.OnNext(42);
            Assert.AreEqual(0, telemetry.Leases);
            _changes.OnNext((nameof(IAppConfiguration.IsOverlayActive), true));
            Assert.AreEqual(1, telemetry.Leases);
            Assert.AreEqual(42, channels.Last().Pid);
            telemetry.Emit(51);
            Assert.AreEqual(51, (int)JObject.Parse(channels.Last().Metrics)["cpuLoad"]);
            ulong revision = channels.Last().MetricsRevision;
            int count = channels.Last().PublishCount;
            ticks.OnNext(1);
            Assert.IsTrue(channels.Last().PublishCount > count);
            Assert.AreEqual(revision, channels.Last().MetricsRevision, "A heartbeat must not force unchanged telemetry to be reparsed.");
            _changes.OnNext((nameof(IAppConfiguration.IsOverlayActive), false));
            Assert.AreEqual(0, telemetry.Leases);
            Assert.AreEqual(0, channels.Last().Pid);
            Assert.IsTrue(channels.Last().Disposed);
            Assert.AreEqual(JTokenType.Null, JObject.Parse(publisher.Current.MetricsJson)["cpuLoad"].Type);
        }

        [TestMethod]
        public void BlockedTargetsAndClassicModeNeverOpenTheDesignChannel()
        {
            var service = ActiveService();
            _configuration.Object.EnableHookOverlay = true;
            _configuration.Object.IsOverlayActive = true;
            using var telemetry = new FakeTelemetry();
            using var pids = new Subject<int>();
            using var ticks = new Subject<long>();
            var channels = new List<FakeChannel>();
            using var publisher = Publisher(service, telemetry, pids, ticks, channels, allowed: _ => false);
            pids.OnNext(42);
            ticks.OnNext(1);
            Assert.AreEqual(0, telemetry.Leases);
            Assert.AreEqual(0, channels.Count);
            service.Deactivate();
            Assert.IsNull(publisher.Current.Design);
            Assert.AreEqual(0, channels.Count);
        }

        [TestMethod]
        public void SelectingRtssDisablesThePublishedDesignChannelAndTelemetryDemand()
        {
            using var service = ActiveService();
            _configuration.Object.IsOverlayActive = true;
            using var telemetry = new FakeTelemetry();
            using var pids = new Subject<int>();
            using var ticks = new Subject<long>();
            var channels = new List<FakeChannel>();
            using var publisher = Publisher(service, telemetry, pids, ticks, channels);
            pids.OnNext(42);
            telemetry.Emit(50);
            Assert.AreEqual(1, telemetry.Leases);
            Assert.AreEqual(42, channels.Last().Pid);
            Assert.IsNotNull(publisher.Current.Design);

            _configuration.Object.EnableHookOverlay = false;
            _changes.OnNext((nameof(IAppConfiguration.EnableHookOverlay), false));
            Assert.IsFalse(service.IsEnabled);
            Assert.IsNull(_configuration.Object.ActiveOverlayDesignProfileId);
            Assert.IsNull(publisher.Current.Design);
            Assert.AreEqual(0, telemetry.Leases);
            Assert.AreEqual(0, channels.Last().Pid);
            Assert.IsTrue(channels.Last().Disposed);
            int count = channels.Count;
            ticks.OnNext(1);
            Assert.AreEqual(count, channels.Count, "A heartbeat must not recreate the design channel under RTSS.");
        }

        [TestMethod]
        public void TargetSwitchClearsValuesAndRejectsSnapshotsFromBeforeTheSwitch()
        {
            var service = ActiveService();
            _configuration.Object.EnableHookOverlay = true;
            _configuration.Object.IsOverlayActive = true;
            using var telemetry = new FakeTelemetry();
            using var pids = new Subject<int>();
            using var ticks = new Subject<long>();
            var channels = new List<FakeChannel>();
            using var publisher = Publisher(service, telemetry, pids, ticks, channels);
            pids.OnNext(42);
            telemetry.Emit(25);
            DateTime previousTarget = DateTime.UtcNow.AddSeconds(-1);
            pids.OnNext(43);
            Assert.AreEqual(43, channels.Last().Pid);
            Assert.AreEqual(JTokenType.Null, JObject.Parse(channels.Last().Metrics)["cpuLoad"].Type);
            telemetry.Emit(99, previousTarget);
            Assert.AreEqual(JTokenType.Null, JObject.Parse(channels.Last().Metrics)["cpuLoad"].Type);
            telemetry.Emit(70);
            Assert.AreEqual(70, (int)JObject.Parse(channels.Last().Metrics)["cpuLoad"]);
        }

        [TestMethod]
        public void HookFreeFallbackUsesProcessGateAndDoesNotCreateInGameChannel()
        {
            var service = ActiveService();
            _configuration.Object.IsOverlayActive = true;
            using var telemetry = new FakeTelemetry();
            using var pids = new Subject<int>();
            using var ticks = new Subject<long>();
            using var counts = new BehaviorSubject<int>(0);
            using var fallback = new BehaviorSubject<bool>(true);
            var channels = new List<FakeChannel>();
            using var publisher = Publisher(service, telemetry, pids, ticks, channels, counts, fallback);
            Assert.AreEqual(0, telemetry.Leases);
            counts.OnNext(1);
            Assert.AreEqual(1, telemetry.Leases);
            Assert.AreEqual(0, channels.Count);
            counts.OnNext(0);
            Assert.AreEqual(0, telemetry.Leases);
        }

        [TestMethod]
        public void NewWriterRevisionsCannotReuseThePreviousWritersInitialScene()
        {
            var service = ActiveService();
            using var telemetry = new FakeTelemetry();
            using var pids = new Subject<int>();
            using var ticks = new Subject<long>();
            var channels = new List<FakeChannel>();
            ulong first;
            using (var publisher = Publisher(service, telemetry, pids, ticks, channels)) first = publisher.Current.DesignRevision;
            using var restarted = Publisher(service, telemetry, pids, ticks, channels);
            Assert.AreNotEqual(first, restarted.Current.DesignRevision);
            Assert.AreNotEqual(0UL, restarted.Current.DesignRevision);
        }

        [TestMethod]
        public void ChannelFailureReportsStatusWithoutRecursiveRetries()
        {
            var service = ActiveService();
            _configuration.Object.EnableHookOverlay = true;
            _configuration.Object.IsOverlayActive = true;
            using var telemetry = new FakeTelemetry();
            using var pids = new Subject<int>();
            using var ticks = new Subject<long>();
            int attempts = 0;
            using var publisher = new OverlayDesignRuntimePublisher(service, telemetry, _configuration.Object, pids, null, null,
                _ => true, () => { attempts++; throw new IOException("Simulated denied channel"); }, ticks);
            pids.OnNext(42);
            Assert.AreEqual(1, attempts);
            StringAssert.Contains(service.Status, "Simulated denied channel");
            ticks.OnNext(1);
            Assert.AreEqual(2, attempts);
        }

        [TestMethod]
        public void FailedTelemetrySubscriptionReleasesItsLeaseAndRecoversOnRetry()
        {
            var service = ActiveService();
            _configuration.Object.EnableHookOverlay = true;
            _configuration.Object.IsOverlayActive = true;
            using var telemetry = new FakeTelemetry { FailSubscription = true };
            using var pids = new Subject<int>();
            using var ticks = new Subject<long>();
            var channels = new List<FakeChannel>();
            using var publisher = Publisher(service, telemetry, pids, ticks, channels);
            pids.OnNext(42);
            Assert.AreEqual(0, telemetry.Leases);
            Assert.AreEqual(0, channels.Count);
            StringAssert.Contains(service.Status, "telemetry stream");
            telemetry.FailSubscription = false;
            ticks.OnNext(1);
            Assert.AreEqual(1, telemetry.Leases);
            Assert.AreEqual(42, channels.Last().Pid);
            StringAssert.StartsWith(service.Status, "Tile overlay:");
        }

        [TestMethod]
        public void RecoveredChannelClearsOnlyItsTransientRuntimeError()
        {
            var service = ActiveService();
            _configuration.Object.EnableHookOverlay = true;
            _configuration.Object.IsOverlayActive = true;
            using var telemetry = new FakeTelemetry();
            using var pids = new Subject<int>();
            using var ticks = new Subject<long>();
            int attempts = 0;
            using var publisher = new OverlayDesignRuntimePublisher(service, telemetry, _configuration.Object, pids, null, null,
                _ => true, () => ++attempts == 1 ? throw new IOException("Transient failure") : new FakeChannel(), ticks);
            pids.OnNext(42);
            StringAssert.Contains(service.Status, "Transient failure");
            ticks.OnNext(1);
            StringAssert.StartsWith(service.Status, "Tile overlay:");
            Assert.AreEqual(2, attempts);
        }

        [TestMethod]
        public void SharedMemoryPublishesExactUtf8LayoutAndDisableIsImmediatelyVisible()
        {
            string name = @"Local\CfxDesignTest_" + Guid.NewGuid().ToString("N");
            using var writer = HookDesignChannel.Create(name);
            using var map = MemoryMappedFile.OpenExisting(name, MemoryMappedFileRights.Read);
            using var reader = map.CreateViewAccessor(0, HookDesignChannel.MapSize, MemoryMappedFileAccess.Read);
            string scene = Scene("cpu/ü");
            string metrics = "{\"cpu/ü\":32}";
            writer.Publish(4242, scene, metrics, 100, 101);
            Assert.AreEqual(0x31445843u, reader.ReadUInt32(0));
            Assert.AreEqual(1, reader.ReadInt32(4));
            Assert.AreEqual(0, reader.ReadInt32(8) & 1);
            Assert.AreEqual(4242, reader.ReadInt32(12));
            Assert.AreEqual(1, reader.ReadInt32(16));
            Assert.AreEqual(Encoding.UTF8.GetByteCount(scene), reader.ReadInt32(20));
            Assert.AreEqual(Encoding.UTF8.GetByteCount(metrics), reader.ReadInt32(24));
            Assert.IsTrue(reader.ReadInt64(32) > 0);
            Assert.AreEqual(100UL, reader.ReadUInt64(40));
            Assert.AreEqual(101UL, reader.ReadUInt64(48));
            var bytes = new byte[reader.ReadInt32(24)];
            reader.ReadArray(64 + 1048576, bytes, 0, bytes.Length);
            Assert.AreEqual(metrics, Encoding.UTF8.GetString(bytes));
            writer.Disable();
            Assert.AreEqual(0, reader.ReadInt32(12));
            Assert.AreEqual(0, reader.ReadInt32(16));
            Assert.AreEqual(0, reader.ReadInt64(32));
            writer.Publish(4243, scene, metrics, 100, 101);
            Assert.AreEqual(4243, reader.ReadInt32(12));
            Assert.AreEqual(Encoding.UTF8.GetByteCount(scene), reader.ReadInt32(20));
        }

        private OverlayDesignProfileStore Store()
        {
            var store = new OverlayDesignProfileStore(_folder, OverlayRuntimeDesign.Canonicalize);
            OverlayDesignStarterCatalog.EnsureProfiles(store);
            return store;
        }
        private OverlayDesignService Service() => new OverlayDesignService(_folder, _configuration.Object);
        private OverlayDesignService ActiveService()
        {
            var profile = Store().Create("CPU", Scene("cpuLoad"));
            var service = Service();
            service.Activate(profile.Id);
            return service;
        }

        private OverlayDesignRuntimePublisher Publisher(OverlayDesignService service, FakeTelemetry telemetry,
            IObservable<int> pids, IObservable<long> ticks, List<FakeChannel> channels,
            IObservable<int> counts = null, IObservable<bool> fallback = null, Func<int, bool> allowed = null)
            => new OverlayDesignRuntimePublisher(service, telemetry, _configuration.Object, pids, counts, fallback,
                allowed ?? (_ => true), () => { var channel = new FakeChannel(); channels.Add(channel); return channel; }, ticks);

        private static string Scene(string key) => new JObject
        {
            ["version"] = 1, ["name"] = "Test", ["root"] = new JObject
            {
                ["type"] = "panel", ["children"] = new JArray(new JObject { ["type"] = "metric", ["key"] = key })
            }
        }.ToString(Formatting.None);

        private sealed class FakeTelemetry : IOverlayTelemetryService
        {
            private readonly Subject<OverlayTelemetrySnapshot> _snapshots = new Subject<OverlayTelemetrySnapshot>();
            public int Leases { get; private set; }
            public bool FailSubscription;
            public IObservable<OverlayTelemetrySnapshot> Snapshots => FailSubscription
                ? Observable.Throw<OverlayTelemetrySnapshot>(new IOException("Simulated telemetry failure")) : _snapshots;
            public IDisposable AcquirePreview() { Leases++; return Disposable.Create(() => Leases--); }
            public Task<IReadOnlyList<OverlayTelemetrySource>> GetSourcesAsync() => Task.FromResult<IReadOnlyList<OverlayTelemetrySource>>(new[]
            {
                new OverlayTelemetrySource { Id = "cpu/total", SemanticKey = "cpuLoad", IsAvailable = true }
            });
            public void Emit(double load, DateTime? timestamp = null) => _snapshots.OnNext(new OverlayTelemetrySnapshot(timestamp ?? DateTime.UtcNow,
                new Dictionary<string, double?> { ["cpu/total"] = load }));
            public void Emit(OverlayTelemetrySnapshot snapshot) => _snapshots.OnNext(snapshot);
            public void Dispose() => _snapshots.Dispose();
        }

        private sealed class FakeChannel : IHookDesignChannel
        {
            public int Pid, PublishCount;
            public bool Disposed;
            public string Template, Metrics;
            public ulong MetricsRevision;
            public void Publish(int targetPid, string templateJson, string metricsJson, ulong designRevision, ulong metricsRevision)
            {
                Pid = targetPid; Template = templateJson; Metrics = metricsJson; MetricsRevision = metricsRevision; PublishCount++;
            }
            public void Disable() => Pid = 0;
            public void Dispose() { Disable(); Disposed = true; }
        }
    }
}

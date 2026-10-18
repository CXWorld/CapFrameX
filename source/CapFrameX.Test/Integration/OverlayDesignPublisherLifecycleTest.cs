using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class OverlayDesignPublisherLifecycleTest
    {
        [TestMethod]
        public void HeatmapCellsPublishTheSlidingAverageOfTheirSource()
        {
            using var context = new PublisherContext("cpuLoad");
            string heatmap = new JObject
            {
                ["version"] = 1, ["name"] = "Heatmap",
                ["root"] = new JObject
                {
                    ["type"] = "panel",
                    ["children"] = new JArray(new JObject { ["type"] = "heatmap", ["keys"] = new JArray("avg:60:cpu/1", "avg:60:cpu/2") })
                }
            }.ToString(Formatting.None);
            context.Service.Activate(context.Store().Create("Heatmap", heatmap).Id);

            context.Telemetry.Emit(("cpu/1", 20), ("cpu/2", 90));
            context.Telemetry.Emit(("cpu/1", 40));

            Assert.AreEqual(30, (double)context.Metrics["avg:60:cpu/1"], 1e-9);
            Assert.AreEqual(90, (double)context.Metrics["avg:60:cpu/2"], 1e-9, "A snapshot without the reading does not count.");
            Assert.AreEqual(context.Publisher.Current.MetricsJson, context.Channel.Metrics, "The hook receives the averages.");
            CollectionAssert.IsSubsetOf(new[] { "cpu/1", "cpu/2" },
                OverlayDesignRuntimePublisher.GetRequiredSourceIds(context.Publisher.Current.Design, null).ToArray(),
                "The telemetry lease polls the averaged sources.");
        }

        [TestMethod]
        public void ShutdownWithdrawsTheHookRowFeedBeforeTheDesign()
        {
            using var context = new PublisherContext("cpuLoad");
            var channel = context.Channel;
            Assert.AreEqual(42, channel.Pid, "The hook must be showing the design when the app exits.");
            using var entries = new Subject<IOverlayEntry[]>();
            var overlay = new Mock<IOverlayService>();
            overlay.SetupGet(service => service.OnDictionaryUpdated).Returns(entries);
            var rowConfiguration = new Mock<IAppConfiguration>();
            rowConfiguration.SetupGet(configuration => configuration.OnValueChanged)
                .Returns(Observable.Never<(string key, object value)>());
            var rowFeed = new HookMetricsPublisher(overlay.Object, rowConfiguration.Object, Observable.Never<int>());
            Assert.IsTrue(entries.HasObservers);
            bool? rowFeedLiveAtWithdrawal = null;
            channel.Withdrawing = () => rowFeedLiveAtWithdrawal = entries.HasObservers;

            OverlayRendererShutdown.Run(rowFeed, context.Publisher);

            // Without a design the hook draws the rows it still holds, i.e. the row overlay.
            Assert.IsTrue(channel.Disposed);
            Assert.AreEqual(false, rowFeedLiveAtWithdrawal, "The row feed must be gone before the design is withdrawn.");
        }

        [TestMethod]
        public void ShutdownDisposesEveryRendererInOrderEvenWhenOneFails()
        {
            var order = new List<string>();

            OverlayRendererShutdown.Run(new ShutdownProbe("rows", order, fail: true), new ShutdownProbe("design", order),
                new ShutdownProbe("frametimes", order), null, new ShutdownProbe("hook", order, fail: true),
                new ShutdownProbe("hook-free", order));

            CollectionAssert.AreEqual(new[] { "rows", "design", "frametimes", "hook", "hook-free" }, order);
        }

        [TestMethod]
        public async Task DelayedDiscoveryPublishesTheLatestSampleWithoutRestartingTheStream()
        {
            using var context = new PublisherContext("cpuLoad");
            context.Telemetry.Emit(("cpu/total", 31));
            context.Telemetry.Emit(("cpu/total", 67));
            Assert.AreEqual(JTokenType.Null, context.Metrics["cpuLoad"].Type);
            ulong designRevision = context.Publisher.Current.DesignRevision;
            ulong metricsRevision = context.Publisher.Current.MetricsRevision;

            await context.Telemetry.CompleteCatalogAsync(0, CpuCatalog("cpu/total"));

            Assert.AreEqual(67, (int)context.Metrics["cpuLoad"]);
            Assert.AreEqual(designRevision, context.Publisher.Current.DesignRevision);
            Assert.IsTrue(context.Publisher.Current.MetricsRevision > metricsRevision);
            Assert.AreEqual(context.Publisher.Current.MetricsJson, context.Channel.Metrics);
            Assert.AreEqual(1, context.Telemetry.Acquisitions);
            Assert.AreEqual(1, context.Telemetry.ActiveSubscriptions);
        }

        [TestMethod]
        public async Task CompletingDiscoveryWhileHiddenCannotRestoreDemandOrPublishValues()
        {
            using var context = new PublisherContext("cpuLoad");
            context.Telemetry.Emit(("cpu/total", 82));
            var oldChannel = context.Channel;
            context.SetVisible(false);
            var hidden = context.Publisher.Current;
            int updateCount = 0;
            using var subscription = context.Publisher.Snapshots.Subscribe(_ => updateCount++);

            await context.Telemetry.CompleteCatalogAsync(0, CpuCatalog("cpu/total"));
            context.Telemetry.Emit(("cpu/total", 95));
            context.Ticks.OnNext(1);

            Assert.AreEqual(0, context.Telemetry.Leases);
            Assert.AreEqual(0, context.Telemetry.ActiveSubscriptions);
            Assert.AreEqual(1, context.Telemetry.Acquisitions);
            Assert.AreEqual(2, updateCount, "Only the initial snapshot and explicit heartbeat should be observed.");
            Assert.AreEqual(hidden.MetricsJson, context.Publisher.Current.MetricsJson);
            Assert.AreEqual(hidden.MetricsRevision, context.Publisher.Current.MetricsRevision);
            Assert.AreEqual(JTokenType.Null, context.Metrics["cpuLoad"].Type);
            Assert.IsTrue(oldChannel.Disposed);
            Assert.AreEqual(1, context.Channels.Count);
        }

        [TestMethod]
        public async Task DiscoveryFromAnOldDemandCannotOverwriteTheRestartedCatalog()
        {
            using var context = new PublisherContext("cpuLoad");
            context.SetVisible(false);
            context.SetVisible(true);
            context.Telemetry.Emit(("old/cpu", 99), ("new/cpu", 24));
            await context.Telemetry.CompleteCatalogAsync(1, CpuCatalog("new/cpu"));
            var current = context.Publisher.Current;
            Assert.AreEqual(24, (int)context.Metrics["cpuLoad"]);

            await context.Telemetry.CompleteCatalogAsync(0, CpuCatalog("old/cpu"));
            context.Ticks.OnNext(1);

            Assert.AreEqual(24, (int)context.Metrics["cpuLoad"]);
            Assert.AreEqual(current.DesignRevision, context.Publisher.Current.DesignRevision);
            Assert.AreEqual(current.MetricsRevision, context.Publisher.Current.MetricsRevision);
            Assert.AreEqual(2, context.Telemetry.Acquisitions);
            Assert.AreEqual(2, context.Telemetry.CatalogRequests.Count);
            Assert.AreEqual(1, context.Telemetry.Leases);
            Assert.AreEqual(1, context.Telemetry.ActiveSubscriptions);
        }

        [TestMethod]
        public async Task CompletingDiscoveryAfterDisposalCannotPublishOrReacquireResources()
        {
            using var context = new PublisherContext("cpuLoad");
            int updates = 0;
            int completions = 0;
            using var subscription = context.Publisher.Snapshots.Subscribe(_ => updates++, () => completions++);
            context.Telemetry.Emit(("cpu/total", 48));
            var last = context.Publisher.Current;
            var channel = context.Channel;
            int publications = channel.Publications;
            context.Publisher.Dispose();
            int updatesBeforeCompletion = updates;

            await context.Telemetry.CompleteCatalogAsync(0, CpuCatalog("cpu/total"));
            context.Telemetry.Emit(("cpu/total", 73));
            context.Ticks.OnNext(1);
            context.Pids.OnNext(43);
            context.SetVisible(false);
            context.SetVisible(true);
            context.Publisher.Dispose();

            Assert.AreSame(last, context.Publisher.Current);
            Assert.AreEqual(updatesBeforeCompletion, updates);
            Assert.AreEqual(1, completions);
            Assert.AreEqual(publications, channel.Publications);
            Assert.IsTrue(channel.Disposed);
            Assert.AreEqual(0, context.Telemetry.Leases);
            Assert.AreEqual(0, context.Telemetry.ActiveSubscriptions);
            Assert.AreEqual(1, context.Telemetry.Acquisitions);
            Assert.AreEqual(1, context.Channels.Count);
        }

        [TestMethod]
        public async Task ProfileSwitchDuringDiscoveryBindsOnlyTheSelectedProfilesLatestValues()
        {
            using var context = new PublisherContext("cpuLoad");
            context.Telemetry.Emit(("cpu/total", 42), ("gpu/total", 76));
            ulong firstRevision = context.Publisher.Current.DesignRevision;
            string gpuProfile = context.AddProfile("GPU profile", "gpuLoad");
            context.Service.Activate(gpuProfile);
            Assert.IsTrue(context.Publisher.Current.DesignRevision > firstRevision);

            await context.Telemetry.CompleteCatalogAsync(0, new[]
            {
                new OverlayTelemetrySource { Id = "cpu/total", SemanticKey = "cpuLoad", IsHardwareSensor = true },
                new OverlayTelemetrySource { Id = "gpu/total", SemanticKey = "gpuLoad", IsHardwareSensor = true }
            });

            Assert.AreEqual(gpuProfile, context.Publisher.Current.Design.ProfileId);
            CollectionAssert.AreEqual(new[] { "gpuLoad" }, context.Metrics.Properties().Select(property => property.Name).ToArray());
            Assert.AreEqual(76, (int)context.Metrics["gpuLoad"]);
            StringAssert.Contains(context.Channel.Template, "gpuLoad");
            Assert.AreEqual(context.Publisher.Current.DesignRevision, context.Channel.DesignRevision);
            Assert.AreEqual(1, context.Telemetry.Acquisitions);
            Assert.AreEqual(1, context.Telemetry.CatalogRequests.Count);
        }

        [TestMethod]
        public async Task RepeatedValuesAndUnchangedProfileRefreshKeepRevisionsStable()
        {
            using var context = new PublisherContext("cpuLoad");
            await context.Telemetry.CompleteCatalogAsync(0, CpuCatalog("cpu/total"));
            context.Telemetry.Emit(("cpu/total", 45));
            var first = context.Publisher.Current;

            context.Telemetry.Emit(("cpu/total", 45));
            context.Ticks.OnNext(1);
            context.Service.RefreshProfiles();

            Assert.AreEqual(first.DesignRevision, context.Publisher.Current.DesignRevision);
            Assert.AreEqual(first.MetricsRevision, context.Publisher.Current.MetricsRevision);
            context.Telemetry.Emit(("cpu/total", 46));
            Assert.AreEqual(first.DesignRevision, context.Publisher.Current.DesignRevision);
            Assert.AreEqual(first.MetricsRevision + 1, context.Publisher.Current.MetricsRevision);
            Assert.AreEqual(context.Publisher.Current.MetricsRevision, context.Channel.MetricsRevision);
            Assert.AreEqual(1, context.Telemetry.Acquisitions);
        }

        [TestMethod]
        public async Task SavingTheActiveProfileReplacesItsMetricKeysWithoutRestartingSampling()
        {
            using var context = new PublisherContext("cpuLoad");
            await context.Telemetry.CompleteCatalogAsync(0, new[]
            {
                new OverlayTelemetrySource { Id = "cpu/total", SemanticKey = "cpuLoad", IsHardwareSensor = true },
                new OverlayTelemetrySource { Id = "gpu/total", SemanticKey = "gpuLoad", IsHardwareSensor = true }
            });
            context.Telemetry.Emit(("cpu/total", 36), ("gpu/total", 88));
            var previous = context.Publisher.Current;
            context.Store().Save(previous.Design.ProfileId, Scene("gpuLoad"));
            context.Service.RefreshProfiles();

            Assert.AreEqual(previous.Design.ProfileId, context.Publisher.Current.Design.ProfileId);
            Assert.AreEqual(previous.DesignRevision + 1, context.Publisher.Current.DesignRevision);
            Assert.AreEqual(previous.MetricsRevision + 1, context.Publisher.Current.MetricsRevision);
            Assert.AreEqual(88, (int)context.Metrics["gpuLoad"]);
            Assert.IsNull(context.Metrics["cpuLoad"]);
            Assert.AreEqual(context.Publisher.Current.MetricsJson, context.Channel.Metrics);
            Assert.AreEqual(1, context.Telemetry.Acquisitions);
            Assert.AreEqual(1, context.Telemetry.CatalogRequests.Count);
        }

        [TestMethod]
        public async Task FailedCatalogDiscoveryKeepsDirectReadingsAndRecoversOnTheNextDemand()
        {
            using var context = new PublisherContext("cpu/total", "cpuLoad");
            context.Telemetry.Emit(("cpu/total", 57));
            await context.Telemetry.FailCatalogAsync(0, new IOException("Sensor discovery interrupted"));

            Assert.AreEqual(57, (int)context.Metrics["cpu/total"]);
            Assert.AreEqual(JTokenType.Null, context.Metrics["cpuLoad"].Type);
            Assert.AreEqual(1, context.Telemetry.Leases);
            context.SetVisible(false);
            context.SetVisible(true);
            Assert.AreEqual(JTokenType.Null, context.Metrics["cpu/total"].Type,
                "A newly acquired stream must not reuse a sample retained by a stopped stream.");
            await context.Telemetry.CompleteCatalogAsync(1, CpuCatalog("cpu/total"));
            context.Telemetry.Emit(("cpu/total", 62));

            Assert.AreEqual(62, (int)context.Metrics["cpu/total"]);
            Assert.AreEqual(62, (int)context.Metrics["cpuLoad"]);
            Assert.AreEqual(2, context.Telemetry.Acquisitions);
            Assert.AreEqual(1, context.Telemetry.Leases);
            Assert.AreEqual(1, context.Telemetry.ActiveSubscriptions);
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task StreamFailureReleasesTheLeaseAndRecoveryRestoresTheActiveProfileStatus(bool inGame)
        {
            using var context = new PublisherContext(inGame, "cpuLoad");
            await context.Telemetry.CompleteCatalogAsync(0, CpuCatalog("old/cpu"));
            context.Telemetry.Emit(("old/cpu", 70));
            string healthyStatus = context.Service.Status;
            // The hook-free bridge acknowledges successful application even when a
            // restart snapshot contains only null values and no new sensor reading.
            using var successfulRenderer = inGame ? Disposable.Empty
                : context.Publisher.Snapshots.Subscribe(_ => context.Publisher.ReportRendererReady());
            context.Telemetry.FailStream(new IOException("Temporary sensor disconnect"));

            Assert.AreEqual(0, context.Telemetry.Leases);
            Assert.AreEqual(0, context.Telemetry.ActiveSubscriptions);
            if (inGame) Assert.AreEqual(0, context.Channel.Pid);
            else Assert.AreEqual(0, context.Channels.Count);
            StringAssert.Contains(context.Service.Status, "Temporary sensor disconnect");
            context.Ticks.OnNext(1);
            Assert.AreEqual(JTokenType.Null, context.Metrics["cpuLoad"].Type);
            StringAssert.Contains(context.Service.Status, "Temporary sensor disconnect",
                "Restarting the subscription alone must not claim that the telemetry stream has recovered.");
            await context.Telemetry.CompleteCatalogAsync(1, CpuCatalog("new/cpu"));
            StringAssert.Contains(context.Service.Status, "Temporary sensor disconnect",
                "Catalog discovery does not replace the missing sensor sample.");
            context.Telemetry.Emit(("new/cpu", 29));

            Assert.AreEqual(29, (int)context.Metrics["cpuLoad"]);
            if (inGame) Assert.AreEqual(42, context.Channel.Pid);
            else Assert.AreEqual(0, context.Channels.Count);
            Assert.AreEqual(2, context.Telemetry.Acquisitions);
            Assert.AreEqual(1, context.Telemetry.Leases);
            Assert.AreEqual(1, context.Telemetry.ActiveSubscriptions);
            Assert.AreEqual(healthyStatus, context.Service.Status,
                "Once fresh readings are published, a previous telemetry error must no longer mask the active profile status.");
        }

        [TestMethod]
        public async Task TelemetryRecoveryPreservesANewRendererFailureReportedByTheSnapshotConsumer()
        {
            using var context = new PublisherContext(false, "cpuLoad");
            await context.Telemetry.CompleteCatalogAsync(0, CpuCatalog("cpu/total"));
            context.Telemetry.Emit(("cpu/total", 70));
            string healthyStatus = context.Service.Status;
            context.Telemetry.FailStream(new IOException("Temporary sensor disconnect"));
            context.Ticks.OnNext(1);
            await context.Telemetry.CompleteCatalogAsync(1, CpuCatalog("cpu/total"));
            int rendererFailures = 0;
            using var renderer = context.Publisher.Snapshots.Subscribe(snapshot =>
            {
                if ((double?)JObject.Parse(snapshot.MetricsJson)["cpuLoad"] != 29) return;
                rendererFailures++;
                context.Publisher.ReportRendererError("Renderer rejected recovery");
            });

            context.Telemetry.Emit(("cpu/total", 29));

            Assert.AreEqual(1, rendererFailures);
            Assert.AreEqual(29, (int)context.Metrics["cpuLoad"]);
            Assert.AreEqual(0, context.Channels.Count);
            Assert.AreEqual("Renderer rejected recovery", context.Service.Status,
                "Telemetry recovery must not clear a newer failure synchronously reported while consuming the recovered snapshot.");

            renderer.Dispose();
            context.Publisher.ReportRendererReady();

            Assert.AreEqual(healthyStatus, context.Service.Status,
                "Once the renderer also recovers, restore the active profile status rather than the superseded telemetry error.");
        }

        [TestMethod]
        public async Task TelemetryLease_ReportsTheSourcesOfTheActiveDesign()
        {
            // The telemetry computes online metrics and CapFrameX values only for these sources.
            using var context = new PublisherContext("cpuLoad");
            CollectionAssert.AreEquivalent(new[] { "cpuLoad" }, context.Telemetry.RequiredSources.ToArray());

            await context.Telemetry.CompleteCatalogAsync(0, CpuCatalog("cpu/total"));
            CollectionAssert.AreEquivalent(new[] { "cpuLoad", "cpu/total" }, context.Telemetry.RequiredSources.ToArray(),
                "The alias the design resolves through is a source it reads.");

            context.Service.Activate(context.AddProfile("GPU profile", "gpuLoad", "metric/OnlineP1"));
            CollectionAssert.AreEquivalent(new[] { "gpuLoad", "metric/OnlineP1" }, context.Telemetry.RequiredSources.ToArray(),
                "A design change swaps the sources without a new lease.");
            Assert.AreEqual(1, context.Telemetry.Acquisitions);
        }

        [TestMethod]
        public void RequiredSourceIds_ResolveKeysTheWayTheMetricsDo()
        {
            var design = OverlayRuntimeDesign.Parse("profile", "Test", Scene("cpuLoad", "metric/Ping", "unknownKey"));
            var aliases = new Dictionary<string, string> { ["cpuLoad"] = "sensor:cpu-total", ["other"] = "sensor:other" };

            var required = OverlayDesignRuntimePublisher.GetRequiredSourceIds(design, aliases);

            CollectionAssert.AreEquivalent(new[] { "cpuLoad", "sensor:cpu-total", "metric/Ping", "unknownKey" },
                required.ToArray());
            Assert.AreEqual(0, OverlayDesignRuntimePublisher.GetRequiredSourceIds(null, aliases).Count);
            CollectionAssert.AreEquivalent(new[] { "cpuLoad", "metric/Ping", "unknownKey" },
                OverlayDesignRuntimePublisher.GetRequiredSourceIds(design, null).ToArray());
        }

        private static IReadOnlyList<OverlayTelemetrySource> CpuCatalog(string id) => new[]
        {
            new OverlayTelemetrySource { Id = id, SemanticKey = "cpuLoad", IsHardwareSensor = true }
        };

        private static string Scene(params string[] keys) => new JObject
        {
            ["version"] = 1,
            ["name"] = "Lifecycle test",
            ["root"] = new JObject
            {
                ["type"] = "panel",
                ["children"] = new JArray(keys.Select(key => new JObject { ["type"] = "metric", ["key"] = key }))
            }
        }.ToString(Formatting.None);

        private sealed class PublisherContext : IDisposable
        {
            private readonly string _folder = Path.Combine(Path.GetTempPath(), "CapFrameX-publisher-lifecycle-tests", Guid.NewGuid().ToString("N"));
            private readonly Mock<IAppConfiguration> _configuration = new Mock<IAppConfiguration>();
            private readonly Subject<(string key, object value)> _changes = new Subject<(string, object)>();
            public readonly ControlledTelemetry Telemetry = new ControlledTelemetry();
            public readonly Subject<int> Pids = new Subject<int>();
            public readonly Subject<long> Ticks = new Subject<long>();
            public readonly List<RecordingChannel> Channels = new List<RecordingChannel>();
            public readonly OverlayDesignService Service;
            public readonly OverlayDesignRuntimePublisher Publisher;
            public RecordingChannel Channel => Channels.Last();
            public JObject Metrics => JObject.Parse(Publisher.Current.MetricsJson);

            public PublisherContext(params string[] keys) : this(true, keys)
            {
            }

            public PublisherContext(bool inGame, params string[] keys)
            {
                _configuration.SetupAllProperties();
                _configuration.SetupGet(configuration => configuration.OnValueChanged).Returns(_changes);
                _configuration.Object.EnableHookOverlay = inGame;
                _configuration.Object.EnableHookFreeOverlay = !inGame;
                string profile = AddProfile("Lifecycle profile", keys);
                Service = new OverlayDesignService(_folder, _configuration.Object);
                Service.Activate(profile);
                Publisher = new OverlayDesignRuntimePublisher(Service, Telemetry, _configuration.Object, Pids,
                    null, _ => true, () =>
                    {
                        var channel = new RecordingChannel();
                        Channels.Add(channel);
                        return channel;
                    }, Ticks);
                Pids.OnNext(42);
            }

            public OverlayDesignProfileStore Store() => new OverlayDesignProfileStore(_folder, OverlayRuntimeDesign.Canonicalize);
            public string AddProfile(string name, params string[] keys) => Store().Create(name, Scene(keys)).Id;

            public void SetVisible(bool visible)
            {
                _configuration.Object.IsOverlayActive = visible;
                _changes.OnNext((nameof(IAppConfiguration.IsOverlayActive), visible));
            }

            public void Dispose()
            {
                Publisher.Dispose();
                Service.Dispose();
                Telemetry.Dispose();
                Pids.Dispose();
                Ticks.Dispose();
                _changes.Dispose();
                if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
            }
        }

        private sealed class ControlledTelemetry : IOverlayTelemetryService
        {
            private Subject<OverlayTelemetrySnapshot> _snapshots = new Subject<OverlayTelemetrySnapshot>();
            private readonly List<Subject<OverlayTelemetrySnapshot>> _streams = new List<Subject<OverlayTelemetrySnapshot>>();
            public readonly List<TaskCompletionSource<IReadOnlyList<OverlayTelemetrySource>>> CatalogRequests
                = new List<TaskCompletionSource<IReadOnlyList<OverlayTelemetrySource>>>();
            public int Acquisitions { get; private set; }
            public int Leases { get; private set; }
            public int ActiveSubscriptions { get; private set; }

            public ControlledTelemetry() => _streams.Add(_snapshots);

            public IObservable<OverlayTelemetrySnapshot> Snapshots => Observable.Create<OverlayTelemetrySnapshot>(observer =>
            {
                ActiveSubscriptions++;
                var subscription = _snapshots.Subscribe(observer);
                return Disposable.Create(() =>
                {
                    subscription.Dispose();
                    ActiveSubscriptions--;
                });
            });

            public IDisposable AcquirePreview()
            {
                Acquisitions++;
                Leases++;
                return Disposable.Create(() => Leases--);
            }

            private Func<IReadOnlyCollection<string>> _requiredSources;

            // The sources the publisher's lease reports right now.
            public IReadOnlyCollection<string> RequiredSources => _requiredSources?.Invoke();

            public IDisposable AcquirePreview(Func<IReadOnlyCollection<string>> requiredSourceIds)
            {
                _requiredSources = requiredSourceIds;
                return AcquirePreview();
            }

            public Task<IReadOnlyList<OverlayTelemetrySource>> GetSourcesAsync()
            {
                // Inline continuations make completion an explicit barrier: when the producer
                // task finishes, discovery and its synchronous publication have both finished.
                var request = new TaskCompletionSource<IReadOnlyList<OverlayTelemetrySource>>();
                CatalogRequests.Add(request);
                return request.Task;
            }

            public Task CompleteCatalogAsync(int index, IReadOnlyList<OverlayTelemetrySource> sources)
                => Task.Run(() => CatalogRequests[index].SetResult(sources));

            public Task FailCatalogAsync(int index, Exception error)
                => Task.Run(() => CatalogRequests[index].SetException(error));

            public void Emit(params (string id, double value)[] values) => _snapshots.OnNext(
                new OverlayTelemetrySnapshot(DateTime.UtcNow, values.ToDictionary(value => value.id, value => (double?)value.value)));

            public void FailStream(Exception error)
            {
                var previous = _snapshots;
                _snapshots = new Subject<OverlayTelemetrySnapshot>();
                _streams.Add(_snapshots);
                previous.OnError(error);
            }

            public void Dispose()
            {
                foreach (var stream in _streams) stream.Dispose();
            }
        }

        private sealed class RecordingChannel : IHookDesignChannel
        {
            public int Pid, Publications;
            public bool Disposed;
            public string Template, Metrics;
            public ulong DesignRevision, MetricsRevision;

            public void Publish(int targetPid, string templateJson, string metricsJson, ulong designRevision, ulong metricsRevision)
            {
                Pid = targetPid;
                Template = templateJson;
                Metrics = metricsJson;
                DesignRevision = designRevision;
                MetricsRevision = metricsRevision;
                Publications++;
            }

            public Action Withdrawing;

            public void Disable()
            {
                if (Pid != 0) Withdrawing?.Invoke();
                Pid = 0;
            }

            public void Dispose()
            {
                Disable();
                Disposed = true;
            }
        }

        private sealed class ShutdownProbe : IDisposable
        {
            private readonly string _name;
            private readonly List<string> _order;
            private readonly bool _fail;

            public ShutdownProbe(string name, List<string> order, bool fail = false)
            {
                _name = name;
                _order = order;
                _fail = fail;
            }

            public void Dispose()
            {
                _order.Add(_name);
                if (_fail) throw new InvalidOperationException(_name + " failed");
            }
        }
    }
}

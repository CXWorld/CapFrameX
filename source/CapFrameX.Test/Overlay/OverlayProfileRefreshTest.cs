using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Logging;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Overlay;
using CapFrameX.Sensor;
using CapFrameX.Statistics.NetStandard.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CapFrameX.Test.Overlay
{
    [TestClass]
    public class OverlayProfileRefreshTest
    {
        [TestMethod]
        public async Task RequestRefresh_PublishesCompletedProfileWithoutWaitingForSensorTick()
        {
            using var fixture = new RefreshFixture();
            var oldEntries = CreateEntries("Old profile");
            var newEntries = CreateEntries("New profile");
            fixture.Provider.Setup(provider => provider.GetOverlayEntries(true))
                .ReturnsAsync(oldEntries);
            var firstUpdate = fixture.Service.OnDictionaryUpdated.Take(1).ToTask();
            fixture.Start();
            Assert.AreSame(oldEntries, await firstUpdate.WaitAsync(TimeSpan.FromSeconds(5)));

            var readStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var pendingEntries = new TaskCompletionSource<IOverlayEntry[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Provider.Setup(provider => provider.GetOverlayEntries(true))
                .Returns(() =>
                {
                    readStarted.TrySetResult(true);
                    return pendingEntries.Task;
                });
            var nextUpdate = fixture.Service.OnDictionaryUpdated
                .Select(entries => (Published: entries, Current: fixture.Service.CurrentOverlayEntries))
                .Take(1).ToTask();

            fixture.Service.RequestRefresh();
            await readStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsFalse(nextUpdate.IsCompleted,
                "Renderers must not receive a tick with the old profile while the new entries are loading.");
            Assert.AreSame(oldEntries, fixture.Service.CurrentOverlayEntries);

            pendingEntries.SetResult(newEntries);
            var update = await nextUpdate.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreSame(newEntries, update.Published);
            Assert.AreSame(newEntries, update.Current,
                "The current display list must be replaced before renderer callbacks run.");
            fixture.Rtss.Verify(service => service.ReleaseOSD(), Times.Never);
        }

        [TestMethod]
        public async Task RefreshRequests_ReadProfilesInOrder()
        {
            using var fixture = new RefreshFixture();
            var firstEntries = new TaskCompletionSource<IOverlayEntry[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstReadStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondReadStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var oldEntries = CreateEntries("Old profile");
            var newEntries = CreateEntries("New profile");
            fixture.Provider.SetupSequence(provider => provider.GetOverlayEntries(true))
                .Returns(() =>
                {
                    firstReadStarted.TrySetResult(true);
                    return firstEntries.Task;
                })
                .Returns(() =>
                {
                    secondReadStarted.TrySetResult(true);
                    return Task.FromResult(newEntries);
                });
            var updates = fixture.Service.OnDictionaryUpdated.Take(2).ToArray().ToTask();
            fixture.Start();

            // The first read can still be pending when another profile update is requested.
            await firstReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            fixture.Service.RequestRefresh();
            firstEntries.SetResult(oldEntries);

            await secondReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var published = await updates.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreSame(oldEntries, published[0]);
            Assert.AreSame(newEntries, published[1]);
            Assert.AreSame(newEntries, fixture.Service.CurrentOverlayEntries);
        }

        [TestMethod]
        public async Task RefreshWhileHidden_KeepsOverlayHiddenAndLoadsNewProfileOnActivation()
        {
            using var fixture = new RefreshFixture();
            var oldEntries = CreateEntries("Old profile");
            var newEntries = CreateEntries("New profile");
            fixture.Provider.Setup(provider => provider.GetOverlayEntries(true))
                .ReturnsAsync(oldEntries);
            var firstUpdate = fixture.Service.OnDictionaryUpdated.Take(1).ToTask();
            fixture.Start();
            await firstUpdate.WaitAsync(TimeSpan.FromSeconds(5));

            var activeStates = new ConcurrentQueue<bool>();
            using var activeSubscription = fixture.Service.IsOverlayActiveStream.Subscribe(activeStates.Enqueue);
            fixture.Configuration.Object.IsOverlayActive = false;
            fixture.Service.IsOverlayActiveStream.OnNext(false);
            fixture.Provider.Setup(provider => provider.GetOverlayEntries(true))
                .ReturnsAsync(newEntries);
            fixture.Service.RequestRefresh();

            Assert.IsFalse(fixture.Service.IsOverlayActive);
            CollectionAssert.AreEqual(new[] { true, false }, activeStates.ToArray());

            var nextUpdate = fixture.Service.OnDictionaryUpdated.Take(1).ToTask();
            fixture.Configuration.Object.IsOverlayActive = true;
            fixture.Service.IsOverlayActiveStream.OnNext(true);
            Assert.AreSame(newEntries, await nextUpdate.WaitAsync(TimeSpan.FromSeconds(5)));
        }

        [TestMethod]
        public async Task RemoteDemandWhileOverlayIsOff_RefreshesEntriesWithoutRtss()
        {
            using var fixture = new RefreshFixture(overlayActive: false, rtssRenderer: true);
            var entries = CreateEntries("Remote");
            fixture.Provider.Setup(provider => provider.GetOverlayEntries(true))
                .ReturnsAsync(entries);
            var update = fixture.Service.OnDictionaryUpdated.Take(1).ToTask();
            fixture.Start();

            fixture.Demand.RegisterRequest();

            Assert.AreSame(entries, await update.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.AreSame(entries, fixture.Service.CurrentOverlayEntries);
            fixture.Rtss.Verify(service => service.CheckRTSSRunning(), Times.Never);
            fixture.Rtss.Verify(service => service.OnOSDOn(), Times.Never);
            fixture.Rtss.Verify(service => service.SetOverlayEntries(It.IsAny<IOverlayEntry[]>()), Times.Never);
        }

        [TestMethod]
        public async Task SwitchingOverlayOffWithRemoteDemand_KeepsRefreshingAndStopsFeedingRtss()
        {
            using var fixture = new RefreshFixture(overlayActive: true, rtssRenderer: true);
            int rtssFeeds = 0;
            var firstFeed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Rtss.Setup(service => service.SetOverlayEntries(It.IsAny<IOverlayEntry[]>()))
                .Callback(() =>
                {
                    Interlocked.Increment(ref rtssFeeds);
                    firstFeed.TrySetResult(true);
                });
            fixture.Provider.Setup(provider => provider.GetOverlayEntries(true))
                .ReturnsAsync(CreateEntries("Overlay"));
            fixture.Start();
            // Without sensor ticks the start is the only update of the active overlay.
            await firstFeed.Task.WaitAsync(TimeSpan.FromSeconds(5));

            fixture.Demand.RegisterRequest();
            var remoteEntries = CreateEntries("Remote");
            fixture.Provider.Setup(provider => provider.GetOverlayEntries(true))
                .ReturnsAsync(remoteEntries);
            var updates = fixture.Service.OnDictionaryUpdated.Take(2).ToArray().ToTask();
            fixture.Configuration.Object.IsOverlayActive = false;
            fixture.Service.IsOverlayActiveStream.OnNext(false);
            fixture.Service.RequestRefresh();

            // The second update is only delivered once the first one has been handled completely.
            var published = await updates.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsTrue(published.All(entries => entries == remoteEntries));
            Assert.AreEqual(1, Volatile.Read(ref rtssFeeds), "The released RTSS slot must not be fed again.");
            fixture.Rtss.Verify(service => service.ReleaseOSD(), Times.Once);
        }

        [TestMethod]
        public async Task RemoteLeaseExpiry_SwitchesTheFeedOff()
        {
            using var fixture = new RefreshFixture(overlayActive: false, rtssRenderer: true);
            fixture.Provider.Setup(provider => provider.GetOverlayEntries(true))
                .ReturnsAsync(CreateEntries("Remote"));
            var remoteUpdate = fixture.Service.OnDictionaryUpdated.Take(1).ToTask();
            fixture.Demand.RegisterRequest();
            fixture.Start();
            await remoteUpdate.WaitAsync(TimeSpan.FromSeconds(5));
            int releases = CountCalls(fixture.Rtss, nameof(IRTSSService.ReleaseOSD));
            var laterUpdate = fixture.Service.OnDictionaryUpdated.Take(1).ToTask();

            // The lease timer runs synchronously on the virtual clock, and so does the mode switch.
            fixture.DemandClock.AdvanceBy(RemoteOverlayDemand.DefaultRequestLease);
            Assert.AreEqual(releases + 1, CountCalls(fixture.Rtss, nameof(IRTSSService.ReleaseOSD)),
                "The expired lease must switch the feed to Off.");

            fixture.Service.RequestRefresh();
            await Task.WhenAny(laterUpdate, Task.Delay(TimeSpan.FromMilliseconds(200)));
            Assert.IsFalse(laterUpdate.IsCompleted, "Without an overlay or a remote client nothing is refreshed.");
        }

        [TestMethod]
        public async Task SwitchingOverlayOnDuringRemoteFeed_InitializesAndFeedsRtss()
        {
            using var fixture = new RefreshFixture(overlayActive: false, rtssRenderer: true);
            var fed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Rtss.Setup(service => service.SetOverlayEntries(It.IsAny<IOverlayEntry[]>()))
                .Callback(() => fed.TrySetResult(true));
            fixture.Provider.Setup(provider => provider.GetOverlayEntries(true))
                .ReturnsAsync(CreateEntries("Entries"));
            var remoteUpdate = fixture.Service.OnDictionaryUpdated.Take(1).ToTask();
            fixture.Demand.RegisterRequest();
            fixture.Start();
            await remoteUpdate.WaitAsync(TimeSpan.FromSeconds(5));
            fixture.Rtss.Verify(service => service.CheckRTSSRunning(), Times.Never);

            fixture.Configuration.Object.IsOverlayActive = true;
            fixture.Service.IsOverlayActiveStream.OnNext(true);

            await fed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            fixture.Rtss.Verify(service => service.CheckRTSSRunning(), Times.Once);
            fixture.Rtss.Verify(service => service.OnOSDOn(), Times.Once);
        }

        [TestMethod]
        public async Task SensorTickWithRemoteDemand_UpdatesSensorEntriesWhileOverlayIsOff()
        {
            using var fixture = new RefreshFixture(overlayActive: false);
            var sensor = new SensorEntry
            {
                Identifier = "/intelcpu/0/temperature/0",
                Name = "CPU Package",
                SensorType = "Temperature",
                HardwareType = "Cpu",
                HardwareName = "Test CPU"
            };
            fixture.Provider.Setup(provider => provider.GetOverlayEntries(true))
                .ReturnsAsync(CreateEntries("Remote"));
            fixture.Snapshots.OnNext(Snapshot(sensor, 10f));
            fixture.Demand.RegisterRequest();
            fixture.Start(sensor);
            await WaitUntil(() => fixture.OsdTicks.HasObservers, "The sensor refresh never subscribed.");

            // The first tick always passes; it seeds the entries at startup.
            fixture.OsdTicks.OnNext(TimeSpan.FromHours(1));
            await WaitUntil(() => SensorValue(fixture, sensor) == 10f, "The first tick was not applied.");

            fixture.Snapshots.OnNext(Snapshot(sensor, 20f));
            fixture.OsdTicks.OnNext(TimeSpan.FromHours(1));
            await WaitUntil(() => SensorValue(fixture, sensor) == 20f,
                "A tick must reach the sensor entries while a remote client reads them.");
        }

        private static (DateTime, Dictionary<ISensorEntry, float>) Snapshot(ISensorEntry sensor, float value)
            => (DateTime.UtcNow, new Dictionary<ISensorEntry, float> { [sensor] = value });

        private static float? SensorValue(RefreshFixture fixture, ISensorEntry sensor)
            => fixture.Service.GetSensorOverlayEntry(sensor.Identifier)?.Value as float?;

        private static int CountCalls<T>(Mock<T> mock, string methodName) where T : class
            => mock.Invocations.Count(invocation => invocation.Method.Name == methodName);

        private static async Task WaitUntil(Func<bool> condition, string message)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
            while (!condition())
            {
                if (DateTime.UtcNow > deadline)
                    Assert.Fail(message);
                await Task.Delay(10);
            }
        }

        private static IOverlayEntry[] CreateEntries(string group)
            => new IOverlayEntry[] { new OverlayEntryWrapper("Framerate") { GroupName = group } };

        private sealed class RefreshFixture : IDisposable
        {
            private readonly TaskCompletionSource<IEnumerable<ISensorEntry>> _sensors =
                new TaskCompletionSource<IEnumerable<ISensorEntry>>(TaskCreationOptions.RunContinuationsAsynchronously);

            public Mock<IOverlayEntryProvider> Provider { get; } = new Mock<IOverlayEntryProvider>();
            public Mock<IAppConfiguration> Configuration { get; } = new Mock<IAppConfiguration>();
            public Mock<IRTSSService> Rtss { get; } = new Mock<IRTSSService>();
            public OverlayEntryCore Core { get; } = new OverlayEntryCore();
            // A virtual clock: a remote lease only runs out when a test advances it.
            public HistoricalScheduler DemandClock { get; } = new HistoricalScheduler(DateTimeOffset.UtcNow);
            public RemoteOverlayDemand Demand { get; }
            public Subject<TimeSpan> OsdTicks { get; } = new Subject<TimeSpan>();
            public BehaviorSubject<(DateTime, Dictionary<ISensorEntry, float>)> Snapshots { get; } =
                new BehaviorSubject<(DateTime, Dictionary<ISensorEntry, float>)>(
                    (DateTime.UtcNow, new Dictionary<ISensorEntry, float>()));
            public OverlayService Service { get; }

            public RefreshFixture(bool overlayActive = true, bool rtssRenderer = false)
            {
                Configuration.SetupAllProperties();
                Configuration.SetupGet(config => config.OnValueChanged)
                    .Returns(Observable.Never<(string key, object value)>());
                Configuration.Object.EnableHookFreeOverlay = !rtssRenderer;
                Configuration.Object.IsOverlayActive = overlayActive;
                Configuration.Object.SelectedHistoryRuns = 3;
                Rtss.Setup(service => service.IsRTSSInstalled()).Returns(rtssRenderer);

                Demand = new RemoteOverlayDemand(DemandClock, RemoteOverlayDemand.DefaultRequestLease);
                var sensors = new Mock<ISensorService>();
                sensors.Setup(service => service.GetSensorEntries()).Returns(_sensors.Task);
                // Clock ticks only arrive when a test pushes them: profile changes must publish
                // on their own.
                sensors.SetupGet(service => service.OsdUpdateStream).Returns(OsdTicks);
                sensors.SetupGet(service => service.SensorSnapshotStream).Returns(Snapshots);

                Service = new OverlayService(Mock.Of<IStatisticProvider>(), sensors.Object,
                    Provider.Object, Configuration.Object, Mock.Of<ILogger<OverlayService>>(),
                    Mock.Of<IRecordManager>(), Rtss.Object, Core, Mock.Of<ILogEntryManager>(), Demand);
            }

            public void Start(params ISensorEntry[] sensors) => _sensors.SetResult(sensors);

            public void Dispose()
            {
                Service.ShutdownOverlayService();
                OsdTicks.Dispose();
                Snapshots.Dispose();
            }
        }
    }
}

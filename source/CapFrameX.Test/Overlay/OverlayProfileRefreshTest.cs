using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading.Tasks;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Logging;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Overlay;
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
            public OverlayService Service { get; }

            public RefreshFixture()
            {
                Configuration.SetupAllProperties();
                Configuration.SetupGet(config => config.OnValueChanged)
                    .Returns(Observable.Never<(string key, object value)>());
                Configuration.Object.EnableHookFreeOverlay = true;
                Configuration.Object.IsOverlayActive = true;
                Configuration.Object.SelectedHistoryRuns = 3;

                var sensors = new Mock<ISensorService>();
                sensors.Setup(service => service.GetSensorEntries()).Returns(_sensors.Task);
                // No clock ticks or hardware samples: profile changes must publish on their own.
                sensors.SetupGet(service => service.OsdUpdateStream).Returns(Observable.Never<TimeSpan>());
                sensors.SetupGet(service => service.SensorSnapshotStream)
                    .Returns(Observable.Never<(DateTime, Dictionary<ISensorEntry, float>)>());

                Service = new OverlayService(Mock.Of<IStatisticProvider>(), sensors.Object,
                    Provider.Object, Configuration.Object, Mock.Of<ILogger<OverlayService>>(),
                    Mock.Of<IRecordManager>(), Rtss.Object, Core, Mock.Of<ILogEntryManager>());
            }

            public void Start() => _sensors.SetResult(Array.Empty<ISensorEntry>());

            public void Dispose() => Service.ShutdownOverlayService();
        }
    }
}

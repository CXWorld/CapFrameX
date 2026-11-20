using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Reactive;
using System.Reactive.Subjects;
using CapFrameX.Contracts.Configuration;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CapFrameX.Test.Integration
{
    /// <summary>
    /// Only PresentMon knows display times. The in-game renderers drew a design's display-time
    /// chart empty while "Use PresentMon frame times" was off (issue 483), so such a design takes
    /// PresentMon as the graph source whatever the option says.
    /// </summary>
    [TestClass]
    public class HookGraphSourceTest
    {
        private const string FrametimeChart = "{\"version\":1,\"root\":{\"type\":\"chart\",\"series\":\"frametimes\"}}";
        private const string DisplayTimeChart = "{\"version\":1,\"root\":{\"type\":\"group\",\"children\":["
            + "{\"type\":\"chart\",\"series\":\"frametimes\"},{\"type\":\"chart\",\"series\":\"displaytimes\"}]}}";
        private const string DisplayTimeThreshold = "{\"version\":1,\"root\":{\"type\":\"chart\",\"series\":\"frametimes\","
            + "\"lines\":[{\"series\":\"frametimes\"},{\"series\":\"displaytimes\",\"kind\":\"stutter\"}]}}";

        [TestMethod]
        public void ADesignWithADisplayTimeChartDrawsFromPresentMon()
        {
            var configuration = Configuration(usePresentMon: false);

            Assert.IsFalse(HookGraphSource.UsesPresentMon(configuration, null), "The classic rows keep the hook's presents.");
            Assert.IsFalse(HookGraphSource.UsesPresentMon(configuration, Design(FrametimeChart)),
                "A frame time chart keeps the hook's presents.");
            Assert.IsTrue(HookGraphSource.UsesPresentMon(configuration, Design(DisplayTimeChart)));
            Assert.IsTrue(HookGraphSource.UsesPresentMon(configuration, Design(DisplayTimeThreshold)),
                "A display-time stutter threshold needs the display times too.");
        }

        [TestMethod]
        public void TheOptionDrawsEveryDesignFromPresentMon()
        {
            var configuration = Configuration(usePresentMon: true);

            Assert.IsTrue(HookGraphSource.UsesPresentMon(configuration, null));
            Assert.IsTrue(HookGraphSource.UsesPresentMon(configuration, Design(FrametimeChart)));
            Assert.IsTrue(HookGraphSource.UsesPresentMon(configuration, Design(DisplayTimeChart)));
        }

        [TestMethod]
        public void HeaderFlags_CarryThePresentMonGraphBitForADisplayTimeDesign()
        {
            var configuration = Configuration(usePresentMon: false);
            configuration.OsdZoom = 120;
            configuration.OsdReplayBufferSize = 500;

            uint rows = HookMetricsPublisher.BuildHeaderFlags(configuration);
            uint frametimes = HookMetricsPublisher.BuildHeaderFlags(configuration, Design(FrametimeChart));
            uint displayTimes = HookMetricsPublisher.BuildHeaderFlags(configuration, Design(DisplayTimeChart));

            Assert.AreEqual(0u, rows & HookMetricsChannel.FlagPresentMonGraph);
            Assert.AreEqual(rows, frametimes);
            Assert.AreEqual(rows | HookMetricsChannel.FlagPresentMonGraph, displayTimes,
                "The design sets the graph source bit and leaves every other header field unchanged.");
        }

        [TestMethod]
        public void FrametimeRing_RunsWhileTheActiveDesignHasADisplayTimeChart()
        {
            using var context = new FrametimeContext(hookEnabled: true, usePresentMon: false);
            Assert.IsFalse(context.Publisher.IsPublishing, "The classic rows keep the hook's presents.");
            Assert.IsFalse(context.RingExists());

            context.Activate(Design(DisplayTimeChart));
            Assert.IsTrue(context.Publisher.IsPublishing);
            Assert.IsTrue(context.RingExists(), "The hook opens the ring by name.");

            context.Activate(Design(FrametimeChart));
            Assert.IsFalse(context.Publisher.IsPublishing);
            Assert.IsFalse(context.RingExists());

            context.Activate(Design(DisplayTimeThreshold));
            Assert.IsTrue(context.Publisher.IsPublishing);

            context.Activate(null);
            Assert.IsFalse(context.Publisher.IsPublishing, "Back to the classic rows.");
        }

        [TestMethod]
        public void FrametimeRing_FollowsTheOptionWithoutADesign()
        {
            using var context = new FrametimeContext(hookEnabled: true, usePresentMon: false);

            context.SetUsePresentMon(true);
            Assert.IsTrue(context.Publisher.IsPublishing);

            context.SetUsePresentMon(false);
            Assert.IsFalse(context.Publisher.IsPublishing);
        }

        [TestMethod]
        public void FrametimeRing_StaysOffWhileAnotherRendererDraws()
        {
            using var context = new FrametimeContext(hookEnabled: false, usePresentMon: false);
            context.Activate(Design(DisplayTimeChart));
            Assert.IsFalse(context.Publisher.IsPublishing, "The hook-free window reads PresentMon itself.");

            context.SetHookEnabled(true);
            Assert.IsTrue(context.Publisher.IsPublishing);

            context.SetHookEnabled(false);
            Assert.IsFalse(context.Publisher.IsPublishing);
        }

        [TestMethod]
        public void FrametimeRing_IsCreatedAtStartForAnActiveDisplayTimeDesign()
        {
            using var context = new FrametimeContext(hookEnabled: true, usePresentMon: false, Design(DisplayTimeChart));
            Assert.IsTrue(context.Publisher.IsPublishing);
        }

        private static OverlayRuntimeDesign Design(string json) => OverlayRuntimeDesign.Parse("id", "Test", json);

        private static IAppConfiguration Configuration(bool usePresentMon)
        {
            var configuration = new Mock<IAppConfiguration>();
            configuration.SetupAllProperties();
            configuration.Object.HookOverlayUsePresentMonFrametimes = usePresentMon;
            return configuration.Object;
        }

        private sealed class FrametimeContext : IDisposable
        {
            private readonly string _mapName = @"Local\CfxOsdFrametimesPublisherTest_" + Guid.NewGuid().ToString("N");
            private readonly Subject<(string key, object value)> _changes = new Subject<(string key, object value)>();
            private readonly Subject<Unit> _designChanges = new Subject<Unit>();
            private readonly Subject<int> _pids = new Subject<int>();
            private readonly Mock<IAppConfiguration> _configuration = new Mock<IAppConfiguration>();
            private OverlayRuntimeDesign _design;

            public HookFrametimePublisher Publisher { get; }

            public FrametimeContext(bool hookEnabled, bool usePresentMon, OverlayRuntimeDesign design = null)
            {
                _configuration.SetupAllProperties();
                _configuration.SetupGet(configuration => configuration.OnValueChanged).Returns(_changes);
                _configuration.Object.EnableHookOverlay = hookEnabled;
                _configuration.Object.HookOverlayUsePresentMonFrametimes = usePresentMon;
                _design = design;
                Publisher = new HookFrametimePublisher(_configuration.Object, null, _pids,
                    processIdColumnIndex: 0, frametimeColumnIndex: 1, displayChangedColumnIndex: 2,
                    startTimeIndexProvider: () => 3, () => _design, _designChanges, _mapName);
            }

            public void Activate(OverlayRuntimeDesign design)
            {
                _design = design;
                _designChanges.OnNext(Unit.Default);
            }

            public void SetUsePresentMon(bool enabled)
            {
                _configuration.Object.HookOverlayUsePresentMonFrametimes = enabled;
                _changes.OnNext((nameof(IAppConfiguration.HookOverlayUsePresentMonFrametimes), enabled));
            }

            public void SetHookEnabled(bool enabled)
            {
                _configuration.Object.EnableHookOverlay = enabled;
                _changes.OnNext((nameof(IAppConfiguration.EnableHookOverlay), enabled));
            }

            public bool RingExists()
            {
                try
                {
                    using var map = MemoryMappedFile.OpenExisting(_mapName, MemoryMappedFileRights.Read);
                    return true;
                }
                catch (FileNotFoundException)
                {
                    return false;
                }
            }

            public void Dispose()
            {
                Publisher.Dispose();
                _changes.Dispose();
                _designChanges.Dispose();
                _pids.Dispose();
            }
        }
    }
}

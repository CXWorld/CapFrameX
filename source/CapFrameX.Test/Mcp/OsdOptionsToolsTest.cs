using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Mcp.Tools;
using CapFrameX.Overlay;
using CapFrameX.PresentMonInterface;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace CapFrameX.Test.Mcp
{
    [TestClass]
    public class OsdOptionsToolsTest
    {
        [TestMethod]
        public void GetOsdOptions_ReturnsCompleteTypedSnapshot()
        {
            var config = CreateConfiguration();
            config.Object.EnableHookOverlay = true;
            config.Object.IsOverlayActive = true;
            config.Object.AutoDisableOverlay = false;
            config.Object.ShowSystemTimeSeconds = true;
            config.Object.HideOverlay = true;
            config.Object.HookOverlayUsePresentMonFrametimes = true;
            config.Object.OsdReplayBufferSize = 1750;
            config.Object.HookFreeRefreshRate = 10;
            config.Object.OSDCustomPosition = true;
            config.Object.OSDPositionX = 123;
            config.Object.OSDPositionY = 456;
            config.Object.OsdBackgroundOpacity = 85;
            config.Object.OsdAnchor = (int)OsdAnchorPosition.BottomRight;
            config.Object.OsdMarginX = 31;
            config.Object.OsdMarginY = 32;
            config.Object.OsdZoom = 125;
            config.Object.UseOsdValueSmoothing = false;
            config.Object.OverlayHotKey = "Alt+O";
            config.Object.OverlayConfigHotKey = "Alt+C";
            config.Object.ResetMetricsHotkey = "Alt+M";
            config.Object.OSDRefreshPeriod = 750;
            config.Object.MetricInterval = 30;

            var rtss = new Mock<IRTSSService>();
            rtss.Setup(service => service.IsRTSSInstalled()).Returns(true);
            var tool = CreateOsdOptionsTool(config, rtss: rtss);

            var result = tool.GetOsdOptions();

            Assert.AreEqual("InGame", result.Renderer);
            Assert.IsTrue(result.RendererConfigurationValid);
            Assert.IsTrue(result.EnableHookOverlay);
            Assert.IsFalse(result.EnableHookFreeOverlay);
            Assert.IsTrue(result.RtssInstalled);
            Assert.IsTrue(result.IsOverlayActive);
            Assert.IsFalse(result.AutoDisableOverlay);
            Assert.IsTrue(result.ShowSystemTimeSeconds);
            Assert.IsTrue(result.HideOverlay);
            Assert.IsTrue(result.HookOverlayUsePresentMonFrametimes);
            Assert.AreEqual(1750, result.ReplayBufferSizeMs);
            Assert.AreEqual(10, result.HookFreeRefreshRate);
            Assert.IsTrue(result.OsdCustomPosition);
            Assert.AreEqual(123, result.OsdPositionX);
            Assert.AreEqual(456, result.OsdPositionY);
            Assert.AreEqual(85, result.BackgroundOpacity);
            Assert.AreEqual("BottomRight", result.Anchor);
            Assert.AreEqual((int)OsdAnchorPosition.BottomRight, result.AnchorValue);
            Assert.AreEqual(31, result.MarginX);
            Assert.AreEqual(32, result.MarginY);
            Assert.AreEqual(125, result.Zoom);
            Assert.IsFalse(result.UseValueSmoothing);
            Assert.AreEqual("Alt+O", result.OverlayHotkey);
            Assert.AreEqual("Alt+C", result.OverlayConfigHotkey);
            Assert.AreEqual("Alt+M", result.ResetMetricsHotkey);
            Assert.AreEqual(750, result.RefreshPeriodMs);
            Assert.AreEqual(30, result.MetricIntervalSeconds);
        }

        [TestMethod]
        public void SetOsdOptions_AppliesRuntimeSideEffectsAndReturnsResult()
        {
            var config = CreateConfiguration();
            config.Object.EnableHookOverlay = false;
            config.Object.EnableHookFreeOverlay = false;
            config.Object.IsOverlayActive = false;
            config.Object.OSDCustomPosition = false;
            config.Object.OSDPositionX = 0;
            config.Object.OSDPositionY = 0;
            config.Object.HookFreeRefreshRate = 1;
            config.Object.OSDRefreshPeriod = 1000;
            config.Object.MetricInterval = 20;

            var activeStream = new Subject<bool>();
            bool? publishedActiveState = null;
            activeStream.Subscribe(value => publishedActiveState = value);
            var overlayService = new Mock<IOverlayService>();
            overlayService.SetupGet(service => service.IsOverlayActiveStream).Returns(activeStream);
            var rtss = new Mock<IRTSSService>();
            rtss.Setup(service => service.IsRTSSInstalled()).Returns(false);
            var sensorService = new Mock<ISensorService>();
            var onlineMetricService = new Mock<IOnlineMetricService>();
            var tool = CreateOsdOptionsTool(config, overlayService, rtss, sensorService, onlineMetricService);

            var result = tool.SetOsdOptions(
                renderer: OsdRendererMode.HookFree,
                isOverlayActive: true,
                autoDisableOverlay: false,
                showSystemTimeSeconds: true,
                replayBufferSizeMs: 1500,
                hookFreeRefreshRate: 20,
                osdCustomPosition: true,
                osdPositionX: 100,
                osdPositionY: 200,
                backgroundOpacity: 80,
                anchor: OsdAnchorPosition.TopCenter,
                marginX: 40,
                marginY: 50,
                zoom: 130,
                useValueSmoothing: false,
                refreshPeriodMs: 750,
                metricIntervalSeconds: 30);

            Assert.IsTrue(config.Object.EnableHookFreeOverlay);
            Assert.IsFalse(config.Object.EnableHookOverlay);
            Assert.IsTrue(config.Object.IsOverlayActive);
            Assert.IsFalse(config.Object.AutoDisableOverlay);
            Assert.IsTrue(config.Object.ShowSystemTimeSeconds);
            Assert.AreEqual(1500, config.Object.OsdReplayBufferSize);
            Assert.AreEqual(20, config.Object.HookFreeRefreshRate);
            Assert.IsTrue(config.Object.OSDCustomPosition);
            Assert.AreEqual(100, config.Object.OSDPositionX);
            Assert.AreEqual(200, config.Object.OSDPositionY);
            Assert.AreEqual(80, config.Object.OsdBackgroundOpacity);
            Assert.AreEqual((int)OsdAnchorPosition.TopCenter, config.Object.OsdAnchor);
            Assert.AreEqual(40, config.Object.OsdMarginX);
            Assert.AreEqual(50, config.Object.OsdMarginY);
            Assert.AreEqual(130, config.Object.OsdZoom);
            Assert.IsFalse(config.Object.UseOsdValueSmoothing);
            Assert.AreEqual(750, config.Object.OSDRefreshPeriod);
            Assert.AreEqual(30, config.Object.MetricInterval);
            Assert.AreEqual(true, publishedActiveState);

            rtss.Verify(service => service.SetOSDCustomPosition(true), Times.Once);
            rtss.Verify(service => service.SetOverlayPosition(100, 200), Times.Once);
            TimeSpan expectedSensorInterval = TimeSpan.FromMilliseconds(750);
            sensorService.Verify(service => service.SetOSDInterval(expectedSensorInterval), Times.Once);
            onlineMetricService.Verify(service => service.SetMetricInterval(), Times.Once);
            Assert.IsTrue(result.Applied);
            Assert.AreEqual(result.ChangedProperties.Count, result.ChangedCount);
            Assert.AreEqual("HookFree", result.Options.Renderer);
            Assert.AreEqual(1500, result.Options.ReplayBufferSizeMs);
            Assert.AreEqual(20, result.Options.HookFreeRefreshRate);
            CollectionAssert.Contains(result.ChangedProperties, nameof(IAppConfiguration.IsOverlayActive));
            CollectionAssert.Contains(result.ChangedProperties, nameof(IAppConfiguration.EnableHookFreeOverlay));
            CollectionAssert.Contains(result.ChangedProperties, nameof(IAppConfiguration.HookFreeRefreshRate));
        }

        [TestMethod]
        public void SetOsdOptions_InvalidInputDoesNotPartiallyApply()
        {
            var config = CreateConfiguration();
            config.Object.AutoDisableOverlay = true;
            config.Object.OsdZoom = 100;
            config.Object.OsdReplayBufferSize = 750;
            config.Object.HookFreeRefreshRate = 1;
            var activeStream = new Subject<bool>();
            int publishedCount = 0;
            activeStream.Subscribe(_ => publishedCount++);
            var overlayService = new Mock<IOverlayService>();
            overlayService.SetupGet(service => service.IsOverlayActiveStream).Returns(activeStream);
            var tool = CreateOsdOptionsTool(config, overlayService);

            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                tool.SetOsdOptions(autoDisableOverlay: false, zoom: 201));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                tool.SetOsdOptions(autoDisableOverlay: false, replayBufferSizeMs: 499));
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                tool.SetOsdOptions(autoDisableOverlay: false, hookFreeRefreshRate: 3));

            Assert.IsTrue(config.Object.AutoDisableOverlay);
            Assert.AreEqual(100, config.Object.OsdZoom);
            Assert.AreEqual(750, config.Object.OsdReplayBufferSize);
            Assert.AreEqual(1, config.Object.HookFreeRefreshRate);
            Assert.AreEqual(0, publishedCount);
        }

        [TestMethod]
        public void SetOsdOptions_CannotActivateUnavailableRtssRenderer()
        {
            var config = CreateConfiguration();
            config.Object.EnableHookOverlay = false;
            config.Object.EnableHookFreeOverlay = false;
            config.Object.IsOverlayActive = false;
            var rtss = new Mock<IRTSSService>();
            rtss.Setup(service => service.IsRTSSInstalled()).Returns(false);
            var tool = CreateOsdOptionsTool(config, rtss: rtss);

            Assert.ThrowsException<InvalidOperationException>(() =>
                tool.SetOsdOptions(renderer: OsdRendererMode.Rtss, isOverlayActive: true));

            Assert.IsFalse(config.Object.IsOverlayActive);
        }

        [TestMethod]
        public void GetOverlayEntries_ReturnsAllLiveFieldsInCurrentOrder()
        {
            var first = CreateEntry("Frametime", "z", "Frame Time");
            first.Value = 12.34;
            first.ValueFormat = "{0:F1}";
            first.GroupNameFormat = "[{0}]";
            first.GroupColor = "11223344";
            first.Color = "55667788";
            first.UpperLimitValue = "20";
            first.LowerLimitValue = "5";
            first.UpperLimitColor = "AABBCCDD";
            first.LowerLimitColor = "01020304";
            first.GroupFontSize = 90;
            first.ValueFontSize = 110;
            first.GroupSeparators = 2;
            first.IsNumeric = true;
            first.LastLimitState = LimitState.Upper;
            first.FormatChanged = false;
            var second = CreateEntry("Framerate", "a", "Frame Rate");

            var overlayService = new Mock<IOverlayService>();
            overlayService.SetupGet(service => service.CurrentOverlayEntries)
                .Returns(new IOverlayEntry[] { first, second });
            var tool = CreateOverlayEntryTool(overlayService);

            var result = tool.GetOverlayEntries();

            Assert.AreEqual(2, result.EntryCount);
            Assert.AreEqual("Frametime", result.Entries[0].Identifier,
                "Live order must not be replaced by the persisted SortKey order.");
            Assert.AreEqual(0, result.Entries[0].OrderIndex);
            Assert.AreEqual("12.3", result.Entries[0].FormattedValue);
            Assert.AreEqual("[Frame Time]", result.Entries[0].FormattedGroupName);
            Assert.AreEqual("11223344", result.Entries[0].GroupColor);
            Assert.AreEqual("AABBCCDD", result.Entries[0].UpperLimitColor);
            Assert.AreEqual("01020304", result.Entries[0].LowerLimitColor);
            Assert.AreEqual(90, result.Entries[0].GroupFontSize);
            Assert.AreEqual(110, result.Entries[0].ValueFontSize);
            Assert.AreEqual(2, result.Entries[0].GroupSeparators);
            Assert.IsTrue(result.Entries[0].ShowOnOverlayIsEnabled);
            Assert.IsTrue(result.Entries[0].ShowGraphIsEnabled);
            Assert.IsTrue(result.Entries[0].IsNumeric);
            Assert.AreEqual("Upper", result.Entries[0].LastLimitState);
        }

        [TestMethod]
        public async Task SetOverlayEntry_UpdatesEveryEditableFieldAndPersistsActiveSlot()
        {
            var entry = CreateEntry("Frametime", "0", "Frame Time");
            entry.IsNumeric = true;
            var secondEntry = CreateEntry("Framerate", "1", "Frame Rate");
            var overlayService = new Mock<IOverlayService>();
            overlayService.SetupGet(service => service.CurrentOverlayEntries)
                .Returns(new IOverlayEntry[] { entry, secondEntry });
            var provider = new Mock<IOverlayEntryProvider>();
            provider.Setup(service => service.SaveOverlayEntriesToJson(2)).Returns(Task.CompletedTask);
            var config = CreateConfiguration();
            config.Object.OverlayEntryConfigurationFile = 2;
            var tool = new OverlayConfigTools(overlayService.Object, provider.Object, config.Object);

            var result = await tool.SetOverlayEntry(
                identifier: "frametime",
                isEntryEnabled: false,
                showOnOverlay: true,
                groupName: "FT",
                showGraph: true,
                color: "11223344",
                groupColor: "55667788",
                upperLimitValue: "25.5",
                lowerLimitValue: "4",
                upperLimitColor: "AABBCCDD",
                lowerLimitColor: "01020304",
                valueFontSize: 115,
                groupFontSize: 95,
                groupSeparators: 1,
                orderIndex: 1);

            Assert.IsFalse(entry.IsEntryEnabled);
            Assert.IsTrue(entry.ShowOnOverlay);
            Assert.AreEqual("FT", entry.GroupName);
            Assert.IsTrue(entry.ShowGraph);
            Assert.AreEqual("11223344", entry.Color);
            Assert.AreEqual("55667788", entry.GroupColor);
            Assert.AreEqual("25.5", entry.UpperLimitValue);
            Assert.AreEqual("4", entry.LowerLimitValue);
            Assert.AreEqual("AABBCCDD", entry.UpperLimitColor);
            Assert.AreEqual("01020304", entry.LowerLimitColor);
            Assert.AreEqual(115, entry.ValueFontSize);
            Assert.AreEqual(95, entry.GroupFontSize);
            Assert.AreEqual(1, entry.GroupSeparators);
            Assert.IsTrue(entry.FormatChanged);
            Assert.IsTrue(result.Persisted);
            Assert.AreEqual(result.ChangedProperties.Count, result.ChangedCount);
            Assert.AreEqual(1, result.Entry.OrderIndex);
            provider.Verify(service => service.MoveEntry(0, 1), Times.Once);
            provider.Verify(service => service.SaveOverlayEntriesToJson(2), Times.Once);
        }

        [TestMethod]
        public async Task SetOverlayEntry_InvalidColorDoesNotPartiallyApply()
        {
            var entry = CreateEntry("Frametime", "0", "Original");
            var overlayService = new Mock<IOverlayService>();
            overlayService.SetupGet(service => service.CurrentOverlayEntries)
                .Returns(new IOverlayEntry[] { entry });
            var provider = new Mock<IOverlayEntryProvider>();
            var tool = new OverlayConfigTools(overlayService.Object, provider.Object, CreateConfiguration().Object);

            await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
                tool.SetOverlayEntry("Frametime", groupName: "Changed", color: "not-a-color"));

            Assert.AreEqual("Original", entry.GroupName);
            provider.Verify(service => service.SaveOverlayEntriesToJson(It.IsAny<int>()), Times.Never);
        }

        private static Mock<IAppConfiguration> CreateConfiguration()
        {
            var config = new Mock<IAppConfiguration>();
            config.SetupAllProperties();
            config.Object.OsdBackgroundOpacity = 97;
            config.Object.OsdReplayBufferSize = 750;
            config.Object.HookFreeRefreshRate = 1;
            config.Object.OsdZoom = 100;
            config.Object.OsdAnchor = 0;
            config.Object.OsdMarginX = 30;
            config.Object.OsdMarginY = 30;
            config.Object.OSDRefreshPeriod = 1000;
            config.Object.MetricInterval = 20;
            return config;
        }

        private static OsdOptionsTools CreateOsdOptionsTool(
            Mock<IAppConfiguration> config,
            Mock<IOverlayService> overlayService = null,
            Mock<IRTSSService> rtss = null,
            Mock<ISensorService> sensorService = null,
            Mock<IOnlineMetricService> onlineMetricService = null)
        {
            if (overlayService == null)
            {
                overlayService = new Mock<IOverlayService>();
                overlayService.SetupGet(service => service.IsOverlayActiveStream)
                    .Returns(new Subject<bool>());
            }
            rtss ??= new Mock<IRTSSService>();
            sensorService ??= new Mock<ISensorService>();
            onlineMetricService ??= new Mock<IOnlineMetricService>();
            return new OsdOptionsTools(config.Object, overlayService.Object, rtss.Object,
                sensorService.Object, onlineMetricService.Object);
        }

        private static OverlayConfigTools CreateOverlayEntryTool(Mock<IOverlayService> overlayService)
        {
            return new OverlayConfigTools(overlayService.Object,
                new Mock<IOverlayEntryProvider>().Object,
                CreateConfiguration().Object);
        }

        private static OverlayEntryWrapper CreateEntry(string identifier, string sortKey, string groupName)
        {
            return new OverlayEntryWrapper(identifier)
            {
                SortKey = sortKey,
                OverlayEntryType = EOverlayEntryType.CX,
                Description = identifier,
                IsEntryEnabled = true,
                ShowOnOverlay = false,
                ShowOnOverlayIsEnabled = true,
                GroupName = groupName,
                ShowGraph = false,
                ShowGraphIsEnabled = true,
            };
        }
    }
}

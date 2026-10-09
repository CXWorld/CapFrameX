using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.OSD.Integration;
using CapFrameX.Overlay;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class OverlayClassicPreviewFeedTest
    {
        [TestMethod]
        public async Task PreviewUsesCurrentRowsAndMeasuredValuesWithoutChangingClassicProfileOrRtssMacros()
        {
            var fps = Entry("Framerate", "Performance", "<FR>");
            fps.ValueAlignmentAndDigits = "{0:F1}";
            fps.ValueUnitFormat = "{0} FPS";
            fps.UpperLimitValue = "144";
            fps.UpperLimitColor = "FF0000";
            var hidden = Entry("Hidden", "Hidden", 42d);
            hidden.ShowOnOverlay = false;
            var provider = new Mock<IOverlayEntryProvider>(MockBehavior.Strict);
            provider.Setup(p => p.GetOverlayEntries(false)).ReturnsAsync(new IOverlayEntry[] { fps, hidden });
            var configuration = new Mock<IAppConfiguration>(MockBehavior.Strict);
            configuration.SetupGet(c => c.UseRunHistory).Returns(false);
            var overlay = new Mock<IOverlayService>();
            var feed = new OverlayClassicPreviewFeed(provider.Object, overlay.Object, configuration.Object);
            var sources = new[] { new OverlayTelemetrySource { Id = "metric/Framerate", Identifier = "Framerate" } };
            var snapshot = new OverlayTelemetrySnapshot(DateTime.UtcNow,
                new Dictionary<string, double?> { ["metric/Framerate"] = 165.5 });

            var result = await feed.ReadAsync(snapshot, sources);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(165.5, result[0].Value);
            Assert.AreEqual(1, result[0].Digits);
            Assert.AreEqual("FPS", result[0].Unit);
            Assert.AreEqual("Performance", result[0].Group);
            Assert.IsTrue(result[0].HasUpper);
            Assert.AreEqual(144d, result[0].UpperLimit);
            Assert.AreEqual("<FR>", fps.Value);
            Assert.IsFalse(hidden.ShowOnOverlay);
            configuration.VerifyGet(c => c.UseRunHistory, Times.Once);
            configuration.VerifyNoOtherCalls();
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task AbsentOrStaleReadingsAreUnavailableAndProfileChangesReplaceTheBlock(bool stale)
        {
            var provider = new Mock<IOverlayEntryProvider>();
            provider.SetupSequence(p => p.GetOverlayEntries(false))
                .ReturnsAsync(new IOverlayEntry[] { Entry("Framerate", "FPS", "<FR>") })
                .ReturnsAsync(Array.Empty<IOverlayEntry>());
            var feed = new OverlayClassicPreviewFeed(provider.Object, Mock.Of<IOverlayService>(), Mock.Of<IAppConfiguration>());
            var sources = new[] { new OverlayTelemetrySource { Id = "metric/Framerate", Identifier = "Framerate" } };
            var snapshot = new OverlayTelemetrySnapshot(DateTime.UtcNow.AddSeconds(stale ? -10 : 0),
                new Dictionary<string, double?> { ["metric/Framerate"] = stale ? 100d : null });

            var result = await feed.ReadAsync(snapshot, sources);
            Assert.IsFalse(result[0].IsNumeric);
            Assert.AreEqual("—", result[0].ValueText);
            Assert.AreEqual(0, (await feed.ReadAsync(snapshot, sources)).Count);
        }

        [TestMethod]
        public void HybridGraphRequirementsCombineClassicRowsAndDesignCharts()
        {
            var rows = new[] { Entry("DisplayTime", "Display time", 4d) };
            rows[0].ShowGraph = true;
            var hybrid = OverlayRuntimeDesign.Parse("test", "Hybrid", Scene(true));
            var tiles = OverlayRuntimeDesign.Parse("test", "Tiles", Scene(false));

            Assert.IsTrue(hybrid.HasClassicRows);
            Assert.IsFalse(tiles.HasClassicRows);
            Assert.AreEqual(3, OsdOverlayBridge.GetFrameFeedRequirements(hybrid, rows));
            Assert.AreEqual(1, OsdOverlayBridge.GetFrameFeedRequirements(tiles, rows));
            Assert.AreEqual(2, OsdOverlayBridge.GetFrameFeedRequirements(null, rows));
            rows[0].ShowOnOverlay = false;
            Assert.AreEqual(1, OsdOverlayBridge.GetFrameFeedRequirements(hybrid, rows));
            Assert.AreEqual(0, OsdOverlayBridge.GetFrameFeedRequirements(null, rows));
        }

        private static string Scene(bool classic) => "{\"version\":1,\"root\":{\"type\":\"panel\",\"children\":["
            + "{\"type\":\"chart\",\"series\":\"frametimes\"}"
            + (classic ? ",{\"type\":\"classicRows\",\"width\":320,\"height\":160}" : "") + "]}}";

        private static OverlayEntryWrapper Entry(string id, string group, object value) => new OverlayEntryWrapper(id)
        {
            GroupName = group, Value = value, IsNumeric = true, IsEntryEnabled = true,
            ShowOnOverlay = true, Color = "FFFFFF", GroupColor = "FFFFFF",
            ValueAlignmentAndDigits = "{0:F0}", ValueUnitFormat = "{0}"
        };
    }
}

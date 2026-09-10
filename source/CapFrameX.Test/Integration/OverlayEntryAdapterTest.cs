using System.Collections.Generic;
using System.Linq;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.Sensor;
using CapFrameX.OSD.Integration;
using CapFrameX.Overlay;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class OverlayEntryAdapterTest
    {
        [TestMethod]
        public void ToOsdEntries_NumericNullValue_DoesNotLeakRtssHypertext()
        {
            var entry = new OverlayEntryWrapper("DisplayTime")
            {
                Description = "Displaytime",
                GroupName = "Displaytime",
                IsEntryEnabled = true,
                ShowOnOverlay = true,
                ShowGraph = true,
                IsNumeric = true,
                Value = null,
                ValueAlignmentAndDigits = "{0,5:F1}",
                ValueUnitFormat = "ms ",
                ValueFormat = "<S2><C3>{0,5:F1}<C><S><S0><C3>ms <C><S>"
            };

            var result = OverlayEntryAdapter.ToOsdEntries(new[] { entry }).Single();

            Assert.IsTrue(result.IsNumeric);
            Assert.AreEqual(0d, result.Value);
            Assert.IsTrue(string.IsNullOrEmpty(result.ValueText));
            Assert.AreEqual("ms", result.Unit);
            Assert.AreEqual(1, result.Digits);
        }

        /// <summary>
        /// The group-name and value font sizes are RTSS percentages whose sign selects super- or
        /// subscript. The CX renderers scale the text by the magnitude only, an unset size (0) is
        /// the 100 % default, and the range is clamped so a row still fits the panel.
        /// </summary>
        [DataTestMethod]
        [DataRow(0, 100)]
        [DataRow(100, 100)]
        [DataRow(150, 150)]
        [DataRow(-150, 150)]
        [DataRow(75, 75)]
        [DataRow(-30, 50)]
        [DataRow(500, 200)]
        public void ToOsdEntries_MapsFontSizesToTextScales_MagnitudeOnlyAndClamped(int fontSize, int expectedPercent)
        {
            var entry = new OverlayEntryWrapper("/intelcpu/0/clock/1")
            {
                Description = "Core #1 P (MHz)",
                GroupName = "Core #1 P",
                OverlayEntryType = EOverlayEntryType.CPU,
                IsEntryEnabled = true,
                ShowOnOverlay = true,
                IsNumeric = true,
                Value = 5200d,
                GroupFontSize = fontSize,
                ValueFontSize = fontSize
            };

            var result = OverlayEntryAdapter.ToOsdEntries(new[] { entry }).Single();

            Assert.AreEqual(expectedPercent, result.GroupScalePercent);
            Assert.AreEqual(expectedPercent, result.ValueScalePercent);
        }

        [TestMethod]
        public void ToOsdEntries_RunHistoryRowsInheritTheTemplateFontSizes()
        {
            var template = CreateRunHistoryEntry();
            template.GroupFontSize = 120;
            template.ValueFontSize = -80;

            var rows = OverlayEntryAdapter.ToOsdEntries(new[] { template },
                showRunHistory: true, runHistory: new[] { "120 FPS", "118 FPS" },
                runHistoryAggregation: "119 FPS");

            Assert.AreEqual(3, rows.Count);
            Assert.IsTrue(rows.All(row => row.GroupScalePercent == 120), "group scale");
            Assert.IsTrue(rows.All(row => row.ValueScalePercent == 80), "value scale (magnitude)");
        }

        [TestMethod]
        public void ToOsdEntries_TextNullValue_DoesNotLeakRtssHypertext()
        {
            var entry = new OverlayEntryWrapper("TextEntry")
            {
                Description = "Text entry",
                IsEntryEnabled = true,
                ShowOnOverlay = true,
                IsNumeric = false,
                Value = null,
                ValueFormat = "<S2><C3>{0}<C><S>"
            };

            var result = OverlayEntryAdapter.ToOsdEntries(new[] { entry }).Single();

            Assert.IsFalse(result.IsNumeric);
            Assert.IsTrue(string.IsNullOrEmpty(result.ValueText));
        }

        [TestMethod]
        public void ToOsdEntries_MapsGroupAndValueColorsSeparately()
        {
            var entry = new OverlayEntryWrapper("ColoredEntry")
            {
                Description = "Colored entry",
                GroupName = "Group",
                GroupColor = "FF0271F9",
                Color = "FFFFD700",
                IsEntryEnabled = true,
                ShowOnOverlay = true,
                Value = 42d
            };

            var result = OverlayEntryAdapter.ToOsdEntries(new[] { entry }).Single();

            Assert.AreEqual(0x0271F9FFu, result.GroupColor);
            Assert.AreEqual(0xFFD700FFu, result.Color);
        }

        [TestMethod]
        public void ToOsdEntries_RunHistoryDisabled_DoesNotRenderPlaceholder()
        {
            var entry = CreateRunHistoryEntry();

            var result = OverlayEntryAdapter.ToOsdEntries(new[] { entry }, false,
                new[] { "N/A", "N/A", "N/A" });

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void ToOsdEntries_RunHistoryEnabled_RendersEveryConfiguredRunAndResult()
        {
            var entry = CreateRunHistoryEntry();
            var history = new[] { "120 FPS", "N/A", "N/A" };
            var outliers = new[] { true, false, false };

            var result = OverlayEntryAdapter.ToOsdEntries(new[] { entry }, true, history,
                outliers, "118 FPS");

            Assert.AreEqual(4, result.Count);
            Assert.AreEqual("Run 1:", result[0].Group);
            Assert.AreEqual("120 FPS", result[0].ValueText);
            Assert.AreEqual("Run 2:", result[1].Group);
            Assert.AreEqual("N/A", result[1].ValueText);
            Assert.AreEqual("Run 3:", result[2].Group);
            Assert.AreEqual("N/A", result[2].ValueText);
            Assert.AreEqual("Result:", result[3].Group);
            Assert.AreEqual("118 FPS", result[3].ValueText);
            Assert.AreNotEqual(result[0].Color, result[1].Color);
        }

        [TestMethod]
        public void EnthusiastTemplate_EnablesDisplayTimeOnlyWhenRendererProvidesIt()
        {
            var rtssConfig = new Mock<IAppConfiguration>();
            var rtssDefaults = OverlayUtils.GetOverlayEntryDefaults(rtssConfig.Object);
            Assert.IsFalse(rtssDefaults.Single(e => e.Identifier == "DisplayTime").IsEntryEnabled);
            Assert.IsTrue(rtssDefaults.Single(e => e.Identifier == "Resolution").IsEntryEnabled);

            var hookFreeConfig = new Mock<IAppConfiguration>();
            hookFreeConfig.SetupGet(c => c.EnableHookFreeOverlay).Returns(true);
            var hookFreeDefaults = OverlayUtils.GetOverlayEntryDefaults(hookFreeConfig.Object);
            var displayTime = hookFreeDefaults.Single(e => e.Identifier == "DisplayTime");
            Assert.IsTrue(displayTime.IsEntryEnabled);
            Assert.IsFalse(hookFreeDefaults.Single(e => e.Identifier == "Resolution").IsEntryEnabled);

            var template = new OverlayTemplateService(new Mock<ISensorService>().Object);
            template.ApplyTemplate(EOverlayTemplate.Enthusiast,
                new List<IOverlayEntry> { displayTime });

            Assert.IsTrue(displayTime.ShowOnOverlay);
            Assert.IsTrue(displayTime.ShowGraph);
        }

        private static OverlayEntryWrapper CreateRunHistoryEntry()
        {
            return new OverlayEntryWrapper("RunHistory")
            {
                Description = "Run history",
                GroupColor = "FFFFFFFF",
                Color = "FF0271F9",
                LowerLimitColor = "FFC80000",
                GroupSeparators = 1,
                IsEntryEnabled = true,
                ShowOnOverlay = true
            };
        }
    }
}

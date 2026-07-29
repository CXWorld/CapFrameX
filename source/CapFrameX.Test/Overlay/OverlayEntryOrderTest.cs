using System.Collections.Generic;
using System.Linq;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data;
using CapFrameX.Overlay;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CapFrameX.Test.Overlay
{
    /// <summary>
    /// DisplayTime has to sit directly beneath the framerate rows in the overlay entry list, and it
    /// has to do so on every path that produces such a list: "Reset to default" and each of the three
    /// overlay templates. It used to fall through to the generic CX rank (10) while Framerate and
    /// Frametime were pinned to the end (80), which put DisplayTime at the very TOP of a templated
    /// list — and consequently rendered its graph above everything else.
    /// </summary>
    [TestClass]
    public class OverlayEntryOrderTest
    {
        private const string Framerate = "Framerate";
        private const string Frametime = "Frametime";
        private const string DisplayTime = "DisplayTime";

        // ---------------------------------------------------------------- Reset to default

        [TestMethod]
        public void Defaults_DisplayTimeFollowsTheFramerateRows()
        {
            var defaults = OverlayUtils.GetOverlayEntryDefaults(new Mock<IAppConfiguration>().Object);
            var identifiers = defaults.Select(entry => entry.Identifier).ToList();

            AssertFramerateBlockOrder(identifiers, "Reset to default");
        }

        [TestMethod]
        public void Defaults_FramerateBlockTiesSoTheResetSortPreservesIt()
        {
            // "Reset to default" does NOT use SortForTemplate — OverlayEntryProvider orders the
            // defaults by OverlayEntryType and then by SortKey. That path keeps the shipped order
            // only because the three entries tie on BOTH keys and OrderBy is stable. Lock the tie:
            // give one of them a different type or SortKey and the reset order silently changes.
            var block = OverlayUtils.GetOverlayEntryDefaults(new Mock<IAppConfiguration>().Object)
                .Where(entry => IsFramerateBlock(entry.Identifier))
                .ToList();

            Assert.AreEqual(3, block.Count, "expected exactly Framerate, Frametime and DisplayTime");
            CollectionAssert.AreEqual(
                new[] { EOverlayEntryType.CX, EOverlayEntryType.CX, EOverlayEntryType.CX },
                block.Select(entry => entry.OverlayEntryType).ToArray(),
                "all three must share a type, otherwise the type sort separates them");

            var comparer = new SortKeyComparer();
            Assert.AreEqual(0, comparer.Compare(block[0].SortKey, block[1].SortKey),
                "Framerate and Frametime must tie on SortKey");
            Assert.AreEqual(0, comparer.Compare(block[1].SortKey, block[2].SortKey),
                "Frametime and DisplayTime must tie on SortKey");
        }

        // ---------------------------------------------------------------- the three templates

        [DataTestMethod]
        [DataRow(EOverlayTemplate.Basic)]
        [DataRow(EOverlayTemplate.Detailed)]
        [DataRow(EOverlayTemplate.Enthusiast)]
        public void Template_DisplayTimeFollowsTheFramerateRows(EOverlayTemplate template)
        {
            var entries = CreateEntries();

            ApplyTemplate(template, entries);

            var sorted = OverlayUtils.SortForTemplate(entries)
                .Select(entry => entry.Identifier).ToList();

            AssertFramerateBlockOrder(sorted, template.ToString());
        }

        [DataTestMethod]
        [DataRow(EOverlayTemplate.Basic)]
        [DataRow(EOverlayTemplate.Detailed)]
        [DataRow(EOverlayTemplate.Enthusiast)]
        public void Template_RepairsAConfigurationThatHasDisplayTimeOnTop(EOverlayTemplate template)
        {
            // A configuration written by a build that ranked DisplayTime as a generic CX entry has it
            // at the very top. All CX entries share the SortKey "0_0_0_0_0", so a shared rank would
            // let the stable sort inherit exactly that broken order instead of repairing it.
            var entries = CreateEntries();
            var displayTime = entries.Single(entry => entry.Identifier == DisplayTime);
            entries.Remove(displayTime);
            entries.Insert(0, displayTime);

            ApplyTemplate(template, entries);

            var sorted = OverlayUtils.SortForTemplate(entries)
                .Select(entry => entry.Identifier).ToList();

            Assert.AreNotEqual(DisplayTime, sorted.First(),
                $"{template}: DisplayTime stayed at the top instead of moving under the framerate rows");
            AssertFramerateBlockOrder(sorted, template.ToString());
        }

        // ---------------------------------------------------------------- DisplayTime activation

        [DataTestMethod]
        [DataRow(EOverlayTemplate.Basic)]
        [DataRow(EOverlayTemplate.Detailed)]
        [DataRow(EOverlayTemplate.Enthusiast)]
        public void Template_EnablesDisplayTime_WhenAPresentMonSourceExists(EOverlayTemplate template)
        {
            // IsEntryEnabled == true is what OverlayEntryProvider sets for the CapFrameX overlay fed
            // by PresentMon (hook-free, or the in-game hook in PresentMon graph mode).
            var entries = CreateEntries();
            var displayTime = entries.Single(entry => entry.Identifier == DisplayTime);
            displayTime.IsEntryEnabled = true;

            ApplyTemplate(template, entries);

            Assert.IsTrue(displayTime.ShowOnOverlay, $"{template}: DisplayTime must be switched on");
            Assert.IsTrue(displayTime.ShowGraph, $"{template}: DisplayTime graph must be switched on");
        }

        [DataTestMethod]
        [DataRow(EOverlayTemplate.Basic)]
        [DataRow(EOverlayTemplate.Detailed)]
        [DataRow(EOverlayTemplate.Enthusiast)]
        public void Template_LeavesDisplayTimeOff_WithoutAPresentMonSource(EOverlayTemplate template)
        {
            // RTSS, or the in-game hook on its own present ring: no MsBetweenDisplayChange source.
            // Enthusiast used to switch the row on unconditionally here.
            var entries = CreateEntries();
            var displayTime = entries.Single(entry => entry.Identifier == DisplayTime);
            displayTime.IsEntryEnabled = false;

            ApplyTemplate(template, entries);

            Assert.IsFalse(displayTime.ShowOnOverlay,
                $"{template}: DisplayTime has no data source and must stay off");
        }

        [DataTestMethod]
        [DataRow(EOverlayTemplate.Basic)]
        [DataRow(EOverlayTemplate.Detailed)]
        [DataRow(EOverlayTemplate.Enthusiast)]
        public void Template_SurvivesAnEntryListWithoutDisplayTime(EOverlayTemplate template)
        {
            // Under RTSS the provider drops the entry from the list altogether.
            var entries = CreateEntries();
            entries.Remove(entries.Single(entry => entry.Identifier == DisplayTime));

            ApplyTemplate(template, entries);

            Assert.IsTrue(entries.Single(entry => entry.Identifier == Framerate).ShowOnOverlay,
                $"{template}: the framerate rows must still be applied");
        }

        [TestMethod]
        public void FramerateBlock_RanksBehindEveryOtherSection()
        {
            var others = CreateEntries()
                .Where(entry => !IsFramerateBlock(entry.Identifier))
                .Select(entry => OverlayUtils.GetTemplateSortOrder(entry))
                .ToList();
            var block = CreateEntries()
                .Where(entry => IsFramerateBlock(entry.Identifier))
                .Select(entry => OverlayUtils.GetTemplateSortOrder(entry))
                .ToList();

            Assert.IsTrue(block.Min() > others.Max(),
                "the framerate block must close the overlay, behind every other section");
            CollectionAssert.AllItemsAreUnique(block,
                "Framerate/Frametime/DisplayTime need distinct ranks, otherwise their relative " +
                "order is inherited from the incoming list instead of being enforced");
        }

        // ---------------------------------------------------------------- helpers

        private static void ApplyTemplate(EOverlayTemplate template, List<IOverlayEntry> entries)
            => new OverlayTemplateService(new Mock<ISensorService>().Object)
                .ApplyTemplate(template, entries);

        private static bool IsFramerateBlock(string identifier)
            => identifier == Framerate || identifier == Frametime || identifier == DisplayTime;

        private static void AssertFramerateBlockOrder(IList<string> identifiers, string context)
        {
            int framerate = identifiers.IndexOf(Framerate);
            int frametime = identifiers.IndexOf(Frametime);
            int displayTime = identifiers.IndexOf(DisplayTime);

            Assert.IsTrue(framerate >= 0, $"{context}: Framerate missing from the list");
            Assert.IsTrue(frametime >= 0, $"{context}: Frametime missing from the list");
            Assert.IsTrue(displayTime >= 0, $"{context}: DisplayTime missing from the list");

            Assert.AreEqual(framerate + 1, frametime,
                $"{context}: Frametime must directly follow Framerate");
            Assert.AreEqual(frametime + 1, displayTime,
                $"{context}: DisplayTime must sit directly beneath the framerate rows");
        }

        // A cross section of the real entry set: the framerate block plus one entry of every other
        // section, so the ranking is exercised against actual neighbours rather than in isolation.
        private static List<IOverlayEntry> CreateEntries()
        {
            return new List<IOverlayEntry>
            {
                Cx("CaptureServiceStatus", "Status:"),
                Cx(Framerate, "<APP>"),
                Cx(Frametime, "<APP>"),
                Cx(DisplayTime, "Displaytime"),
                Cx("CustomCPU", "CPU Info"),
                Cx("CustomRAM", "RAM Info"),
                Sensor("/gpu-nvidia/0/clock/0", "GPU Core (MHz)", "GPU", EOverlayEntryType.GPU),
                Sensor("/intelcpu/0/clock/1", "Core #1 (MHz)", "Core #1", EOverlayEntryType.CPU),
                Sensor("/ram/data/0", "RAM Used (GB)", "RAM Used", EOverlayEntryType.RAM),
                Metric("OnlineAverage", "Average"),
                Metric("Online1PercentLow", "1% Low")
            };
        }

        private static IOverlayEntry Cx(string identifier, string groupName)
            => new OverlayEntryWrapper(identifier)
            {
                OverlayEntryType = EOverlayEntryType.CX,
                Description = identifier,
                GroupName = groupName,
                IsEntryEnabled = true,
                ShowOnOverlayIsEnabled = true
            };

        private static IOverlayEntry Sensor(string identifier, string description, string groupName,
            EOverlayEntryType type)
            => new OverlayEntryWrapper(identifier)
            {
                OverlayEntryType = type,
                Description = description,
                GroupName = groupName,
                IsEntryEnabled = true,
                ShowOnOverlayIsEnabled = true
            };

        private static IOverlayEntry Metric(string identifier, string groupName)
            => new OverlayEntryWrapper(identifier)
            {
                OverlayEntryType = EOverlayEntryType.OnlineMetric,
                Description = identifier,
                GroupName = groupName,
                IsEntryEnabled = true,
                ShowOnOverlayIsEnabled = true
            };
    }
}

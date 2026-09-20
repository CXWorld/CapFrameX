using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Overlay;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Test.Overlay
{
    [TestClass]
    public class DisplayResolutionEntryTest
    {
        [TestMethod]
        public void Defaults_RenameLegacyResolutionLabelWithoutChangingItsIdentifier()
        {
            var entry = GetDefaults().Single(item => item.Identifier == "Resolution");

            Assert.AreEqual("Resolution", entry.Identifier,
                "the native in-game renderer still uses the legacy identifier for substitution");
            Assert.AreEqual("Present Resolution", entry.Description);
            Assert.AreEqual("Present Resolution", entry.GroupName);
        }

        [TestMethod]
        public void LegacyPresentResolutionMetadata_MigratesOnlyTheDefaultGroupName()
        {
            var legacy = new OverlayEntryWrapper("Resolution")
            {
                Description = "Resolution",
                GroupName = "Resolution"
            };
            var customized = new OverlayEntryWrapper("Resolution")
            {
                Description = "Resolution",
                GroupName = "My Game Output"
            };

            Assert.IsTrue(OverlayEntryProvider.RefreshPresentResolutionMetadata(legacy));
            Assert.AreEqual("Present Resolution", legacy.Description);
            Assert.AreEqual("Present Resolution", legacy.GroupName);

            Assert.IsTrue(OverlayEntryProvider.RefreshPresentResolutionMetadata(customized));
            Assert.AreEqual("Present Resolution", customized.Description);
            Assert.AreEqual("My Game Output", customized.GroupName,
                "a user-customized OSD label must survive the migration");
        }

        [TestMethod]
        public void ReconcileDisplayEntries_AddsOneHiddenItemPerDetectedDisplayBesidePresentResolution()
        {
            var entries = GetDefaults();
            var displays = new[]
            {
                Display(1, 2560, 1440, isPrimary: true),
                Display(2, 3840, 2160)
            };

            Assert.IsTrue(OverlayEntryProvider.ReconcileDisplayResolutionEntries(entries, displays));

            int presentIndex = entries.FindIndex(entry => entry.Identifier == "Resolution");
            var displayEntries = entries
                .Where(entry => OverlayEntryProvider.IsDisplayResolutionIdentifier(entry.Identifier))
                .ToArray();

            Assert.AreEqual(2, displayEntries.Length);
            Assert.AreSame(displayEntries[0], entries[presentIndex + 1]);
            Assert.AreSame(displayEntries[1], entries[presentIndex + 2]);
            AssertDisplayEntry(displayEntries[0], displays[0], "Display 1 Resolution", "2560x1440");
            AssertDisplayEntry(displayEntries[1], displays[1], "Display 2 Resolution", "3840x2160");
        }

        [TestMethod]
        public void ReconcileDisplayEntries_UpdatesValuesAndPreservesUserFormattingAcrossHotPlug()
        {
            var entries = GetDefaults();
            var display1 = Display(1, 1920, 1080, isPrimary: true);
            var display2 = Display(2, 2560, 1440);
            OverlayEntryProvider.ReconcileDisplayResolutionEntries(entries,
                new[] { display1, display2 });

            var firstEntry = entries.Single(entry => entry.Identifier ==
                OverlayEntryProvider.GetDisplayResolutionIdentifier(display1.DeviceName));
            firstEntry.ShowOnOverlay = true;
            firstEntry.GroupName = "Main Monitor";
            firstEntry.Color = "12AB34";

            var resizedDisplay1 = Display(1, 3440, 1440, isPrimary: true);
            var display3 = Display(3, 3840, 2160);
            Assert.IsTrue(OverlayEntryProvider.ReconcileDisplayResolutionEntries(entries,
                new[] { resizedDisplay1, display3 }));

            var retainedEntry = entries.Single(entry => entry.Identifier == firstEntry.Identifier);
            Assert.AreSame(firstEntry, retainedEntry);
            Assert.AreEqual("3440x1440", retainedEntry.Value);
            Assert.IsTrue(retainedEntry.ShowOnOverlay);
            Assert.AreEqual("Main Monitor", retainedEntry.GroupName);
            Assert.AreEqual("12AB34", retainedEntry.Color);
            Assert.IsFalse(entries.Any(entry => entry.Identifier ==
                OverlayEntryProvider.GetDisplayResolutionIdentifier(display2.DeviceName)));

            var addedEntry = entries.Single(entry => entry.Identifier ==
                OverlayEntryProvider.GetDisplayResolutionIdentifier(display3.DeviceName));
            Assert.IsFalse(addedEntry.ShowOnOverlay,
                "a newly connected display must not unexpectedly add a row to the live OSD");
        }

        [TestMethod]
        public void ReconcileDisplayEntries_ResolutionOnlyChangeDoesNotRebuildTheEntryList()
        {
            var entries = GetDefaults();
            var original = Display(1, 1920, 1080, isPrimary: true);
            OverlayEntryProvider.ReconcileDisplayResolutionEntries(entries, new[] { original });
            var entry = entries.Single(item =>
                OverlayEntryProvider.IsDisplayResolutionIdentifier(item.Identifier));
            int propertyChanges = 0;
            entry.PropertyChangedAction = () => propertyChanges++;

            bool structureChanged = OverlayEntryProvider.ReconcileDisplayResolutionEntries(entries,
                new[] { Display(1, 2560, 1440, isPrimary: true) });

            Assert.IsFalse(structureChanged);
            Assert.AreSame(entry, entries.Single(item =>
                OverlayEntryProvider.IsDisplayResolutionIdentifier(item.Identifier)));
            Assert.AreEqual("2560x1440", entry.Value);
            Assert.AreEqual(0, propertyChanges,
                "a normal value refresh must not mark the overlay configuration as edited");
        }

        private static List<IOverlayEntry> GetDefaults()
        {
            var configuration = new Mock<IAppConfiguration>();
            return OverlayUtils.GetOverlayEntryDefaults(configuration.Object)
                .Cast<IOverlayEntry>()
                .ToList();
        }

        private static DetectedDisplay Display(int number, int width, int height,
            bool isPrimary = false)
            => new DetectedDisplay($@"\\.\DISPLAY{number}", width, height, isPrimary);

        private static void AssertDisplayEntry(IOverlayEntry entry, DetectedDisplay display,
            string label, string resolution)
        {
            Assert.AreEqual(OverlayEntryProvider.GetDisplayResolutionIdentifier(display.DeviceName),
                entry.Identifier);
            Assert.AreEqual(display.DeviceName, entry.StableIdentifier);
            Assert.AreEqual(label, entry.Description);
            Assert.AreEqual(label, entry.GroupName);
            Assert.AreEqual(resolution, entry.Value);
            Assert.AreEqual(EOverlayEntryType.CX, entry.OverlayEntryType);
            Assert.IsTrue(entry.IsEntryEnabled);
            Assert.IsTrue(entry.ShowOnOverlayIsEnabled);
            Assert.IsFalse(entry.ShowOnOverlay);
            Assert.IsFalse(entry.ShowGraph);
            Assert.IsFalse(entry.ShowGraphIsEnabled);
        }
    }
}

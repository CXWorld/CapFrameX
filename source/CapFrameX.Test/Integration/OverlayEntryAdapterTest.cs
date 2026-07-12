using System.Linq;
using CapFrameX.OSD.Integration;
using CapFrameX.Overlay;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
    }
}

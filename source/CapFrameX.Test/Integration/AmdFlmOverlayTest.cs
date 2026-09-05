using System.Linq;
using CapFrameX.ApiInterface;
using CapFrameX.Contracts.Latency;
using CapFrameX.Contracts.Overlay;
using CapFrameX.OSD.Integration;
using CapFrameX.Overlay;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class AmdFlmOverlayTest
    {
        [TestMethod]
        public void MissingMeasurement_ShowsNaInAllOverlayFormats()
        {
            foreach (string identifier in new[] { "OnlineAmdFlmLatency", AmdFlmSensorMetadata.Identifier })
            foreach (object value in new object[] { null, double.NaN, float.NaN, double.PositiveInfinity, 0d })
            {
                var entry = CreateEntry(identifier, value);
                var native = OverlayEntryAdapter.ToOsdEntries(new[] { entry }).Single();
                Assert.IsFalse(native.IsNumeric);
                Assert.AreEqual("N/A", native.ValueText);
                Assert.IsFalse(native.ShowGraph);
                Assert.AreEqual("N/A", entry.FormattedValue);
                var service = new Mock<IOverlayService>();
                service.SetupGet(s => s.CurrentOverlayEntries).Returns(new[] { entry });
                StringAssert.Contains(OSDController.GetEntries(service.Object, true).Single(), "N/A");
            }
        }

        [TestMethod]
        public void ValidMeasurement_PreservesTheNumericValueAndUnits()
        {
            var entry = CreateEntry("OnlineAmdFlmLatency", 23.5d);
            var native = OverlayEntryAdapter.ToOsdEntries(new[] { entry }).Single();
            Assert.IsTrue(native.IsNumeric);
            Assert.AreEqual(23.5d, native.Value);
            Assert.AreEqual("ms", native.Unit);
            Assert.IsTrue(native.ShowGraph);
            StringAssert.Contains(entry.FormattedValue, "23.5");
        }

        private static OverlayEntryWrapper CreateEntry(string identifier, object value)
        {
            return new OverlayEntryWrapper(identifier)
            {
                Value = value, IsNumeric = true, IsEntryEnabled = true, ShowOnOverlay = true,
                ShowGraph = true, GroupName = "Latency", ValueAlignmentAndDigits = "{0:F1}",
                ValueUnitFormat = "ms", ValueFormat = "<S2>{0:F1}<S>ms"
            };
        }
    }
}

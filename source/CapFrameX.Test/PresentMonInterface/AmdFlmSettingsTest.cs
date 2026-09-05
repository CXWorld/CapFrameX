using CapFrameX.Contracts.Latency;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.PresentMonInterface
{
    [TestClass]
    public class AmdFlmSettingsTest
    {
        [TestMethod]
        public void Settings_KeepTheCaptureRegionWithinTheOutput()
        {
            var settings = new AmdFlmSettings(100, -1, .9, 1, .4, .5, 100);
            Assert.AreEqual(31, settings.CaptureOutputIndex);
            Assert.AreEqual(0, settings.CaptureMode);
            Assert.AreEqual(1, settings.StartX + settings.Width, .000001);
            Assert.AreEqual(1, settings.StartY + settings.Height, .000001);
            Assert.AreEqual(10, settings.ThresholdCoefficient);
        }

        [TestMethod]
        public void Settings_ReplaceNonFinitePersistedValuesWithSafeDefaults()
        {
            var settings = new AmdFlmSettings(0, 0, double.NaN, double.PositiveInfinity, double.NaN, double.NegativeInfinity, double.NaN);
            Assert.AreEqual(.4, settings.StartX);
            Assert.AreEqual(.45, settings.StartY);
            Assert.AreEqual(.2, settings.Width);
            Assert.AreEqual(.25, settings.Height);
            Assert.AreEqual(3, settings.ThresholdCoefficient);
        }
    }
}

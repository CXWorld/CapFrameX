using CapFrameX.PresentMonInterface;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Globalization;
using System.Linq;

namespace CapFrameX.Test.PresentMonInterface
{
    [TestClass]
    public class CaptureServiceConfigurationTest
    {
        [TestMethod]
        public void ParseVersionFromAppName_BundledName_ReturnsVersionWithoutArchitecture()
        {
            Assert.AreEqual("2.6.1", CaptureServiceConfiguration.ParseVersionFromAppName(CaptureServiceConfiguration.PresentMonAppName));
            Assert.AreEqual("2.6.0", CaptureServiceConfiguration.ParseVersionFromAppName("PresentMon-2.6.0-x64"));
        }

        [TestMethod]
        public void ParseVersionFromAppName_NoVersion_ReturnsNull()
        {
            Assert.IsNull(CaptureServiceConfiguration.ParseVersionFromAppName("PresentMon-x64"));
            Assert.IsNull(CaptureServiceConfiguration.ParseVersionFromAppName(null));
        }

        [TestMethod]
        public void GetCaptureFilename_PadsTheTimeOfDay()
        {
            Assert.AreEqual("CapFrameX-game.exe-2026-10-15T101704.json",
                CaptureServiceConfiguration.GetCaptureFilename("game", new DateTime(2026, 10, 15, 10, 17, 4)));
            Assert.AreEqual("CapFrameX-game.exe-2026-01-05T000000.json",
                CaptureServiceConfiguration.GetCaptureFilename("game", new DateTime(2026, 1, 5)));
        }

        [TestMethod]
        public void GetCaptureFilename_DifferentTimesOfADay_GiveDifferentNames()
        {
            // Unpadded, all three read "T11111", so a later capture overwrote an earlier one.
            var names = new[]
            {
                new DateTime(2026, 10, 15, 1, 11, 11),
                new DateTime(2026, 10, 15, 11, 1, 11),
                new DateTime(2026, 10, 15, 11, 11, 1),
            }.Select(time => CaptureServiceConfiguration.GetCaptureFilename("game", time)).ToArray();

            Assert.AreEqual(3, names.Distinct().Count(), string.Join(", ", names));
        }

        [TestMethod]
        public void FormatFileTimestamp_IgnoresTheCurrentCulture()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                // The Thai culture counts years in the Buddhist era (2026 is 2569).
                CultureInfo.CurrentCulture = new CultureInfo("th-TH");
                Assert.AreEqual("2026-10-15T101704",
                    CaptureServiceConfiguration.FormatFileTimestamp(new DateTime(2026, 10, 15, 10, 17, 4)));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }
    }
}

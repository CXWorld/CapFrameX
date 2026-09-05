using System.Runtime.InteropServices;
using LibreHardwareMonitor.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Sensor
{
    [TestClass]
    public class NvidiaMlPowerSampleTest
    {
        [TestMethod]
        public void NvmlSample_MatchesNativeLayout()
        {
            Assert.AreEqual(8, Marshal.SizeOf<NvidiaML.NvmlValue>());
            Assert.AreEqual(16, Marshal.SizeOf<NvidiaML.NvmlSample>());
            Assert.AreEqual(8, Marshal.OffsetOf<NvidiaML.NvmlSample>(nameof(NvidiaML.NvmlSample.SampleValue)).ToInt32());
        }

        [TestMethod]
        public void GetLatestPowerSample_ReturnsNewestSampleAndUpdatesTimestamp()
        {
            NvidiaML.NvmlSample[] samples =
            {
                CreateSample(100, 52000),
                CreateSample(300, 57000),
                CreateSample(200, 54000)
            };
            ulong lastSeenTimestamp = 0;

            int? powerUsage = NvidiaML.GetLatestPowerSample(
                samples,
                (uint)samples.Length,
                NvidiaML.NvmlValueType.UnsignedInt,
                ref lastSeenTimestamp);

            Assert.AreEqual(57000, powerUsage);
            Assert.AreEqual(300UL, lastSeenTimestamp);
        }

        [TestMethod]
        public void GetLatestPowerSample_IgnoresSamplesAlreadySeen()
        {
            NvidiaML.NvmlSample[] samples =
            {
                CreateSample(100, 52000),
                CreateSample(200, 54000)
            };
            ulong lastSeenTimestamp = 200;

            int? powerUsage = NvidiaML.GetLatestPowerSample(
                samples,
                (uint)samples.Length,
                NvidiaML.NvmlValueType.UnsignedInt,
                ref lastSeenTimestamp);

            Assert.IsNull(powerUsage);
            Assert.AreEqual(200UL, lastSeenTimestamp);
        }

        [TestMethod]
        public void GetLatestPowerSample_RejectsUnexpectedValueType()
        {
            NvidiaML.NvmlSample[] samples = { CreateSample(100, 52000) };
            ulong lastSeenTimestamp = 0;

            int? powerUsage = NvidiaML.GetLatestPowerSample(
                samples,
                (uint)samples.Length,
                NvidiaML.NvmlValueType.Double,
                ref lastSeenTimestamp);

            Assert.IsNull(powerUsage);
            Assert.AreEqual(0UL, lastSeenTimestamp);
        }

        private static NvidiaML.NvmlSample CreateSample(ulong timestamp, uint powerUsage)
        {
            return new NvidiaML.NvmlSample
            {
                Timestamp = timestamp,
                SampleValue = new NvidiaML.NvmlValue
                {
                    UnsignedInt = powerUsage
                }
            };
        }
    }
}

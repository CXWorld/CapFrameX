using System;
using LibreHardwareMonitor.PawnIo;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Sensor
{
    [TestClass]
    public class NvidiaThermalTest
    {
        private const int OutputLength = 50;
        private const int SensorCount = 48;

        [TestMethod]
        public void TryDecodeMemoryJunctionTemperature_ReturnsHottestAvailableSensor()
        {
            long[] output = CreateOutput();
            output[2] = 64;
            output[17] = 92;
            output[49] = 78;

            bool success = NvidiaThermal.TryDecodeMemoryJunctionTemperature(output, OutputLength, out float? temperature);

            Assert.IsTrue(success);
            Assert.IsTrue(temperature.HasValue);
            Assert.AreEqual(92f, temperature.GetValueOrDefault());
        }

        [TestMethod]
        public void TryDecodeMemoryJunctionTemperature_AllSensorsUnavailable_ReturnsNull()
        {
            long[] output = CreateOutput();

            bool success = NvidiaThermal.TryDecodeMemoryJunctionTemperature(output, OutputLength, out float? temperature);

            Assert.IsTrue(success);
            Assert.IsNull(temperature);
        }

        [TestMethod]
        public void TryDecodeMemoryJunctionTemperature_TruncatedPayload_ReturnsFalse()
        {
            long[] output = CreateOutput();

            bool success = NvidiaThermal.TryDecodeMemoryJunctionTemperature(output, OutputLength - 1, out float? temperature);

            Assert.IsFalse(success);
            Assert.IsNull(temperature);
        }

        [TestMethod]
        public void TryDecodeMemoryJunctionTemperature_InvalidSensorCount_ReturnsFalse()
        {
            long[] output = CreateOutput();
            output[1] = SensorCount + 1;

            bool success = NvidiaThermal.TryDecodeMemoryJunctionTemperature(output, OutputLength, out float? temperature);

            Assert.IsFalse(success);
            Assert.IsNull(temperature);
        }

        private static long[] CreateOutput()
        {
            long[] output = new long[OutputLength];
            output[1] = SensorCount;

            for (int i = 2; i < output.Length; i++)
                output[i] = int.MinValue;

            return output;
        }
    }
}

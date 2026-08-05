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
        public void TryDecodeMemoryTemperatures_ReturnsEveryAvailableSensorAndHottestValue()
        {
            long[] output = CreateOutput();
            output[2] = 64;
            output[17] = 92;
            output[49] = 78;
            var temperatures = new float?[SensorCount];

            bool success = NvidiaThermal.TryDecodeMemoryTemperatures(
                output,
                OutputLength,
                temperatures,
                out float? junctionTemperature);

            Assert.IsTrue(success);
            Assert.AreEqual(64f, temperatures[0]);
            Assert.AreEqual(92f, temperatures[15]);
            Assert.AreEqual(78f, temperatures[47]);
            Assert.IsNull(temperatures[1]);
            Assert.AreEqual(92f, junctionTemperature);
        }

        [TestMethod]
        public void TryDecodeMemoryTemperatures_AllSensorsUnavailable_ReturnsNullValues()
        {
            long[] output = CreateOutput();
            var temperatures = new float?[SensorCount];

            bool success = NvidiaThermal.TryDecodeMemoryTemperatures(
                output,
                OutputLength,
                temperatures,
                out float? junctionTemperature);

            Assert.IsTrue(success);
            Assert.IsNull(junctionTemperature);
            foreach (float? temperature in temperatures)
                Assert.IsNull(temperature);
        }

        [TestMethod]
        public void TryDecodeMemoryTemperatures_TruncatedPayload_ReturnsFalseAndClearsValues()
        {
            long[] output = CreateOutput();
            var temperatures = new float?[SensorCount];
            temperatures[0] = 99;

            bool success = NvidiaThermal.TryDecodeMemoryTemperatures(
                output,
                OutputLength - 1,
                temperatures,
                out float? junctionTemperature);

            Assert.IsFalse(success);
            Assert.IsNull(junctionTemperature);
            Assert.IsNull(temperatures[0]);
        }

        [TestMethod]
        public void TryDecodeMemoryTemperatures_InvalidSensorCount_ReturnsFalse()
        {
            long[] output = CreateOutput();
            output[1] = SensorCount + 1;
            var temperatures = new float?[SensorCount];

            bool success = NvidiaThermal.TryDecodeMemoryTemperatures(
                output,
                OutputLength,
                temperatures,
                out float? junctionTemperature);

            Assert.IsFalse(success);
            Assert.IsNull(junctionTemperature);
        }

        [TestMethod]
        public void TryDecodeMemoryTemperatures_ValueOutsideInt32_ReturnsFalseAndClearsValues()
        {
            long[] output = CreateOutput();
            output[2] = 70;
            output[3] = (long)int.MaxValue + 1;
            var temperatures = new float?[SensorCount];

            bool success = NvidiaThermal.TryDecodeMemoryTemperatures(
                output,
                OutputLength,
                temperatures,
                out float? junctionTemperature);

            Assert.IsFalse(success);
            Assert.IsNull(junctionTemperature);
            Assert.IsNull(temperatures[0]);
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

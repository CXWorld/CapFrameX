using System;
using System.Reflection;
using LibreHardwareMonitor.Hardware.Memory;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RAMSPDToolkit.SPD;

namespace CapFrameX.Test.Sensor
{
    [TestClass]
    public class DimmTemperatureConversionTest
    {
        [TestMethod]
        public void Ddr5TemperatureConversionMatchesTheToolkit()
        {
            // DimmMemory reads MR49/MR50 itself and converts like the toolkit's internal converter.
            Type converter = typeof(DDR5Accessor).Assembly.GetType("RAMSPDToolkit.Utilities.SPDTemperatureConverter", throwOnError: true);
            MethodInfo convert = converter.GetMethod("CheckAndConvertTemperature", BindingFlags.Public | BindingFlags.Static);

            for (int raw = 0; raw <= ushort.MaxValue; raw++)
            {
                float expected = (float)convert.Invoke(null, new object[] { (ushort)raw });
                Assert.AreEqual(expected, DimmMemory.ConvertTemperature((ushort)raw), $"raw 0x{raw:X4}");
            }

            Assert.AreEqual(37.75f, DimmMemory.ConvertTemperature(0x025C));
        }
    }
}

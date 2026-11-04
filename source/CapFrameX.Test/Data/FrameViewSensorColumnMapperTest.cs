using CapFrameX.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Test.Data
{
    [TestClass]
    public class FrameViewSensorColumnMapperTest
    {
        [TestMethod]
        public void MapColumns_FrameView12Headers_MapsSensorColumns()
        {
            // FrameView 1.2 log header layout (no whitespace before the unit parenthesis)
            var metrics = new[]
            {
                "Application", "GPU", "CPU", "TimeInSeconds", "MsBetweenPresents",
                "GPU0Clk(MHz)", "GPU0MemClk(MHz)", "GPU0Util(%)", "GPU0Temp(C)",
                "NV Pwr(W) (API)", "GPUOnlyPwr(W) (API)",
                "CPUClk(MHz)", "CPUUtil(%)", "CPU Package Temp(C)", "CPU Package Power(W)"
            };

            var sensorColumns = FrameViewSensorColumnMapper.MapColumns(metrics);

            Assert.AreEqual(10, sensorColumns.Count);

            var byKey = sensorColumns.ToDictionary(c => c.ColumnIndex);
            Assert.AreEqual(("GPU Core", "Clock"), (byKey[5].Name, byKey[5].Type));
            Assert.AreEqual(("GPU Memory", "Clock"), (byKey[6].Name, byKey[6].Type));
            Assert.AreEqual(("GPU Core", "Load"), (byKey[7].Name, byKey[7].Type));
            Assert.AreEqual(("GPU Core", "Temperature"), (byKey[8].Name, byKey[8].Type));
            Assert.AreEqual(("GPU Power", "Power"), (byKey[9].Name, byKey[9].Type));
            Assert.AreEqual(("GPU Chip Power", "Power"), (byKey[10].Name, byKey[10].Type));
            Assert.AreEqual(("CPU Average", "Clock"), (byKey[11].Name, byKey[11].Type));
            Assert.AreEqual(("CPU Total", "Load"), (byKey[12].Name, byKey[12].Type));
            Assert.AreEqual(("CPU Package", "Temperature"), (byKey[13].Name, byKey[13].Type));
            Assert.AreEqual(("CPU Package", "Power"), (byKey[14].Name, byKey[14].Type));

            Assert.IsTrue(sensorColumns.All(c => c.Key != null && c.Key.Length > 0));
        }

        [TestMethod]
        public void MapColumns_FrameView14Headers_MapsSensorColumns()
        {
            // FrameView 1.4+ log header layout (whitespace inside the column headers)
            var metrics = new[]
            {
                "Application", "TimeInSeconds", "MsBetweenPresents",
                "GPU0Clk (MHz)", "GPU0MemClock (MHz)", "GPU0Util (%)", "GPU0Temp (C)",
                "NV Pwr(W) (API)", "PCAT Power Total (W)"
            };

            var sensorColumns = FrameViewSensorColumnMapper.MapColumns(metrics);

            Assert.AreEqual(6, sensorColumns.Count);

            var byKey = sensorColumns.ToDictionary(c => c.ColumnIndex);
            Assert.AreEqual(("GPU Core", "Clock"), (byKey[3].Name, byKey[3].Type));
            Assert.AreEqual(("GPU Memory", "Clock"), (byKey[4].Name, byKey[4].Type));
            Assert.AreEqual(("GPU Core", "Load"), (byKey[5].Name, byKey[5].Type));
            Assert.AreEqual(("GPU Core", "Temperature"), (byKey[6].Name, byKey[6].Type));
            Assert.AreEqual(("GPU Power", "Power"), (byKey[7].Name, byKey[7].Type));
            // PCAT is an actual measurement, so it is named to match the "GPU Power" lookup as well
            // and marked as one: the import adds it first although its column comes last here.
            Assert.AreEqual(("GPU Power (PCAT)", "Power"), (byKey[8].Name, byKey[8].Type));
            Assert.IsTrue(byKey[8].IsMeasurement);
            Assert.IsFalse(byKey[7].IsMeasurement);
        }

        [TestMethod]
        public void MapColumns_DegreeCelsiusVariant_IsMapped()
        {
            var sensorColumns = FrameViewSensorColumnMapper.MapColumns(new[] { "GPU0Temp (°C)" });

            var column = AssertSingleSensorColumn(sensorColumns);
            Assert.AreEqual(("GPU Core", "Temperature"), (column.Name, column.Type));
        }

        [TestMethod]
        public void MapColumns_FrameView17ClusterPowerHeaders_MapsSensorColumns()
        {
            // FrameView 1.7: per-cluster core power (E-Core / P-Core)
            var metrics = new[]
            {
                "TimeInSeconds", "CPU Package Power (Watts)", "E-Core Power (W)",
                "P-Core Power (W)"
            };

            var sensorColumns = FrameViewSensorColumnMapper.MapColumns(metrics);

            Assert.AreEqual(3, sensorColumns.Count);

            var byKey = sensorColumns.ToDictionary(c => c.ColumnIndex);
            Assert.AreEqual(("CPU Package", "Power"), (byKey[1].Name, byKey[1].Type));
            Assert.AreEqual(("E-Core Power", "Power"), (byKey[2].Name, byKey[2].Type));
            Assert.AreEqual(("P-Core Power", "Power"), (byKey[3].Name, byKey[3].Type));
        }

        [TestMethod]
        public void MapColumns_BatteryHeaders_MapsSensorColumns()
        {
            var metrics = new[]
            {
                "TimeInSeconds", "Battery Percentage", "Battery Drain Rate (W)"
            };

            var sensorColumns = FrameViewSensorColumnMapper.MapColumns(metrics);

            Assert.AreEqual(2, sensorColumns.Count);

            // LibreHardwareMonitor's names (Battery.cs), so native and imported records share the
            // rows of the sensor tab; FrameView's drain rate is negative while discharging.
            var byKey = sensorColumns.ToDictionary(c => c.ColumnIndex);
            Assert.AreEqual(("Charge Level", "Level"), (byKey[1].Name, byKey[1].Type));
            Assert.AreEqual(1, byKey[1].Factor);
            Assert.AreEqual(("Discharge Rate", "Power"), (byKey[2].Name, byKey[2].Type));
            Assert.AreEqual(-1, byKey[2].Factor);
        }

        [TestMethod]
        public void MapColumns_TwoGpus_OnlyTheLowestIndexIsTheGpuCore()
        {
            var metrics = new[] { "GPU0Clk(MHz)", "GPU0MemClk(MHz)", "GPU1Clk(MHz)", "GPU1MemClk(MHz)", "GPU1Util(%)" };

            var sensorColumns = FrameViewSensorColumnMapper.MapColumns(metrics);

            CollectionAssert.AreEqual(new[] { "GPU Core", "GPU Memory", "GPU1 Core", "GPU1 Memory", "GPU1 Core" },
                sensorColumns.Select(column => column.Name).ToArray());
            CollectionAssert.AreEqual(new[] { 0, 0, 1, 1, 1 }, sensorColumns.Select(column => column.GpuIndex).ToArray());
        }

        [TestMethod]
        public void NameGpuColumns_RenamesEveryGpuButThePrimary()
        {
            var sensorColumns = FrameViewSensorColumnMapper.MapColumns(
                new[] { "GPU0Clk(MHz)", "GPU0Util(%)", "GPU1Clk(MHz)", "GPU1Util(%)", "CPUUtil(%)" });

            FrameViewSensorColumnMapper.NameGpuColumns(sensorColumns, 1);

            CollectionAssert.AreEqual(new[] { "GPU0 Core", "GPU0 Core", "GPU Core", "GPU Core", "CPU Total" },
                sensorColumns.Select(column => column.Name).ToArray());
        }

        [TestMethod]
        [DataRow(3.0, 97.0, 1, DisplayName = "The busy GPU1 wins over the idle GPU0")]
        [DataRow(95.0, 4.0, 0, DisplayName = "The busy GPU0 wins over the idle GPU1")]
        [DataRow(50.0, 50.0, 0, DisplayName = "A tie goes to the lower index")]
        [DataRow(double.NaN, 40.0, 1, DisplayName = "A GPU without values does not count")]
        [DataRow(80.0, double.NaN, 0, DisplayName = "The only GPU with values")]
        [DataRow(double.NaN, double.NaN, -1, DisplayName = "No GPU with values")]
        public void SelectPrimaryGpu_TakesTheGpuWithTheHighestLoad(double gpu0Load, double gpu1Load, int expected)
        {
            var sensorColumns = FrameViewSensorColumnMapper.MapColumns(new[] { "GPU0Util(%)", "GPU1Util(%)" });
            var values = new[] { Readings(gpu0Load), Readings(gpu1Load) };

            Assert.AreEqual(expected, FrameViewSensorColumnMapper.SelectPrimaryGpu(sensorColumns, values));
        }

        [TestMethod]
        public void SelectPrimaryGpu_WithoutLoadValues_TakesTheLowestIndexWithValues()
        {
            var sensorColumns = FrameViewSensorColumnMapper.MapColumns(new[] { "GPU0Clk(MHz)", "GPU1Clk(MHz)", "GPU2Clk(MHz)" });
            var values = new[] { Readings(double.NaN), Readings(1500), Readings(2100) };

            Assert.AreEqual(1, FrameViewSensorColumnMapper.SelectPrimaryGpu(sensorColumns, values));
        }

        [TestMethod]
        public void SelectPrimaryGpu_ALoadValueWinsOverAGpuWithoutOne()
        {
            // GPU0 reports a clock but no load, GPU1 a load: the load decides.
            var sensorColumns = FrameViewSensorColumnMapper.MapColumns(new[] { "GPU0Clk(MHz)", "GPU1Util(%)" });
            var values = new[] { Readings(600), Readings(2) };

            Assert.AreEqual(1, FrameViewSensorColumnMapper.SelectPrimaryGpu(sensorColumns, values));
        }

        [TestMethod]
        public void MapColumns_AMDPwr_MapsToGpuPower()
        {
            // Radeon boards report power via "AMDPwr (W) (API)"; map it to the GPU Power metric
            // so the analysis picks it up (the "GPU Power" lookup matches that name).
            var sensorColumns = FrameViewSensorColumnMapper.MapColumns(new[] { "AMDPwr (W) (API)" });

            var column = AssertSingleSensorColumn(sensorColumns);
            Assert.AreEqual(("GPU Power", "Power"), (column.Name, column.Type));
        }

        [TestMethod]
        public void MapColumns_PresentMonHeader_MatchesNothing()
        {
            // PresentMon/MangoHud style logs carry no embedded FrameView sensor columns;
            // the import must take the same code path as before (empty list).
            var metrics = new[]
            {
                "Application", "ProcessID", "SwapChainAddress", "Runtime", "PresentFlags",
                "SyncInterval", "Dropped", "TimeInSeconds", "MsBetweenPresents",
                "MsBetweenDisplayChange", "MsInPresentAPI", "MsUntilRenderComplete", "AllowsTearing"
            };

            var sensorColumns = FrameViewSensorColumnMapper.MapColumns(metrics);

            Assert.AreEqual(0, sensorColumns.Count);
        }

        [TestMethod]
        public void MapColumns_NullInput_ReturnsEmptyList()
        {
            var sensorColumns = FrameViewSensorColumnMapper.MapColumns(null);

            Assert.IsNotNull(sensorColumns);
            Assert.AreEqual(0, sensorColumns.Count);
        }

        [TestMethod]
        public void MapColumns_DuplicateHeader_KeepsFirstOccurrence()
        {
            var metrics = new[] { "GPU0Clk (MHz)", "TimeInSeconds", "GPU0Clk(MHz)" };

            var sensorColumns = FrameViewSensorColumnMapper.MapColumns(metrics);

            var column = AssertSingleSensorColumn(sensorColumns);
            Assert.AreEqual(0, column.ColumnIndex);
        }

        // A series of a column; NaN stands for a column that reported nothing.
        private static double[] Readings(double value) => Enumerable.Repeat(value, 8).ToArray();

        private static FrameViewSensorColumnMapper.SensorColumn AssertSingleSensorColumn(
            List<FrameViewSensorColumnMapper.SensorColumn> sensorColumns)
        {
            Assert.AreEqual(1, sensorColumns.Count);
            return sensorColumns[0];
        }
    }
}

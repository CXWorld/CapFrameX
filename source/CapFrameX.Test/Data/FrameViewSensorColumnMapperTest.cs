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
            Assert.AreEqual(("PCAT Power Total", "Power"), (byKey[8].Name, byKey[8].Type));
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
            // FrameView 1.7: Intel hybrid CPU cluster power and the printed "AMDPtr" variant
            var metrics = new[]
            {
                "TimeInSeconds", "CPU Package Power (Watts)", "E-Core Power (W)",
                "P-Core Power (W)", "AMDPtr (W) (API)"
            };

            var sensorColumns = FrameViewSensorColumnMapper.MapColumns(metrics);

            Assert.AreEqual(4, sensorColumns.Count);

            var byKey = sensorColumns.ToDictionary(c => c.ColumnIndex);
            Assert.AreEqual(("CPU Package", "Power"), (byKey[1].Name, byKey[1].Type));
            Assert.AreEqual(("E-Core Power", "Power"), (byKey[2].Name, byKey[2].Type));
            Assert.AreEqual(("P-Core Power", "Power"), (byKey[3].Name, byKey[3].Type));
            Assert.AreEqual(("AMD Board Power", "Power"), (byKey[4].Name, byKey[4].Type));
        }

        [TestMethod]
        public void MapColumns_BatteryHeaders_MapsSensorColumns()
        {
            var metrics = new[]
            {
                "TimeInSeconds", "Battery Percentage", "Battery Drain Rate (W)",
                "Battery Charge Rate (W)"
            };

            var sensorColumns = FrameViewSensorColumnMapper.MapColumns(metrics);

            Assert.AreEqual(3, sensorColumns.Count);

            var byKey = sensorColumns.ToDictionary(c => c.ColumnIndex);
            Assert.AreEqual(("Battery Charge Level", "Level"), (byKey[1].Name, byKey[1].Type));
            Assert.AreEqual(("Battery Drain Rate", "Power"), (byKey[2].Name, byKey[2].Type));
            Assert.AreEqual(("Battery Charge Rate", "Power"), (byKey[3].Name, byKey[3].Type));
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

        private static FrameViewSensorColumnMapper.SensorColumn AssertSingleSensorColumn(
            List<FrameViewSensorColumnMapper.SensorColumn> sensorColumns)
        {
            Assert.AreEqual(1, sensorColumns.Count);
            return sensorColumns[0];
        }
    }
}

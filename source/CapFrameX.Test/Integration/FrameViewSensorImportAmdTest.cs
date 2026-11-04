using CapFrameX.Sensor.Reporting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Test.Integration
{
    /// <summary>
    /// <see cref="FrameViewSensorImportTest"/> with Mafia: The Old Country on an RX 9070 XT and a Ryzen 7
    /// 9800X3D: 20 s, 2244 frames. FrameView logs the integrated graphics of the Ryzen as GPU0 (600 MHz,
    /// no load) and the RX 9070 XT, which renders the game, as GPU1. It polls the GPUs and the CPU load
    /// about every 0.5 s and the CPU power about every 0.2 s, and logs the CPU package temperature with a
    /// leading blank (" 63").
    /// AMDPwr is not the power of the RX 9070 XT: 79 W at full load, where a CapFrameX capture of the
    /// same game reads 292 W GPU TBP. It belongs to the integrated graphics: with them switched off in
    /// the BIOS, FrameView logs the card as GPU0 and AMDPwr stays NA. The import keeps it as "GPU Power",
    /// as FrameView reports it.
    /// </summary>
    [TestClass]
    public class FrameViewSensorImportAmdTest : FrameViewSensorImportTest
    {
        protected override string LogFile => @"TestRecordFiles\FrameView_MafiaTheOldCountry.exe_2026_11_02T152324_Log.csv";
        protected override string ProcessName => "MafiaTheOldCountry.exe";
        protected override string Gpu => "AMD Radeon RX 9070 XT";
        // FrameView logs the brand string of the CPU with its padding, and the import keeps it.
        protected override string Processor => "AMD Ryzen 7 9800X3D 8-Core Processor           ";
        protected override int FrameCount => 2244;
        protected override int CellCount => 80;
        protected override int PrimaryGpu => 1;
        protected override string GpuPowerColumn => "AMDPwr(W) (API)";

        // The GPU1 columns get the names the analysis reads, the GPU0 columns names of their own. The
        // NVIDIA and PCAT power, CPU TDP and the battery columns are NA throughout; the per-core loads
        // and the Perf/W columns are not imported.
        protected override IReadOnlyDictionary<string, (string Column, string Name, string Type)> LoggedSensors { get; } =
            new Dictionary<string, (string Column, string Name, string Type)>
            {
                ["gpu0clk(mhz)"] = ("GPU0Clk(MHz)", "GPU0 Core", "Clock"),
                ["gpu0memclk(mhz)"] = ("GPU0MemClk(MHz)", "GPU0 Memory", "Clock"),
                ["gpu0util(%)"] = ("GPU0Util(%)", "GPU0 Core", "Load"),
                ["gpu0temp(c)"] = ("GPU0Temp(C)", "GPU0 Core", "Temperature"),
                ["gpu1clk(mhz)"] = ("GPU1Clk(MHz)", "GPU Core", "Clock"),
                ["gpu1memclk(mhz)"] = ("GPU1MemClk(MHz)", "GPU Memory", "Clock"),
                ["gpu1util(%)"] = ("GPU1Util(%)", "GPU Core", "Load"),
                ["gpu1temp(c)"] = ("GPU1Temp(C)", "GPU Core", "Temperature"),
                ["amdpwr(w)(api)"] = ("AMDPwr(W) (API)", "GPU Power", "Power"),
                ["cpuclk(mhz)"] = ("CPUClk(MHz)", "CPU Average", "Clock"),
                ["cpuutil(%)"] = ("CPUUtil(%)", "CPU Total", "Load"),
                ["cpupackagetemp(c)"] = ("CPU Package Temp(C)", "CPU Package", "Temperature"),
                ["cpupackagepower(w)"] = ("CPU Package Power(W)", "CPU Package", "Power"),
            };

        [TestMethod]
        public void Analysis_ShowsTheRx9070XtAndNotTheIntegratedGpu()
        {
            var sensorData = new[] { Load().SensorData2 };
            var report = SensorReport.GetReportFromSessionSensorData(sensorData)
                .ToDictionary(item => item.Name, item => item.AverageValue);

            // The RX 9070 XT runs at full load throughout; the integrated GPU, logged first, idles at
            // 600 MHz without load.
            Assert.AreEqual(100, report["GPU load (%)"]);
            Assert.AreEqual(100, report["Time in GPU load limit (%)"]);
            Assert.AreEqual(3077, report["GPU clock (MHz)"]);
            Assert.AreEqual(51, report["GPU temp. (°C)"]);

            // The sensor tab lists the integrated GPU under names of its own and takes the GPU limit
            // time from the RX 9070 XT.
            var items = SensorReport.GetFullReportFromSessionSensorData(sensorData)
                .ToDictionary(item => item.Name, item => item.AverageValue);
            Assert.AreEqual(100, items["GPU Limit Time (%)"]);
            Assert.AreEqual(0, items["GPU0 Core (%)"]);
            Assert.AreEqual(600, items["GPU0 Core (MHz)"]);
        }
    }
}

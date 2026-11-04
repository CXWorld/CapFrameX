using CapFrameX.Sensor.Reporting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Test.Integration
{
    /// <summary>
    /// <see cref="FrameViewSensorImportTest"/> with S.T.A.L.K.E.R. 2 on an RTX 5090 and a Core Ultra 9
    /// 285K: 20 s, 3652 frames with frame generation. FrameView polls the GPU about every 0.5 s and the
    /// CPU power about every 0.2 s.
    /// </summary>
    [TestClass]
    public class FrameViewSensorImportNvidiaTest : FrameViewSensorImportTest
    {
        protected override string LogFile => @"TestRecordFiles\FrameView_Stalker2-Win64-Shipping.exe_2026_11_01T112546_Log.csv";
        protected override string ProcessName => "Stalker2-Win64-Shipping.exe";
        protected override string Gpu => "NVIDIA GeForce RTX 5090";
        protected override string Processor => "Intel(R) Core(TM) Ultra 9 285K";
        protected override int FrameCount => 3652;
        protected override int CellCount => 80;
        protected override int PrimaryGpu => 0;
        protected override string GpuPowerColumn => "NV Pwr(W) (API)";

        // GPU1, PCAT, AMDPwr and the battery columns are NA throughout; CPU TDP, the per-core loads and
        // the Perf/W columns are not imported.
        protected override IReadOnlyDictionary<string, (string Column, string Name, string Type)> LoggedSensors { get; } =
            new Dictionary<string, (string Column, string Name, string Type)>
            {
                ["gpu0clk(mhz)"] = ("GPU0Clk(MHz)", "GPU Core", "Clock"),
                ["gpu0memclk(mhz)"] = ("GPU0MemClk(MHz)", "GPU Memory", "Clock"),
                ["gpu0util(%)"] = ("GPU0Util(%)", "GPU Core", "Load"),
                ["gpu0temp(c)"] = ("GPU0Temp(C)", "GPU Core", "Temperature"),
                ["gpuonlypwr(w)(api)"] = ("GPUOnlyPwr(W) (API)", "GPU Chip Power", "Power"),
                ["nv-total-usbcpwr(w)(api)"] = ("NV-Total-USBCPwr(W) (API)", "GPU Board Power (excl. USB-C)", "Power"),
                ["nvpwr(w)(api)"] = ("NV Pwr(W) (API)", "GPU Power", "Power"),
                ["cpuclk(mhz)"] = ("CPUClk(MHz)", "CPU Average", "Clock"),
                ["cpuutil(%)"] = ("CPUUtil(%)", "CPU Total", "Load"),
                ["cpupackagetemp(c)"] = ("CPU Package Temp(C)", "CPU Package", "Temperature"),
                ["cpupackagepower(w)"] = ("CPU Package Power(W)", "CPU Package", "Power"),
            };

        [TestMethod]
        public void Analysis_ShowsTheBoardPower()
        {
            var report = SensorReport.GetReportFromSessionSensorData(new[] { Load().SensorData2 })
                .ToDictionary(item => item.Name, item => item.AverageValue);

            // The board power, not the chip power (183 W) or the USB-C exclusive value.
            Assert.AreEqual(296, report["GPU power (W)"]);
            // The GPU load never reaches 97 %.
            Assert.AreEqual(0, report["Time in GPU load limit (%)"]);
        }
    }
}

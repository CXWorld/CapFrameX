using System.Collections.Generic;
using PmcReader.Interop;

namespace PmcReader.AMD
{
    public class JaguarNorthbridge : Amd16hCpu
    {
        public JaguarNorthbridge()
        {
            List<MonitoringConfig> configs = new List<MonitoringConfig>();
            configs.Add(new DRAMConfig(this));
            configs.Add(new XBARConfig(this));
            monitoringConfigs = configs.ToArray();
            architectureName = "Jaguar Northbridge";
        }

        public class XBARConfig : MonitoringConfig
        {
            private JaguarNorthbridge cpu;
            public string GetConfigName() { return "DCT/XBAR"; }

            public XBARConfig(JaguarNorthbridge amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                ThreadAffinity.Set(1);
                cpu.ProgramNbPerfCounters(
                    GetNBPerfCtlValue(0xEC, 0b111111, true, 0x3), // All DRAM accesses
                    GetNBPerfCtlValue(0xE9, 0xA8, true, 0),  // CPU to mem
                    GetNBPerfCtlValue(0xE9, 0xA4, true, 0),  // CPU to IO
                    GetNBPerfCtlValue(0xE9, 0xA2, true, 0));
            }

            public MonitoringUpdateResults Update()
            {
                const int blockSize = 64; // appears to be 64, though the manual says it can be 32 for some single channel configs
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                ThreadAffinity.Set(1);
                cpu.UpdateNBCounterData();
                float dctAccess = cpu.NormalizedTotalCounts.ctr0;
                float cpuToMem = cpu.NormalizedTotalCounts.ctr1;
                float cpuToIo = cpu.NormalizedTotalCounts.ctr2;
                float ioToMem = cpu.NormalizedTotalCounts.ctr3;
                results.unitMetrics = new string[4][];
                results.unitMetrics[0] = new string[]
                {
                    "DCT",
                    FormatLargeNumber(dctAccess * blockSize) + "B/s",
                    FormatLargeNumber(cpu.NormalizedTotalCounts.ctr0total) + "B"
                };
                results.unitMetrics[1] = new string[]
                {
                    "CPU to Mem",
                    FormatLargeNumber(cpuToMem * blockSize) + "B/s",
                    FormatLargeNumber(cpu.NormalizedTotalCounts.ctr1total) + "B"
                };
                results.unitMetrics[2] = new string[]
                {
                    "CPU to IO",
                    FormatLargeNumber(cpuToIo * blockSize) + "B/s",
                    FormatLargeNumber(cpu.NormalizedTotalCounts.ctr2total) + "B"
                };
                results.unitMetrics[3] = new string[]
                {
                    "IO to Mem",
                    FormatLargeNumber(ioToMem * blockSize) + "B/s",
                    FormatLargeNumber(cpu.NormalizedTotalCounts.ctr3total) + "B"
                };


                results.overallMetrics = new string[]
                {
                    "(XBAR)",
                    FormatLargeNumber((cpuToMem + cpuToIo + ioToMem) * blockSize) + "B/s",
                    FormatLargeNumber((cpu.NormalizedTotalCounts.ctr1total + cpu.NormalizedTotalCounts.ctr2total + cpu.NormalizedTotalCounts.ctr3total) * blockSize) + "B"
                };
                results.overallCounterValues = cpu.GetOverallCounterValues("DCT", "CPU to Mem", "CPU to IO", "IO to Mem");
                return results;
            }

            public string[] columns = new string[] { "Item", "BW", "Total Data" };

            public string GetHelpText()
            {
                return "XBAR and DCT";
            }
        }

        public class DRAMConfig : MonitoringConfig
        {
            private JaguarNorthbridge cpu;
            public string GetConfigName() { return "DRAM Controllers"; }

            public DRAMConfig(JaguarNorthbridge amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                ThreadAffinity.Set(1);
                cpu.ProgramNbPerfCounters(
                    GetNBPerfCtlValue(0xEC, 0b111, true, 3), // DCT0
                    GetNBPerfCtlValue(0xEC, 0b111000, true, 3),  // DCT1
                    0,0);
            }

            public MonitoringUpdateResults Update()
            {
                const int blockSize = 64; // appears to be 64, though the manual says it can be 32 for some single channel configs
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                ThreadAffinity.Set(1);
                cpu.UpdateNBCounterData();
                float dct0Access = cpu.NormalizedTotalCounts.ctr0;
                float dct1Access = cpu.NormalizedTotalCounts.ctr1;
                results.unitMetrics = new string[2][];
                results.unitMetrics[0] = new string[]
                {
                    "DCT0",
                    FormatLargeNumber(dct0Access * blockSize) + "B/s",
                    FormatLargeNumber(cpu.NormalizedTotalCounts.ctr0total) + "B"
                };
                results.unitMetrics[1] = new string[]
                {
                    "DCT1",
                    FormatLargeNumber(dct1Access * blockSize) + "B/s",
                    FormatLargeNumber(cpu.NormalizedTotalCounts.ctr1total) + "B"
                };


                results.overallMetrics = new string[]
                {
                    "Total",
                    FormatLargeNumber((dct0Access + dct1Access) * blockSize) + "B/s",
                    FormatLargeNumber((cpu.NormalizedTotalCounts.ctr0total + cpu.NormalizedTotalCounts.ctr1total) * blockSize) + "B"
                };
                results.overallCounterValues = cpu.GetOverallCounterValues("DCT0", "DCT1", "Unused", "Unused");
                return results;
            }

            public string[] columns = new string[] { "Item", "BW", "Total Data" };

            public string GetHelpText()
            {
                return "DRAM Controllers";
            }
        }
    }
}

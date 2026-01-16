using System.Collections.Generic;
using PmcReader.Interop;

namespace PmcReader.AMD
{
    public class JaguarL2 : Amd16hCpu
    {
        public JaguarL2()
        {
            List<MonitoringConfig> configs = new List<MonitoringConfig>();
            configs.Add(new L2Config(this));
            monitoringConfigs = configs.ToArray();
            architectureName = "Jaguar L2";
        }

        public class L2Config : MonitoringConfig
        {
            private JaguarL2 cpu;
            public string GetConfigName() { return "L2 Traffic"; }

            public L2Config(JaguarL2 amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                ThreadAffinity.Set(0);
                cpu.ProgramL2IPerfCounters(
                    GetL2iPerfCtlValue(0x7E, 1, false, 0, 0, 0, 0), // l2 miss, code
                    GetL2iPerfCtlValue(0x7D, 1, false, 0, 0, 0, 0), // l2 request, instr
                    GetL2iPerfCtlValue(0x7E, 2, false, 0, 0, 0, 0), // l2 miss, data
                    GetL2iPerfCtlValue(0x7D, 2, false, 0, 0, 0, 0)); // l2 request, data
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                ThreadAffinity.Set(0);
                cpu.UpdateL2ICounterData();
                float l2DataReq = cpu.NormalizedTotalCounts.ctr3;
                float l2InstrReq = cpu.NormalizedTotalCounts.ctr1;
                float l2DataMiss = cpu.NormalizedTotalCounts.ctr2;
                float l2InstrMiss = cpu.NormalizedTotalCounts.ctr0;
                float l2DataHit = l2DataReq - l2DataMiss;
                float l2InstrHit = l2InstrReq - l2InstrMiss;
                float totalHit = l2DataReq + l2InstrReq;
                float totalMiss = l2DataMiss + l2InstrMiss;

                ulong totalL2DataHit = cpu.NormalizedTotalCounts.ctr0total - cpu.NormalizedTotalCounts.ctr2total;
                ulong totalL2InstrHit = cpu.NormalizedTotalCounts.ctr1total - cpu.NormalizedTotalCounts.ctr3total;

                List<string[]> unitMetricsList = new List<string[]>();
                unitMetricsList.Add(new string[] { "Data", FormatPercentage(l2DataHit, l2DataReq), FormatLargeNumber(l2DataHit * 64) + "B/s", FormatLargeNumber(totalL2DataHit * 64) + "B" });
                unitMetricsList.Add(new string[] { "Instruction", FormatPercentage(l2InstrHit, l2InstrReq), FormatLargeNumber(l2InstrHit * 64) + "B/s", FormatLargeNumber(totalL2InstrHit * 64) + "B" });
                results.unitMetrics = unitMetricsList.ToArray();

                results.overallMetrics = new string[]
                {
                    "Total", FormatPercentage(l2DataHit + l2InstrHit, l2DataReq + l2InstrReq), FormatLargeNumber(64*(l2DataHit + l2InstrHit)) + "B/s", FormatLargeNumber(64*(totalL2DataHit + totalL2InstrHit)) + "B"
                };
                results.overallCounterValues = cpu.GetOverallCounterValues("L2 Data Req", "L2 Instr Req", "L2 Data Miss", "L2 Instr Miss");
                return results;
            }

            public string[] columns = new string[] { "Item", "Hitrate", "BW", "Total"};

            public string GetHelpText()
            {
                return "L2 traffic";
            }
        }
    }
}

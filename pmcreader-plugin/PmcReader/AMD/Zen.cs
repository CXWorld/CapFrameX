using System;
using System.Runtime.InteropServices.WindowsRuntime;
using PmcReader.Interop;

namespace PmcReader.AMD
{
    public class Zen : Amd17hCpu
    {
        public Zen()
        {
            monitoringConfigs = new MonitoringConfig[1];
            monitoringConfigs[0] = new BpuMonitoringConfig(this);
            architectureName = "Zen 1";
        }

        public class BpuMonitoringConfig : MonitoringConfig
        {
            private Zen cpu;
            public string GetConfigName() { return "Branch Prediction and Fusion"; }

            public BpuMonitoringConfig(Zen amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.EnablePerformanceCounters();
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    ThreadAffinity.Set(1UL << threadIdx);
                    // Set PERF_CTR0 to count retired branches
                    Ring0.WriteMsr(MSR_PERF_CTL_0, GetPerfCtlValue(0xC2, 0, true, true, false, false, true, false, 0, 0, false, false));

                    // PERF_CTR1 = mispredicted retired branches
                    Ring0.WriteMsr(MSR_PERF_CTL_1, GetPerfCtlValue(0xC3, 0, true, true, false, false, true, false, 0, 0, false, false));

                    // PERF_CTR2 = retired instrs
                    Ring0.WriteMsr(MSR_PERF_CTL_2, GetPerfCtlValue(0xC0, 0, true, true, false, false, true, false, 0, 0, false, false));

                    // PERF_CTR3 = cycles not in halt
                    Ring0.WriteMsr(MSR_PERF_CTL_3, GetPerfCtlValue(0x76, 0, true, true, false, false, true, false, 0, 0, false, false));

                    // PERF_CTR4 = decoder overrides existing prediction
                    Ring0.WriteMsr(MSR_PERF_CTL_4, GetPerfCtlValue(0x91, 0, true, true, false, false, true, false, 0, 0, false, false));

                    // PERF_CTR5 = retired fused branch instructions
                    Ring0.WriteMsr(MSR_PERF_CTL_5, GetPerfCtlValue(0xD0, 0, true, true, false, false, true, false, 0, 1, false, false));
                }
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[cpu.GetThreadCount()][];
                cpu.InitializeCoreTotals();
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[threadIdx] = computeMetrics("Thread " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                }

                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues("Retired Branches", "Retired Misp Branches", "L1 BTB Override", "L2 BTB Override", "Decoder Override", "Fused Branches");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "BPU Accuracy", "Branch MPKI", "Branches", "Mispredicted Branches", "Fused Branches" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {

                return new string[] { label,
                        FormatLargeNumber(counterData.ctr3),
                        FormatLargeNumber(counterData.ctr2),
                        string.Format("{0:F2}", counterData.ctr2 / counterData.ctr3),
                        string.Format("{0:F2}%", 100 * (1 - counterData.ctr1 / counterData.ctr0)),
                        string.Format("{0:F2}", counterData.ctr1 / counterData.ctr3 * 1000),
                        FormatLargeNumber(counterData.ctr0),
                        FormatLargeNumber(counterData.ctr1),
                        FormatLargeNumber(counterData.ctr5)};
            }
        }
    }
}

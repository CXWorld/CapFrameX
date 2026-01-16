using System.Collections.Generic;
using PmcReader.Interop;

namespace PmcReader.AMD
{
    public class Jaguar : Amd16hCpu
    {
        public Jaguar()
        {
            List<MonitoringConfig> configs = new List<MonitoringConfig>();
            configs.Add(new BpuMonitoringConfig(this));
            configs.Add(new FPPipes(this));
            configs.Add(new IFetch(this));
            configs.Add(new DCache(this));
            configs.Add(new DCacheMissLatency(this));
            monitoringConfigs = configs.ToArray();
            architectureName = "Jaguar";
        }

        public class BpuMonitoringConfig : MonitoringConfig
        {
            private Jaguar cpu;
            public string GetConfigName() { return "Branch Prediction"; }

            public BpuMonitoringConfig(Jaguar amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramCorePerfCounters(
                    GetPerfCtlValue(0xC0, 0, false, 0, 0), // ret instr
                    GetPerfCtlValue(0x76, 0, false, 0, 0), // cycles
                    GetPerfCtlValue(0xC2, 0, false, 0, 0), // ret branch
                    GetPerfCtlValue(0xC3, 0, false, 0, 0)); // ret misp branch
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
                results.overallCounterValues = cpu.GetOverallCounterValues("Instructions", "Cycles", "Retired Branches", "Retired Mispredicted Branches");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "BPU Acc", "Branch MPKI", "% Branches", "Total Instr" };

            public string GetHelpText()
            {
                return "aaaaaa";
            }

            private string[] computeMetrics(string label, NormalizedCounterData counterData)
            {
                float instr = counterData.ctr0;
                float cycles = counterData.ctr1;
                return new string[] { label,
                        FormatLargeNumber(cycles),
                        FormatLargeNumber(instr),
                        string.Format("{0:F2}", instr / cycles),
                        FormatPercentage(counterData.ctr2 - counterData.ctr3, counterData.ctr2),
                        string.Format("{0:F2}", 1000 * counterData.ctr3 / instr),
                        FormatPercentage(counterData.ctr3, instr),
                        FormatLargeNumber(counterData.ctr0total)
                };
            }
        }

        public class FPPipes : MonitoringConfig
        {
            private Jaguar cpu;
            public string GetConfigName() { return "FP Pipes"; }

            public FPPipes(Jaguar amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramCorePerfCounters(
                    GetPerfCtlValue(0xC0, 0, false, 0, 0), // ret instr
                    GetPerfCtlValue(0x76, 0, false, 0, 0), // cycles
                    GetPerfCtlValue(0, 1, false, 0, 0), // FP0
                    GetPerfCtlValue(0, 2, false, 0, 0)); // FP1
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
                results.overallCounterValues = cpu.GetOverallCounterValues("Instructions", "Cycles", "FP0", "FP1");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "FP0", "FP1", "Total Instr", "Total FP0", "Total FP1" };

            public string GetHelpText()
            {
                return "aaaaaa";
            }

            private string[] computeMetrics(string label, NormalizedCounterData counterData)
            {
                float instr = counterData.ctr0;
                float cycles = counterData.ctr1;
                return new string[] { label,
                        FormatLargeNumber(cycles),
                        FormatLargeNumber(instr),
                        string.Format("{0:F2}", instr / cycles),
                        FormatPercentage(counterData.ctr2, cycles),
                        FormatPercentage(counterData.ctr3, cycles),
                        FormatLargeNumber(counterData.ctr0total),
                        FormatLargeNumber(counterData.ctr2total),
                        FormatLargeNumber(counterData.ctr3total)
                };
            }
        }

        public class IFetch : MonitoringConfig
        {
            private Jaguar cpu;
            public string GetConfigName() { return "Instruction Fetch"; }

            public IFetch(Jaguar amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramCorePerfCounters(
                    GetPerfCtlValue(0xC0, 0, false, 0, 0), // instr
                    GetPerfCtlValue(0x80, 0, false, 0, 0), // ic access (32B)
                    GetPerfCtlValue(0x82, 0, false, 0, 0), // ic fill from L2
                    GetPerfCtlValue(0x83, 0, false, 0, 0)); // ic fill from sys
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
                results.overallCounterValues = cpu.GetOverallCounterValues("Instructions", "IC Access", "IC fill from L2", "IC fill from sys");
                return results;
            }

            public string[] columns = new string[] { "Item", "Instructions", "L1i Hitrate", "L1i Hit BW", "L1i MPKI", "L2 Code Hitrate", "L2 Code MPKI" };

            public string GetHelpText()
            {
                return "aaaaaa";
            }

            private string[] computeMetrics(string label, NormalizedCounterData counterData)
            {
                float instr = counterData.ctr0;
                return new string[] { label,
                        FormatLargeNumber(instr),
                        FormatPercentage(counterData.ctr1 - counterData.ctr2 - counterData.ctr3, counterData.ctr1),
                        FormatLargeNumber(counterData.ctr1 * 32) + "B/s",
                        string.Format("{0:F2}", 1000 * (counterData.ctr2 + counterData.ctr3) / instr),
                        FormatPercentage(counterData.ctr2, counterData.ctr2 + counterData.ctr3),
                        string.Format("{0:F2}", 1000 * counterData.ctr3 / instr)
                };
            }
        }

        public class DCache : MonitoringConfig
        {
            private Jaguar cpu;
            public string GetConfigName() { return "Data Cache"; }

            public DCache(Jaguar amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramCorePerfCounters(
                    GetPerfCtlValue(0xC0, 0, false, 0, 0), // instr
                    GetPerfCtlValue(0x40, 0, false, 0, 0), // dc access (8B)
                    GetPerfCtlValue(0x42, 0x1F, false, 0, 0), // dc fill from L2 or NB
                    GetPerfCtlValue(0x43, 0x1F, false, 0, 0)); // dc fill from nb
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
                results.overallCounterValues = cpu.GetOverallCounterValues("Instructions", "DC Access", "DC fill from L2 or NB", "DC fill from NB");
                return results;
            }

            public string[] columns = new string[] { "Item", "Instructions", "L1D Hitrate", "L1D Hit BW", "L1D MPKI", "L2 Data Hitrate", "L2 Data MPKI"};

            public string GetHelpText()
            {
                return "aaaaaa";
            }

            private string[] computeMetrics(string label, NormalizedCounterData counterData)
            {
                float instr = counterData.ctr0;
                return new string[] { label,
                        FormatLargeNumber(instr),
                        FormatPercentage(counterData.ctr1 - counterData.ctr2, counterData.ctr1),
                        FormatLargeNumber((counterData.ctr1 - counterData.ctr2) * 8) + "B/s",
                        string.Format("{0:F2}", 1000 * counterData.ctr2 / instr),
                        FormatPercentage(counterData.ctr2 - counterData.ctr3, counterData.ctr2),
                        string.Format("{0:F2}", 1000 * counterData.ctr3 / instr)
                };
            }
        }

        public class DCacheMissLatency : MonitoringConfig
        {
            private Jaguar cpu;
            public string GetConfigName() { return "DC Miss Latency"; }

            public DCacheMissLatency(Jaguar amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramCorePerfCounters(
                    GetPerfCtlValue(0xC0, 0, false, 0, 0), // instructions
                    GetPerfCtlValue(0x76, 0, false, 0, 0), // cycles
                    GetPerfCtlValue(0x68, 0xB, false, 0, 0), // MAB alloc
                    GetPerfCtlValue(0x69, 0xB, false, 0, 0)); // MAB miss
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
                results.overallCounterValues = cpu.GetOverallCounterValues("Instructions", "Cycles", "MAB Alloc", "MAB Occupancy");
                return results;
            }

            public string[] columns = new string[] { "Item", "Cycles", "Instructions", "IPC", "L1D Miss Latency", "Data MAB Occupancy", "MAB Req BW" };

            public string GetHelpText()
            {
                return "aaaaaa";
            }

            private string[] computeMetrics(string label, NormalizedCounterData counterData)
            {
                float instr = counterData.ctr0;
                float cycles = counterData.ctr1;
                float mabReqs = counterData.ctr2;
                float mabOccupancy = counterData.ctr3;
                return new string[] { label,
                        FormatLargeNumber(cycles),
                        FormatLargeNumber(instr),
                        string.Format("{0:F2}", instr / cycles),
                        string.Format("{0:F2} clk", mabOccupancy / mabReqs),
                        string.Format("{0:F2}", mabOccupancy / cycles),
                        FormatLargeNumber(mabReqs * 64) + "B/s"
                };
            }
        }
    }
}

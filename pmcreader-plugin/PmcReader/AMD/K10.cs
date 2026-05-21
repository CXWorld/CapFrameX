using System.Collections.Generic;
using PmcReader.Interop;

namespace PmcReader.AMD
{
    public class K10 : Amd10hCpu
    {
        public K10()
        {
            List<MonitoringConfig> configs = new List<MonitoringConfig>();
            configs.Add(new BpuMonitoringConfig(this));
            configs.Add(new L1iConfig(this));
            configs.Add(new DispatchStalls1(this));
            configs.Add(new DispatchStalls2(this));
            configs.Add(new DispatchStalls3(this));
            configs.Add(new L1DConfig(this));
            configs.Add(new L1DBW(this));
            configs.Add(new FPUConfig(this));
            configs.Add(new SSEFlops(this));
            configs.Add(new L2Config(this));
            configs.Add(new L3Config(this));
            configs.Add(new DRAMConfig(this));
            configs.Add(new HTConfig(this));
            monitoringConfigs = configs.ToArray();
            architectureName = "K10";
        }

        public class BpuMonitoringConfig : MonitoringConfig
        {
            private K10 cpu;
            public string GetConfigName() { return "Branch Prediction"; }

            public BpuMonitoringConfig(K10 amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0x76, 0, false, 0, 0), // unhalted clocks
                    GetPerfCtlValue(0xC0, 0, false, 0, 0), // ret instr
                    GetPerfCtlValue(0xC2, 0, false, 0, 0), // branches
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
                results.overallCounterValues = cpu.GetOverallCounterValues("cycles", "instructions", "retired branches", "retired mispredicted branches");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "BPU Acc", "Branch MPKI", "% Branches" };

            public string GetHelpText()
            {
                return "aaaaaa";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float instr = counterData.ctr1;
                float cycles = counterData.ctr0;
                return new string[] { label,
                        FormatLargeNumber(cycles),
                        FormatLargeNumber(instr),
                        string.Format("{0:F2}", instr / cycles),
                        string.Format("{0:F2}%", 100 * (1 - counterData.ctr3 / counterData.ctr2)), // BPU Acc
                        string.Format("{0:F2}", counterData.ctr3 / instr * 1000),     // BPU MPKI
                        FormatPercentage(counterData.ctr2, instr)};
            }
        }

        public class L1iConfig : MonitoringConfig
        {
            private K10 cpu;
            public string GetConfigName() { return "L1i Cache"; }

            public L1iConfig(K10 amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0x76, 0, false, 0, 0), // unhalted clocks
                    GetPerfCtlValue(0xC0, 0, false, 0, 0), // ret instr
                    GetPerfCtlValue(0x80, 0, false, 0, 0), // ic access
                    GetPerfCtlValue(0x81, 0, false, 0, 0)); // ic miss
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
                results.overallCounterValues = cpu.GetOverallCounterValues("cycles", "instructions", "IC Access", "IC Miss");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "L1i Hitrate", "L1i MPKI", "L1i Hit BW" };

            public string GetHelpText()
            {
                return "aaaaaa";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float instr = counterData.ctr1;
                float cycles = counterData.ctr0;
                return new string[] { label,
                        FormatLargeNumber(cycles),
                        FormatLargeNumber(instr),
                        string.Format("{0:F2}", instr / cycles),
                        string.Format("{0:F2}%", 100 * (1 - counterData.ctr3 / counterData.ctr2)),
                        string.Format("{0:F2}", counterData.ctr3 / instr * 1000),
                        FormatLargeNumber(16 * (counterData.ctr2 - counterData.ctr3)) + "B/s"};
            }
        }

        public class L1DConfig : MonitoringConfig
        {
            private K10 cpu;
            public string GetConfigName() { return "L1D Cache"; }

            public L1DConfig(K10 amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0x76, 0, false, 0, 0), // unhalted clocks
                    GetPerfCtlValue(0xC0, 0, false, 0, 0), // ret instr
                    GetPerfCtlValue(0x40, 0, false, 0, 0), 
                    GetPerfCtlValue(0x41, 0, false, 0, 0)); 
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
                results.overallCounterValues = cpu.GetOverallCounterValues("cycles", "instructions", "DC Access", "DC Miss");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "L1D Hitrate", "L1D MPKI", "L1D Hit BW" };

            public string GetHelpText()
            {
                return "aaaaaa";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float instr = counterData.ctr1;
                float cycles = counterData.ctr0;
                return new string[] { label,
                        FormatLargeNumber(cycles),
                        FormatLargeNumber(instr),
                        string.Format("{0:F2}", instr / cycles),
                        string.Format("{0:F2}%", 100 * (1 - counterData.ctr3 / counterData.ctr2)),
                        string.Format("{0:F2}", counterData.ctr3 / instr * 1000),
                        FormatLargeNumber(16 * (counterData.ctr2 - counterData.ctr3)) + "B/s"};
            }
        }

        public class L1DBW : MonitoringConfig
        {
            private K10 cpu;
            public string GetConfigName() { return "L1D BW"; }

            public L1DBW(K10 amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0x76, 0, false, 0, 0), // unhalted clocks
                    GetPerfCtlValue(0xC0, 0, false, 0, 0), // ret instr
                    GetPerfCtlValue(0x40, 0, false, 1, 0),
                    GetPerfCtlValue(0x40, 0, false, 2, 0));
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
                results.overallCounterValues = cpu.GetOverallCounterValues("cycles", "instructions", "DC Access cmask 1", "DC Acces cmask 2");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "L1D Access BW", "L1D Active", "L1D 2 Accesses" };

            public string GetHelpText()
            {
                return "aaaaaa";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float instr = counterData.ctr1;
                float cycles = counterData.ctr0;
                return new string[] { label,
                        FormatLargeNumber(cycles),
                        FormatLargeNumber(instr),
                        string.Format("{0:F2}", instr / cycles),
                        FormatLargeNumber(8 * ((counterData.ctr2 - counterData.ctr3) + 2 * counterData.ctr3)) + "B/s",
                        FormatPercentage(counterData.ctr2, cycles),
                        FormatPercentage(counterData.ctr3, cycles)};
            }
        }

        public class L2Config : MonitoringConfig
        {
            private K10 cpu;
            public string GetConfigName() { return "L2 Cache"; }

            public L2Config(K10 amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0x76, 0, false, 0, 0), // unhalted clocks
                    GetPerfCtlValue(0xC0, 0, false, 0, 0), // ret instr
                    GetPerfCtlValue(0x7D, 0x2F, false, 0, 0), // l2 access, not cancelled
                    GetPerfCtlValue(0x7E, 0xF, false, 0, 0)); // l2 miss
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
                results.overallCounterValues = cpu.GetOverallCounterValues("cycles", "instructions", "L2 Access", "L2 Miss");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "L2 Hitrate", "L2 MPKI", "L2 Hit BW", "Total Instructions", "Total L2 Hit Data" };

            public string GetHelpText()
            {
                return "aaaaaa";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float instr = counterData.ctr1;
                float cycles = counterData.ctr0;
                return new string[] { label,
                        FormatLargeNumber(cycles),
                        FormatLargeNumber(instr),
                        string.Format("{0:F2}", instr / cycles),
                        string.Format("{0:F2}%", 100 * (1 - counterData.ctr3 / counterData.ctr2)),
                        string.Format("{0:F2}", counterData.ctr3 / instr * 1000),
                        FormatLargeNumber(64 * (counterData.ctr2 - counterData.ctr3)) + "B/s",
                        FormatLargeNumber(counterData.totalctr1),
                        FormatLargeNumber(64 * (counterData.totalctr2 - counterData.totalctr3)) + "B"
                };
            }
        }

        public class FPUConfig : MonitoringConfig
        {
            private K10 cpu;
            public string GetConfigName() { return "FP Pipes"; }

            public FPUConfig(K10 amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0x76, 0, false, 0, 0), // unhalted clocks
                    GetPerfCtlValue(0, 1 | 0x8, false, 0, 0), // add
                    GetPerfCtlValue(0, 2 | 0x10, false, 0, 0), // mul
                    GetPerfCtlValue(0, 4 | 0x20, false, 0, 0)); // store
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
                results.overallCounterValues = cpu.GetOverallCounterValues("cycles", "fadd", "fmul", "fstore");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "FAdd", "FMul", "FStore" };

            public string GetHelpText()
            {
                return "aaaaaa";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float cycles = counterData.ctr0;
                return new string[] { label,
                        FormatLargeNumber(cycles),
                        FormatPercentage(counterData.ctr1, cycles),
                        FormatPercentage(counterData.ctr2, cycles),
                        FormatPercentage(counterData.ctr3, cycles)};
            }
        }

        public class SSEFlops : MonitoringConfig
        {
            private K10 cpu;
            public string GetConfigName() { return "SSE FLOPS"; }

            public SSEFlops(K10 amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0x76, 0, false, 0, 0), // unhalted clocks
                    GetPerfCtlValue(0xC0, 0, false, 0, 0), // instr
                    GetPerfCtlValue(0x3, 0b111 | 0x40, false, 0, 0), // FP32 flops
                    GetPerfCtlValue(0x3, 0b111000 | 0x40, false, 0, 0)); // FP64 flops
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
                results.overallCounterValues = cpu.GetOverallCounterValues("cycles", "instructions", "FP32 FLOPS", "FP64 FLOPS");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "FP32 FLOPS", "FP64 FLOPS" };

            public string GetHelpText()
            {
                return "aaaaaa";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float cycles = counterData.ctr0;
                float instr = counterData.ctr1;
                return new string[] { label,
                        FormatLargeNumber(cycles),
                        FormatLargeNumber(instr),
                        string.Format("{0:F2}", instr / cycles),
                        FormatLargeNumber(counterData.ctr2),
                        FormatLargeNumber(counterData.ctr3)};
            }
        }

        public class DispatchStalls1 : MonitoringConfig
        {
            private K10 cpu;
            public string GetConfigName() { return "Dispatch Stalls"; }

            public DispatchStalls1(K10 amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0x76, 0, false, 0, 0), // unhalted clocks
                    GetPerfCtlValue(0xD1, 0, false, 0, 0), // dispatch stalls
                    GetPerfCtlValue(0xD5, 0, false, 0, 0), // rob full
                    GetPerfCtlValue(0xD6, 0, false, 0, 0)); // int rs full
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
                results.overallCounterValues = cpu.GetOverallCounterValues("cycles", "dispatch stalls", "rob full", "int rs full");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Dispatch Stalls", "ROB Full", "INT RS Full" };

            public string GetHelpText()
            {
                return "aaaaaa";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float cycles = counterData.ctr0;
                return new string[] { label,
                        FormatLargeNumber(cycles),
                        FormatPercentage(counterData.ctr1, cycles),
                        FormatPercentage(counterData.ctr2, cycles),
                        FormatPercentage(counterData.ctr3, cycles)};
            }
        }

        public class DispatchStalls2 : MonitoringConfig
        {
            private K10 cpu;
            public string GetConfigName() { return "Dispatch Stalls 2"; }

            public DispatchStalls2(K10 amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0x76, 0, false, 0, 0), // unhalted clocks
                    GetPerfCtlValue(0xD7, 0, false, 0, 0), // FPU Full
                    GetPerfCtlValue(0xD8, 0, false, 0, 0), // LS Full
                    GetPerfCtlValue(0xD2, 0, false, 0, 0)); // branch abort
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
                results.overallCounterValues = cpu.GetOverallCounterValues("cycles", "FPU Full", "LS Full", "Branch Abort");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "FPU Full", "LS Full", "Branch Abort" };

            public string GetHelpText()
            {
                return "aaaaaa";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float cycles = counterData.ctr0;
                return new string[] { label,
                        FormatLargeNumber(cycles),
                        FormatPercentage(counterData.ctr1, cycles),
                        FormatPercentage(counterData.ctr2, cycles),
                        FormatPercentage(counterData.ctr3, cycles)};
            }
        }

        public class DispatchStalls3 : MonitoringConfig
        {
            private K10 cpu;
            public string GetConfigName() { return "Dispatch Stalls (Rare)"; }

            public DispatchStalls3(K10 amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0x76, 0, false, 0, 0), // unhalted clocks
                    GetPerfCtlValue(0xD3, 0, false, 0, 0), // Dispatch Stall, serialization
                    GetPerfCtlValue(0xD9, 0, false, 0, 0), // ... wait for all quiet
                    GetPerfCtlValue(0xDA, 0, false, 0, 0)); // far transfer/resync
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
                results.overallCounterValues = cpu.GetOverallCounterValues("cycles", "Serializing Operation", "Wait for All Quiet", "Far Transfer/Resync");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Serializing Operation", "Wait for All Quiet", "Far Transfer/Resync" };

            public string GetHelpText()
            {
                return "aaaaaa";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float cycles = counterData.ctr0;
                return new string[] { label,
                        FormatLargeNumber(cycles),
                        FormatPercentage(counterData.ctr1, cycles),
                        FormatPercentage(counterData.ctr2, cycles),
                        FormatPercentage(counterData.ctr3, cycles)};
            }
        }

        public class HTConfig : MonitoringConfig
        {
            private K10 cpu;
            public string GetConfigName() { return "HT Link"; }

            public HTConfig(K10 amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0xF6, 2, false, 0, 0), 
                    GetPerfCtlValue(0xF7, 2, false, 0, 0), 
                    GetPerfCtlValue(0xF8, 2, false, 0, 0), 
                    GetPerfCtlValue(0xF9, 2, false, 0, 1)); 
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[4][];
                cpu.InitializeCoreTotals();
                cpu.UpdateThreadCoreCounterData(0);
                results.unitMetrics[0] = new string[] { "Link 0", FormatLargeNumber(4 * cpu.NormalizedThreadCounts[0].ctr0) + "B/s" };
                results.unitMetrics[1] = new string[] { "Link 1", FormatLargeNumber(4 * cpu.NormalizedThreadCounts[0].ctr1) + "B/s" };
                results.unitMetrics[2] = new string[] { "Link 2", FormatLargeNumber(4 * cpu.NormalizedThreadCounts[0].ctr2) + "B/s" };
                results.unitMetrics[3] = new string[] { "Link 3", FormatLargeNumber(4 * cpu.NormalizedThreadCounts[0].ctr3) + "B/s" };

                float totalLinkBw = 4 * (cpu.NormalizedThreadCounts[0].ctr0 + cpu.NormalizedThreadCounts[0].ctr1 + cpu.NormalizedThreadCounts[0].ctr2 + cpu.NormalizedThreadCounts[0].ctr3);
                results.overallMetrics = new string[] { "Total", FormatLargeNumber(totalLinkBw) + "B/s" };
                results.overallCounterValues = cpu.GetOverallCounterValues("Link0", "Link1", "Link2", "Link3");
                return results;
            }

            public string[] columns = new string[] { "Item", "Data BW" };

            public string GetHelpText()
            {
                return "aaaaaa";
            }
        }

        public class DRAMConfig : MonitoringConfig
        {
            private K10 cpu;
            public string GetConfigName() { return "DRAM Controller"; }

            public DRAMConfig(K10 amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0xE0, 1, false, 0, 0),
                    GetPerfCtlValue(0xE0, 0b110, false, 0, 0),
                    GetPerfCtlValue(0xE0, 8, false, 0, 0),
                    GetPerfCtlValue(0xE0, 0b110000, false, 0, 0));
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[2][];
                cpu.InitializeCoreTotals();
                cpu.UpdateThreadCoreCounterData(0);
                float dct0PageHit = cpu.NormalizedThreadCounts[0].ctr0;
                float dct0PageMiss = cpu.NormalizedThreadCounts[0].ctr1;
                float dct1PageHit = cpu.NormalizedThreadCounts[0].ctr2;
                float dct1PageMiss = cpu.NormalizedThreadCounts[0].ctr3;
                float dct0Bw = 64 * (dct0PageHit + dct0PageMiss);
                float dct1Bw = 64 * (dct1PageHit + dct1PageMiss);
                ulong dct0Accesses = 64*(cpu.NormalizedThreadCounts[0].totalctr0 + cpu.NormalizedThreadCounts[0].totalctr1);
                ulong dct1Accesses = 64*(cpu.NormalizedThreadCounts[0].totalctr2 + cpu.NormalizedThreadCounts[0].totalctr3);
                results.unitMetrics[0] = new string[] { 
                    "DCT0", FormatLargeNumber(dct0Bw) + "B/s", FormatPercentage(dct0PageHit, dct0PageHit + dct0PageMiss), FormatLargeNumber(dct0Accesses) };
                results.unitMetrics[1] = new string[] { 
                    "DCT1", FormatLargeNumber(dct1Bw) + "B/s", FormatPercentage(dct1PageHit, dct1PageHit + dct1PageMiss), FormatLargeNumber(dct1Accesses) };

                results.overallMetrics = new string[] { "Total", 
                    FormatLargeNumber(dct0Bw + dct1Bw) + "B/s", 
                    FormatPercentage(dct0PageHit + dct1PageHit, dct0PageHit + dct1PageHit + dct0PageMiss + dct1PageMiss),
                    FormatLargeNumber(dct0Accesses + dct1Accesses) + "B"
                };
                results.overallCounterValues = cpu.GetOverallCounterValues("DCT0 Page Hit", "DCT0 Page Miss", "DCT1 Page Hit", "DCT1 Page Miss");
                return results;
            }

            public string[] columns = new string[] { "Item", "Bandwidth", "Page Hit %", "Total Data" };

            public string GetHelpText()
            {
                return "aaaaaa";
            }
        }

        public class L3Config : MonitoringConfig
        {
            private K10 cpu;
            public string GetConfigName() { return "L3 Cache"; }

            public L3Config(K10 amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                ulong ctr0 = GetPerfCtlValue(0xE0, 0b111 | 0xF0, false, 0, 4); // L3 read
                ulong ctr1 = GetPerfCtlValue(0xE1, 0b111 | 0xF0, false, 0, 4); // L3 miss
                ulong ctr2 = GetPerfCtlValue(0xE2, 0xFF, false, 0, 0);            // L3 fill from L2
                ulong ctr3 = GetPerfCtlValue(0xC0, 0, false, 0, 0);    // instructions

                // use thread 0 to read cpu counters
                ThreadAffinity.Set(1UL);
                Ring0.WriteMsr(MSR_PERF_CTL_0, ctr0);
                Ring0.WriteMsr(MSR_PERF_CTL_1, ctr1);
                Ring0.WriteMsr(MSR_PERF_CTL_2, ctr2);
                Ring0.WriteMsr(MSR_PERF_CTL_3, ctr3);

                // monitor instructions on all other threads
                for (int threadIdx = 1; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    ThreadAffinity.Set(1UL << threadIdx);
                    Ring0.WriteMsr(MSR_PERF_CTL_0, 0);
                    Ring0.WriteMsr(MSR_PERF_CTL_1, 0);
                    Ring0.WriteMsr(MSR_PERF_CTL_2, 0);
                    Ring0.WriteMsr(MSR_PERF_CTL_3, ctr3);
                }
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[5][];
                cpu.InitializeCoreTotals();
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                }

                float l3read = cpu.NormalizedThreadCounts[0].ctr0;
                float l3miss = cpu.NormalizedThreadCounts[0].ctr1;
                float l3fill = cpu.NormalizedThreadCounts[0].ctr2;
                results.unitMetrics[0] = new string[] { "Hitrate", FormatPercentage(l3read - l3miss, l3read) };
                results.unitMetrics[1] = new string[] { "Hit BW", FormatLargeNumber(64 *(l3read - l3miss)) + "B/s" };
                results.unitMetrics[2] = new string[] { "Fill BW", FormatLargeNumber(64 * l3fill) + "B/s" };
                results.unitMetrics[3] = new string[] { "Instructions", FormatLargeNumber(cpu.NormalizedTotalCounts.ctr3) };
                results.unitMetrics[4] = new string[] { "MPKI", string.Format("{0:F2}", 1000 * l3miss / cpu.NormalizedTotalCounts.ctr3) };

                results.overallMetrics = new string[] { "Total BW", FormatLargeNumber(64 * ((l3read - l3miss) + l3fill)) + "B/s"};
                results.overallCounterValues = cpu.GetOverallCounterValues("L3 Read", "L3 Miss", "L3 Fill From L2", "L3 Modified Eviction");
                return results;
            }

            public string[] columns = new string[] { "Item", "Value" };

            public string GetHelpText()
            {
                return "aaaaaa";
            }
        }
        // end of monitoring configs
    }
}

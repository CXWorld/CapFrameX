using System;
using System.Runtime.InteropServices.WindowsRuntime;
using PmcReader.Interop;

namespace PmcReader.AMD
{
    public class Piledriver : Amd15hCpu
    {
        public Piledriver()
        {
            monitoringConfigs = new MonitoringConfig[12];
            monitoringConfigs[0] = new BpuMonitoringConfig(this);
            monitoringConfigs[1] = new IFetch(this);
            monitoringConfigs[2] = new DataCache(this);
            monitoringConfigs[3] = new DataCache1(this);
            monitoringConfigs[4] = new L2Cache(this);
            monitoringConfigs[5] = new DispatchStall(this);
            monitoringConfigs[6] = new DispatchStall1(this);
            monitoringConfigs[7] = new DispatchStallFP(this);
            monitoringConfigs[8] = new DispatchStallMisc(this);
            monitoringConfigs[9] = new DTLB(this);
            monitoringConfigs[10] = new FPU(this);
            monitoringConfigs[11] = new Offcore(this);
            architectureName = "Piledriver";
        }

        public class BpuMonitoringConfig : MonitoringConfig
        {
            private Piledriver cpu;
            public string GetConfigName() { return "Branch Prediction"; }

            public BpuMonitoringConfig(Piledriver amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0x88, 0, false, 0, 0), // return stack hits
                    GetPerfCtlValue(0x89, 0, false, 0, 0), // return stack overflows
                    GetPerfCtlValue(0xC0, 0, false, 0, 0), // ret instr
                    GetPerfCtlValue(0xC4, 0, false, 0, 0), // ret taken branches
                    GetPerfCtlValue(0xC2, 0, false, 0, 0), // ret branches
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
                results.overallCounterValues = cpu.GetOverallCounterValues("Return stack hits", "Return stack overflows", "instructions", "taken branches", "retired branches", "retired mispredicted branches");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "BPU Acc", "Branch MPKI", "% Branches", "% Branches Taken", "Return Stack Hits", "Return Stack Overflow"};

            public string GetHelpText()
            {
                return "aaaaaa";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float instr = counterData.ctr2;
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(instr),
                        string.Format("{0:F2}", instr / counterData.aperf),
                        string.Format("{0:F2}%", 100 * (1 - counterData.ctr5 / counterData.ctr4)), // BPU Acc
                        string.Format("{0:F2}", counterData.ctr5 / instr * 1000),     // BPU MPKI
                        FormatPercentage(counterData.ctr4, instr), // % branches
                        FormatPercentage(counterData.ctr3, counterData.ctr4), // % branches taken
                        FormatLargeNumber(counterData.ctr0),
                        FormatLargeNumber(counterData.ctr1)};   // Branch %
            }
        }

        public class IFetch : MonitoringConfig
        {
            private Piledriver cpu;
            public string GetConfigName() { return "L1i Cache"; }

            public IFetch(Piledriver amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0x80, 0, false, 0, 0), // ic fetch
                    GetPerfCtlValue(0x82, 0, false, 0, 0), // ic refill from L2
                    GetPerfCtlValue(0x83, 0, false, 0, 0), // ic refill from system
                    GetPerfCtlValue(0xC0, 0, false, 0, 0), // ret instr
                    GetPerfCtlValue(0xC1, 0, false, 0, 0), // ret uops
                    GetPerfCtlValue(0x21, 0, false, 0, 0));  // SMC pipeline restart
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
                results.overallCounterValues = cpu.GetOverallCounterValues("IC Access", "IC Miss", "Decoder Empty", "Instructions", "Uops", "Pipeline Restart for SMC");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", 
                "Uops/C", "Uops/Instr", "IC Hitrate", "IC Hit BW", "IC MPKI", "L2->IC Fill BW", "L2 Code Hitrate", "L2 Code MPKI", "Sys->IC Fill BW", "Self Modifying Code Pipeline Restarts", "Total Instr", "Total L1i Data" };

            public string GetHelpText()
            {
                return "aaaaaa";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float instr = counterData.ctr3;
                float icHits = counterData.ctr0 - (counterData.ctr1 + counterData.ctr2);
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(instr),
                        string.Format("{0:F2}", instr / counterData.aperf),                       // IPC
                        string.Format("{0:F2}", counterData.ctr4 / counterData.aperf),                    // Uops/c
                        string.Format("{0:F2}", counterData.ctr4 / instr),                                // Uops/instr
                        FormatPercentage(counterData.ctr0 - (counterData.ctr1 + counterData.ctr2), counterData.ctr0),  // IC hitrate
                        FormatLargeNumber(32 * icHits) + "B/s",                                   // IC Hit BW
                        string.Format("{0:F2}", 1000 * (counterData.ctr1 + counterData.ctr2) / instr), // IC MPKI
                        FormatLargeNumber(64 * counterData.ctr1) + "B/s", // IC refill from L2
                        FormatPercentage(counterData.ctr1, counterData.ctr1 + counterData.ctr2),       // L2 code hitrate
                        string.Format("{0:F2}", 1000 * counterData.ctr2 / instr), // L2 code MPKI
                        FormatLargeNumber(64 * counterData.ctr2) + "B/s", // IC refill from system
                        FormatLargeNumber(counterData.ctr5), 
                        FormatLargeNumber(counterData.ctr3total),
                        FormatLargeNumber((counterData.ctr0total - (counterData.ctr1total + counterData.ctr2total)) * 32) + "B"};   
            }
        }

        public class DataCache : MonitoringConfig
        {
            private Piledriver cpu;
            public string GetConfigName() { return "L1D Cache"; }

            public DataCache(Piledriver amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0x23, 1, false, 0, 0), // LDQ full
                    GetPerfCtlValue(0x23, 2, false, 0, 0), // STQ full
                    GetPerfCtlValue(0x43, 0, false, 0, 0), // DC fill from system
                    GetPerfCtlValue(0xC0, 0, false, 0, 0), // ret instr
                    GetPerfCtlValue(0x42, 0b1011, false, 0, 0), // DC fill from L2 or system
                    GetPerfCtlValue(0x40, 0, false, 0, 0));  // DC access
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
                results.overallCounterValues = cpu.GetOverallCounterValues("LDQ full", "STQ full", "DC fill from sys", "Instructions", "DC Fill from L2 or Sys", "DC access");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC",
                "L1D Hitrate", "L1D MPKI", "L1D/MAB Hit BW", "L2->L1D BW", "L2 Data Hitrate", "L2 Data MPKI", "Sys->L1D BW", "LDQ Full", "STQ Full" };

            public string GetHelpText()
            {
                return "aaaaaa";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float instr = counterData.ctr3;
                float dcHits = counterData.ctr5 - counterData.ctr4;
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(instr),
                        string.Format("{0:F2}", instr / counterData.aperf),
                        FormatPercentage(dcHits, counterData.ctr5),
                        string.Format("{0:F2}", 1000 * counterData.ctr4 / instr),
                        FormatLargeNumber(16 * dcHits) + "B/s", // each data cache hit should be 16B
                        FormatLargeNumber(64 * (counterData.ctr4 - counterData.ctr2)) + "B/s",
                        FormatPercentage(counterData.ctr4 - counterData.ctr2, counterData.ctr4),
                        string.Format("{0:F2}", 1000 * counterData.ctr2 / instr),
                        FormatLargeNumber(64 * counterData.ctr2) + "B/s",
                        FormatPercentage(counterData.ctr0, counterData.aperf),
                        FormatPercentage(counterData.ctr1, counterData.aperf)};
            }
        }

        public class DataCache1 : MonitoringConfig
        {
            private Piledriver cpu;
            public string GetConfigName() { return "L1D Activity"; }

            public DataCache1(Piledriver amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0x32, 0, false, 0, 0), // misaligned stores
                    GetPerfCtlValue(0x41, 0b11, false, 0, 0), // DC Miss
                    GetPerfCtlValue(0x47, 0, false, 0, 0), // misaligned access
                    GetPerfCtlValue(0xC0, 0, false, 0, 0), // ret instr
                    GetPerfCtlValue(0x40, 0, false, 1, 0), // DC access, cmask 1
                    GetPerfCtlValue(0x40, 0, false, 2, 0));  // DC access, cmask 2
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
                results.overallCounterValues = cpu.GetOverallCounterValues("Misaligned Store", "DC Miss", "Misaligned Access", "Instr", "DC Access Cmask 1", "DC Access Cmask 2");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC",
                "L1D Hitrate", "L1D MPKI", "L1D/MAB Hit BW", "DC Active", "DC 2 Accesses/c", "Misaligned Accesses", "Misaligned Stores" };

            public string GetHelpText()
            {
                return "aaaaaa";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float instr = counterData.ctr3;
                float dcAccess = 2 * counterData.ctr5 + (counterData.ctr4 - counterData.ctr5);
                float dcHits = dcAccess - counterData.ctr1;
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(instr),
                        string.Format("{0:F2}", instr / counterData.aperf),
                        FormatPercentage(dcHits, dcAccess),
                        string.Format("{0:F2}", 1000 * counterData.ctr1 / instr),
                        FormatLargeNumber(16 * dcHits) + "B/s", // each data cache hit should be 16B
                        FormatPercentage(counterData.ctr4, counterData.aperf),
                        FormatPercentage(counterData.ctr5, counterData.aperf),
                        string.Format("{0:F2}/Ki", 1000 * counterData.ctr2 / instr),
                        string.Format("{0:F2}/Ki", 1000 * counterData.ctr0 / instr)};
            }
        }

        public class L2Cache : MonitoringConfig
        {
            private Piledriver cpu;
            public string GetConfigName() { return "L2 Cache, more LS"; }

            public L2Cache(Piledriver amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0x7D, 0b01000111, false, 0, 0), // Request to L2, excluding cancelled or nb probe
                    GetPerfCtlValue(0x7E, 0b10111, false, 0, 0), // L2 miss, matching reqs from above
                    GetPerfCtlValue(0x7F, 1, false, 0, 0), // L2 Fill from system
                    GetPerfCtlValue(0xC0, 0, false, 0, 0), // ret instr
                    GetPerfCtlValue(0x47, 0, false, 0, 0), // Misaligned DC Access
                    GetPerfCtlValue(0x2A, 1, false, 0, 0));  // Cancelled store forward
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
                results.overallCounterValues = cpu.GetOverallCounterValues("L2 Request", "L2 Miss", "L2 Fill from System", "Instructions", "Misaligned DC Access", "Cancelled Store Forward");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC",
                "L2 Hitrate", "L2 Hit BW", "L2 MPKI", "L2 Fill BW", "Misaligned DC Access", "Cancelled Store Forwards" };

            public string GetHelpText()
            {
                return "aaaaaa";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float instr = counterData.ctr3;
                float L2Hits = counterData.ctr0 - counterData.ctr1;
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(instr),
                        string.Format("{0:F2}", instr / counterData.aperf),
                        FormatPercentage(L2Hits, counterData.ctr0),
                        FormatLargeNumber(64 * L2Hits) + "B/s",
                        string.Format("{0:F2}", 1000 * counterData.ctr1 / instr),
                        FormatLargeNumber(64 * counterData.ctr2) + "B/s",
                        FormatLargeNumber(counterData.ctr4),
                        FormatLargeNumber(counterData.ctr5)};
            }
        }

        public class DispatchStall : MonitoringConfig
        {
            private Piledriver cpu;
            public string GetConfigName() { return "Dispatch Stall"; }

            public DispatchStall(Piledriver amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0xD1, 0, false, 0, 0), // All dispatch stalls
                    GetPerfCtlValue(0xD5, 0, false, 0, 0), // ROB full
                    GetPerfCtlValue(0xD6, 0, false, 0, 0), // Integer scheduler full
                    GetPerfCtlValue(0xC0, 0b111, false, 0, 1), // x87 ops
                    GetPerfCtlValue(0xC0, 0, false, 0, 0), // Instructions
                    GetPerfCtlValue(0x42, 0b1000, false, 0, 0));  // DC refill from L2, read data error
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
                results.overallCounterValues = cpu.GetOverallCounterValues("Dispatch stall", "ROB Full Stall", "Integer Scheduler Full Stall", "STQ Full Stall", "Instructions", "DC Refill Read Data Error");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC",
                "Dispatch Stall", "ROB Full Stall", "Int Sched Full Stall", "x87 Ops", "DC Fill Data Error" };

            public string GetHelpText()
            {
                return "aaaaaa";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float instr = counterData.ctr4;
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(instr),
                        string.Format("{0:F2}", instr / counterData.aperf),
                        FormatPercentage(counterData.ctr0, counterData.aperf),
                        FormatPercentage(counterData.ctr1, counterData.aperf),
                        FormatPercentage(counterData.ctr2, counterData.aperf),
                        FormatLargeNumber(counterData.ctr3),
                        FormatLargeNumber(counterData.ctr5)
                        };
            }
        }

        public class DispatchStall1 : MonitoringConfig
        {
            private Piledriver cpu;
            public string GetConfigName() { return "Dispatch Stall 1"; }

            public DispatchStall1(Piledriver amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0xD8, 0, false, 0, 0), // LDQ Full
                    GetPerfCtlValue(0xD8, 0, false, 0, 1), // STQ Full
                    GetPerfCtlValue(0xDD, 0, false, 0, 1), // Int PRF Full
                    GetPerfCtlValue(0xDB, 0b11111, false, 0, 0), // FP exceptions
                    GetPerfCtlValue(0xC0, 0, false, 0, 0), // Instructions
                    GetPerfCtlValue(0xCB, 0b111, false, 0, 0));  // FP/MMX instructions
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
                results.overallCounterValues = cpu.GetOverallCounterValues("LDQ Full Stall", "STQ Full Stall", "INT PRF Full Stall", "FP Exceptions", "Instructions", "FP/MMX Instructions Retired");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC",
                "LDQ Full Stall", "STQ Full Stall", "INT PRF Full Stall", "FP Exceptions", "FP/MMX Instr Retired" };

            public string GetHelpText()
            {
                return "aaaaaa";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float instr = counterData.ctr4;
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(instr),
                        string.Format("{0:F2}", instr / counterData.aperf),
                        FormatPercentage(counterData.ctr0, counterData.aperf),
                        FormatPercentage(counterData.ctr1, counterData.aperf),
                        FormatPercentage(counterData.ctr2, counterData.aperf),
                        FormatLargeNumber(counterData.ctr3),
                        FormatLargeNumber(counterData.ctr5)
                        };
            }
        }

        public class DispatchStallFP : MonitoringConfig
        {
            private Piledriver cpu;
            public string GetConfigName() { return "Dispatch Stall FP"; }

            public DispatchStallFP(Piledriver amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0xD7, 0, false, 0, 0), // FP Scheduler Full
                    GetPerfCtlValue(0xDE, 0xFF, false, 0, 1), // FP PRF Full. Does this work?
                    GetPerfCtlValue(0xD0, 0, false, 0, 0), // Decoder empty
                    GetPerfCtlValue(0x3, 0xFF, false, 0, 0), // Flops retired
                    GetPerfCtlValue(0xC0, 0, false, 0, 0), // Instr
                    GetPerfCtlValue(0x5, 0b1111, false, 0, 0));  // Serializing FP ops
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
                results.overallCounterValues = cpu.GetOverallCounterValues("FP Scheduler Full", "FP PRF Full", "Decoder Empty", "FLOPs", "Instructions", "Serializing FP Ops");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC",
                "FP Scheduler Full", "FP PRF Full", "Decoder Empty", "FLOPs", "FLOPs/C", "Serializing FP Ops" };

            public string GetHelpText()
            {
                return "aaaaaa";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float instr = counterData.ctr4;
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(instr),
                        string.Format("{0:F2}", instr / counterData.aperf),
                        FormatPercentage(counterData.ctr0, counterData.aperf),
                        FormatPercentage(counterData.ctr1, counterData.aperf),
                        FormatPercentage(counterData.ctr2, counterData.aperf),
                        FormatLargeNumber(counterData.ctr3),
                        string.Format("{0:F2}", counterData.ctr3 / counterData.aperf),
                        FormatLargeNumber(counterData.ctr5)
                        };
            }
        }

        public class DispatchStallMisc : MonitoringConfig
        {
            private Piledriver cpu;
            public string GetConfigName() { return "Dispatch Stall Misc"; }

            public DispatchStallMisc(Piledriver amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0xD9, 0, false, 0, 0), // Waiting for All Quiet
                    GetPerfCtlValue(0xD3, 0, false, 0, 0), // Stall for serialization
                    GetPerfCtlValue(0x87, 0, false, 0, 0), // Instruction fetch stall
                    GetPerfCtlValue(0x04, 1, false, 0, 0), // SSE Moves eliminated
                    GetPerfCtlValue(0xC0, 0, false, 0, 0), // Instr
                    GetPerfCtlValue(0x34, 0, false, 0, 0));  // FP + Load buffer stall
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
                results.overallCounterValues = cpu.GetOverallCounterValues("Dispatch Stall Waiting for All Quiet", 
                    "Dispatch Stall for Serialization", 
                    "Instruction fetch stall", 
                    "SSE Moves Eliminated", 
                    "Instructions", 
                    "FP+Load Buffer Stall");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC",
                "Waiting for All Quiet", "Serialization Stall", "Instruction Fetch Stall", "SSE Movs Eliminated", "FP+Load Buffer Stall" };

            public string GetHelpText()
            {
                return "aaaaaa";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float instr = counterData.ctr4;
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(instr),
                        string.Format("{0:F2}", instr / counterData.aperf),
                        FormatPercentage(counterData.ctr0, counterData.aperf),
                        FormatPercentage(counterData.ctr1, counterData.aperf),
                        FormatPercentage(counterData.ctr2, counterData.aperf),
                        FormatLargeNumber(counterData.ctr3),
                        FormatPercentage(counterData.ctr5, counterData.aperf)
                        };
            }
        }

        public class DTLB : MonitoringConfig
        {
            private Piledriver cpu;
            public string GetConfigName() { return "DTLB"; }

            public DTLB(Piledriver amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0x45, 0b1110111, false, 0, 0), // L2 TLB Hit
                    GetPerfCtlValue(0x46, 0b1110111, false, 0, 0), // L2 TLB Miss
                    GetPerfCtlValue(0x85, 0, false, 0, 0), // ITLB miss L2 ITLB miss
                    GetPerfCtlValue(0x41, 0, false, 0, 0), // DC Miss
                    GetPerfCtlValue(0xC0, 0, false, 0, 0), // Instr
                    GetPerfCtlValue(0x40, 0, false, 0, 0));  // DC Access
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
                results.overallCounterValues = cpu.GetOverallCounterValues("L2 TLB Hit", "L2 TLB Miss", "ITLB Reloads", "(DC Miss)", "Instructions", "DC Access");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC",
                "DTLB Hitrate", "DTLB MPKI", "L2 TLB Hitrate", "L2 TLB MPKI", "ITLB MPKI" };

            public string GetHelpText()
            {
                return "aaaaaa";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float instr = counterData.ctr4;
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(instr),
                        string.Format("{0:F2}", instr / counterData.aperf),
                        FormatPercentage((counterData.ctr5 - counterData.ctr0 - counterData.ctr1), counterData.ctr5),
                        string.Format("{0:F2}", 1000 * (counterData.ctr0 + counterData.ctr1) / instr),
                        FormatPercentage(counterData.ctr0, counterData.ctr0 + counterData.ctr1),
                        string.Format("{0:F2}", 1000 * counterData.ctr1 / instr),
                        string.Format("{0:F2}", 1000 * counterData.ctr2 / instr)
                        };
            }
        }

        public class Offcore : MonitoringConfig
        {
            private Piledriver cpu;
            public string GetConfigName() { return "Off-Module Transfers"; }

            public Offcore(Piledriver amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0x6C, 0x2F, false, 0, 0), // Response from system on cache refill, everything except data error
                    GetPerfCtlValue(0x6D, 1, false, 0, 0), // octwords (16B) written to system
                    GetPerfCtlValue(0x65, 1, false, 0, 0), // requests to uncacheable (UC) memory
                    GetPerfCtlValue(0xC0, 0, false, 0, 0), // instructions retired
                    GetPerfCtlValue(0, 0, false, 0, 0), 
                    GetPerfCtlValue(0, 0, false, 0, 0));  
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
                results.overallCounterValues = cpu.GetOverallCounterValues("Response from System for Cache Fill", "Octwords Written to System", "UC Mem Requests", "Instructions", "Unused", "Unused");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC",
                "Response from System", "Written to System", "UC Mem Requests" };

            public string GetHelpText()
            {
                return "aaaaaa";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float instr = counterData.ctr3;
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(instr),
                        string.Format("{0:F2}", instr / counterData.aperf),                       // IPC
                        FormatLargeNumber(counterData.ctr0 * 64) + "B/s",
                        FormatLargeNumber(counterData.ctr1 * 16) + "B/s",
                        FormatLargeNumber(counterData.ctr3)
                };
            }
        }
    }
}

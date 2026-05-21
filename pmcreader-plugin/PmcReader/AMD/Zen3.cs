using System;
using PmcReader.Interop;

namespace PmcReader.AMD
{
    public class Zen3 : Amd17hCpu
    {
        public Zen3()
        {
            monitoringConfigs = new MonitoringConfig[12];
            monitoringConfigs[0] = new BpuMonitoringConfig(this);
            monitoringConfigs[1] = new FetchConfig(this);
            monitoringConfigs[2] = new DCFillConfig(this);
            monitoringConfigs[3] = new HwPrefetchConfig(this);
            monitoringConfigs[4] = new SwPrefetchConfig(this);
            monitoringConfigs[5] = new FlopsConfig(this);
            monitoringConfigs[6] = new LocksConfig(this);
            monitoringConfigs[7] = new DispatchStallConfig(this);
            monitoringConfigs[8] = new DispatchStallConfig1(this);
            monitoringConfigs[9] = new L2Config(this);
            monitoringConfigs[10] = new TopDown(this);
            monitoringConfigs[11] = new PmcMonitoringConfig(this);
            architectureName = "Zen 3";
        }

        public class BpuMonitoringConfig : MonitoringConfig
        {
            private Zen3 cpu;
            public string GetConfigName() { return "Branch Prediction"; }

            public BpuMonitoringConfig(Zen3 amdCpu) { cpu = amdCpu; }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(GetPerfCtlValue(0xC2, 0, true, true, false, false, true, false, 0, 0, false, false), // retired branches
                    GetPerfCtlValue(0xC3, 0, true, true, false, false, true, false, 0, 0, false, false),  // mispredicted retired branches
                    GetPerfCtlValue(0x8B, 0, true, true, false, false, true, false, 0, 0, false, false),  // L2 BTB override
                    GetPerfCtlValue(0x8E, 0, true, true, false, false, true, false, 0, 0, false, false),  // indirect prediction
                    GetPerfCtlValue(0x91, 0, true, true, false, false, true, false, 0, 0, false, false),  // decoder override
                    GetPerfCtlValue(0xD0, 0, true, true, false, false, true, false, 0, 1, false, false)); // retired fused branches
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
                results.overallCounterValues = cpu.GetOverallCounterValues("Retired Branches", "Retired Misp Branches", "L2 BTB Override", "Indirect Prediction", "Decoder Override", "Retired Fused Branches");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "BPU Accuracy", "Branch MPKI", "% Branches", "L2 BTB Overrides/Ki", "Indirect Predicts/Ki", "Decoder Overrides/Ki", "% Branches Fused" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        string.Format("{0:F2}%", 100 * (1 - counterData.ctr1 / counterData.ctr0)), // bpu acc
                        string.Format("{0:F2}", counterData.ctr1 / counterData.aperf * 1000),      // branch mpki
                        string.Format("{0:F2}%", 100 * counterData.ctr0 / counterData.instr),      // % branches
                        string.Format("{0:F2}", 1000 * counterData.ctr2 / counterData.instr),     // l2 btb overrides
                        string.Format("{0:F2}", 1000 * counterData.ctr3 / counterData.instr),     // ita overrides
                        string.Format("{0:F2}", 1000 * counterData.ctr4 / counterData.instr),     // decoder overrides
                        string.Format("{0:F2}%", counterData.ctr5 / counterData.ctr0 * 100) };    // fused branches
            }
        }

        public class FlopsConfig : MonitoringConfig
        {
            private Zen3 cpu;
            public string GetConfigName() { return "FLOPs"; }

            public FlopsConfig(Zen3 amdCpu) { cpu = amdCpu; }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                ulong merge = GetPerfCtlValue(0xFF, 0, false, false, false, false, true, false, 0, 0xF, false, false);
                cpu.ProgramPerfCounters(GetPerfCtlValue(0x3, 0b1000, true, true, false, false, true, false, 0, 0, false, false), merge, // fma flops, merge
                    GetPerfCtlValue(0x3, 0b11, true, true, false, false, true, false, 0, 0, false, false), merge,  // add/mul flops, merge
                    GetPerfCtlValue(0x3, 0b100, true, true, false, false, true, false, 0, 0, false, false), merge);// div flops, merge
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
                results.overallCounterValues = cpu.GetOverallCounterValues("MacFlops", "(merge)", "Mul/Add Flops", "(merge)", "Div Flops", "(merge)");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC",  "FMA Flops", "Mul/Add Flops", "Div Flops"};

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        FormatLargeNumber(counterData.ctr0),
                        FormatLargeNumber(counterData.ctr2),
                        FormatLargeNumber(counterData.ctr3)};    
            }
        }

        public class FetchConfig : MonitoringConfig
        {
            private Zen3 cpu;
            public string GetConfigName() { return "Instruction Fetch"; }

            public FetchConfig(Zen3 amdCpu) { cpu = amdCpu; }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(GetPerfCtlValue(0x8E, 0x1F, true, true, false, false, true, false, 0, 0x1, false, false), // IC access
                    GetPerfCtlValue(0x8E, 0x18, true, true, false, false, true, false, 0, 0x1, false, false),  // IC Miss
                    GetPerfCtlValue(0x8F, 0x7, true, true, false, false, true, false, 0, 0x2, false, false),  // OC Access
                    GetPerfCtlValue(0x8F, 0x4, true, true, false, false, true, false, 0, 0x2, false, false),  // OC Miss
                    GetPerfCtlValue(0xAA, 0, true, true, false, false, true, false, 0, 0, false, false),  // uop from decoder
                    GetPerfCtlValue(0xAA, 1, true, true, false, false, true, false, 0, 0, false, false)); // uop from op cache
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
                results.overallCounterValues = cpu.GetOverallCounterValues("IC Access", "IC Miss", "OC Access", "OC Miss", "Decoder Ops", "OC Ops");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "Op$ Hitrate", "Op$ MPKI", "Op$ Ops", "L1i Hitrate", "L1i MPKI", "Decoder Ops" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        string.Format("{0:F2}%", 100 * (1 - counterData.ctr1 / counterData.ctr0)),
                        string.Format("{0:F2}", 1000 * counterData.ctr1 / counterData.instr),
                        FormatLargeNumber(counterData.ctr5),
                        string.Format("{0:F2}%", 100 * (1 - counterData.ctr3 / counterData.ctr2)),
                        string.Format("{0:F2}", 1000 * counterData.ctr3 / counterData.instr),
                        FormatLargeNumber(counterData.ctr4),
                        };
            }
        }

        public class LocksConfig : MonitoringConfig
        {
            private Zen3 cpu;
            public string GetConfigName() { return "LS Locks"; }

            public LocksConfig(Zen3 amdCpu) { cpu = amdCpu; }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0x29, 0b111, true, true, false, false, true, false, 0, 0, false, false), // LS dispatch, load/store/load-op-store
                    GetPerfCtlValue(0x25, 0x1, true, true, false, false, true, false, 0, 0, false, false), // Bus lock
                    GetPerfCtlValue(0x25, 0b10, true, true, false, false, true, false, 0, 0, false, false),  // zen 2 non-speculative lock
                    GetPerfCtlValue(0x25, 0b100, true, true, false, false, true, false, 0, 0, false, false),  // zen 2 speclocklo
                    GetPerfCtlValue(0x25, 0b1000, true, true, false, false, true, false, 0, 0, false, false),  // zen 2 speclockhi
                    GetPerfCtlValue(0x25, 0xFF, true, true, false, false, true, false, 0, 0, false, false)); // all locks?
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

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues("LS Dispatch", "Bus lock", "zen2 nonspeclock", "zen 2 speclocklo", "zen 2 speclockhi", "all locks?");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "LS Dispatch", "Bus Lock", "(NonSpecLock)", "(SpecLockLo)", "(SpecLockHi)", "(All Locks)" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        FormatLargeNumber(counterData.ctr0),
                        FormatLargeNumber(counterData.ctr1),
                        FormatLargeNumber(counterData.ctr2),
                        FormatLargeNumber(counterData.ctr3),
                        FormatLargeNumber(counterData.ctr4),
                        FormatLargeNumber(counterData.ctr5)
                        };
            }
        }

        public class SwPrefetchConfig : MonitoringConfig
        {
            private Zen3 cpu;
            public string GetConfigName() { return "Software Prefetch"; }

            public SwPrefetchConfig(Zen3 amdCpu) { cpu = amdCpu; }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0x59, 1, true, true, false, false, true, false, 0, 0, false, false), // sw prefetch from local L2
                    GetPerfCtlValue(0x59, 0b10, true, true, false, false, true, false, 0, 0, false, false), // internal cache
                    GetPerfCtlValue(0x59, 0b100, true, true, false, false, true, false, 0, 0, false, false),  // external cache, same node
                    GetPerfCtlValue(0x59, 0b1000, true, true, false, false, true, false, 0, 0, false, false),  // local mem
                    GetPerfCtlValue(0x52, 0b11, true, true, false, false, true, false, 0, 0, false, false),  // ineffective prefetch
                    GetPerfCtlValue(0x4B, 0b111, true, true, false, false, true, false, 0, 0, false, false)); // prefetch instrs dispatched
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

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues("SwPf LclL2", "SwPf IntCache", "SwPf ExtCacheLocal", "SwPf MemIoLocal", "Ineffective Sw Prefetch", "Prefetch Instrs");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "SwPf, L2", "SwPf, Intra-CCX", "SwPf, Cross-CCX", "SwPf, Memory", "% SwPf Ineffective", "Ineffective SwPf", "Prefetch Instrs" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        FormatLargeNumber(64 * counterData.ctr0) + "B/s",
                        FormatLargeNumber(64 * counterData.ctr1) + "B/s",
                        FormatLargeNumber(64 * counterData.ctr2) + "B/s",
                        FormatLargeNumber(64 * counterData.ctr3) + "B/s",
                        string.Format("{0:F2}%", 100 * counterData.ctr4 / counterData.ctr5),
                        FormatLargeNumber(counterData.ctr4),
                        FormatLargeNumber(counterData.ctr5)
                        };
            }
        }

        public class HwPrefetchConfig : MonitoringConfig
        {
            private Zen3 cpu;
            public string GetConfigName() { return "Hardware Prefetch"; }

            public HwPrefetchConfig(Zen3 amdCpu) { cpu = amdCpu; }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0x5A, 1, true, true, false, false, true, false, 0, 0, false, false), // hw prefetch from local L2
                    GetPerfCtlValue(0x5A, 0b10, true, true, false, false, true, false, 0, 0, false, false), // internal cache
                    GetPerfCtlValue(0x5A, 0b100, true, true, false, false, true, false, 0, 0, false, false),  // external cache, same node
                    GetPerfCtlValue(0x5A, 0b1000, true, true, false, false, true, false, 0, 0, false, false),  // local mem
                    GetPerfCtlValue(0x5A, 0b10000, true, true, false, false, true, false, 0, 0, false, false),  // remote cache
                    GetPerfCtlValue(0x5A, 0b1000000, true, true, false, false, true, false, 0, 0, false, false)); // remote mem
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

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues("SwPf LclL2", "HwPf IntCache", "HwPf ExtCacheLocal", "HwPf MemIoLocal", "HwPf ExtCacheRemote", "HwPf MemIoRemote");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "HwPf, L2", "HwPf, Intra-CCX", "HwPf, Cross-CCX", "HwPf, Memory", "HwPf, Cross-Node Cache", "HwPf, Cross-Node Mem" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        FormatLargeNumber(64 * counterData.ctr0) + "B/s",
                        FormatLargeNumber(64 * counterData.ctr1) + "B/s",
                        FormatLargeNumber(64 * counterData.ctr2) + "B/s",
                        FormatLargeNumber(64 * counterData.ctr3) + "B/s",
                        FormatLargeNumber(64 * counterData.ctr4) + "B/s",
                        FormatLargeNumber(64 * counterData.ctr5) + "B/s"
                        };
            }
        }
        public class DCFillConfig : MonitoringConfig
        {
            private Zen3 cpu;
            public string GetConfigName() { return "All L1D Fills"; }

            public DCFillConfig(Zen3 amdCpu) { cpu = amdCpu; }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0x44, 1, true, true, false, false, true, false, 0, 0, false, false), // fill from local L2
                    GetPerfCtlValue(0x44, 0b10, true, true, false, false, true, false, 0, 0, false, false), // internal cache
                    GetPerfCtlValue(0x44, 0b100, true, true, false, false, true, false, 0, 0, false, false),  // external cache, same node
                    GetPerfCtlValue(0x44, 0b1000, true, true, false, false, true, false, 0, 0, false, false),  // local mem
                    GetPerfCtlValue(0x44, 0b10000, true, true, false, false, true, false, 0, 0, false, false),  // remote cache
                    GetPerfCtlValue(0x44, 0b1000000, true, true, false, false, true, false, 0, 0, false, false)); // remote mem
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

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues("LclL2", "IntCache", "ExtCacheLocal", "MemIoLocal", "ExtCacheRemote", "MemIoRemote");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "L2", "Intra-CCX", "Cross-CCX", "Memory", "Cross-Node Cache", "Cross-Node Mem" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        FormatLargeNumber(64 * counterData.ctr0) + "B/s",
                        FormatLargeNumber(64 * counterData.ctr1) + "B/s",
                        FormatLargeNumber(64 * counterData.ctr2) + "B/s",
                        FormatLargeNumber(64 * counterData.ctr3) + "B/s",
                        FormatLargeNumber(64 * counterData.ctr4) + "B/s",
                        FormatLargeNumber(64 * counterData.ctr5) + "B/s"
                        };
            }
        }
        public class DispatchStallConfig : MonitoringConfig
        {
            private Zen3 cpu;
            public string GetConfigName() { return "Dispatch Stalls"; }

            public DispatchStallConfig(Zen3 amdCpu) { cpu = amdCpu; }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0xAF, 0b100000, true, true, false, false, true, false, 0, 0, false, false), // ROB full
                    GetPerfCtlValue(0xAE, 1, true, true, false, false, true, false, 0, 0, false, false), // int regs full
                    GetPerfCtlValue(0xAE, 0b100000, true, true, false, false, true, false, 0, 0, false, false),  // fp regs full
                    GetPerfCtlValue(0xAE, 0b10, true, true, false, false, true, false, 0, 0, false, false),  // ldq full
                    GetPerfCtlValue(0xAE, 0b100, true, true, false, false, true, false, 0, 0, false, false),  // stq full
                    GetPerfCtlValue(0xAE, 0b10000, true, true, false, false, true, false, 0, 0, false, false)); // Taken branch buffer full
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

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues("RetireToken", "Int Regs Full", "FP Regs Full", "LDQ Full", "STQ Full", "Taken Branch Buffer Full");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "ROB Full", "Int Regs Full", "FP Regs Full", "LDQ Full", "STQ Full", "Taken Branch Buffer Full"};

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        string.Format("{0:F2}%", 100 * counterData.ctr0 / counterData.aperf),
                        string.Format("{0:F2}%", 100 * counterData.ctr1 / counterData.aperf),
                        string.Format("{0:F2}%", 100 * counterData.ctr2 / counterData.aperf),
                        string.Format("{0:F2}%", 100 * counterData.ctr3 / counterData.aperf),
                        string.Format("{0:F2}%", 100 * counterData.ctr4 / counterData.aperf),
                        string.Format("{0:F2}%", 100 * counterData.ctr5 / counterData.aperf)
                        };
            }
        }
        public class DispatchStallConfig1 : MonitoringConfig
        {
            private Zen3 cpu;
            public string GetConfigName() { return "Dispatch Stalls (Sched)"; }

            public DispatchStallConfig1(Zen3 amdCpu) { cpu = amdCpu; }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0xAF, 1, true, true, false, false, true, false, 0, 0, false, false), // sched 0 full
                    GetPerfCtlValue(0xAF, 0b10, true, true, false, false, true, false, 0, 0, false, false), // sched 1 full
                    GetPerfCtlValue(0xAF, 0b100, true, true, false, false, true, false, 0, 0, false, false),  // sched 2 full
                    GetPerfCtlValue(0xAF, 0b1000, true, true, false, false, true, false, 0, 0, false, false),  // sched 3 full
                    GetPerfCtlValue(0xAE, 0b1000000, true, true, false, false, true, false, 0, 0, false, false),  // fp sched full
                    GetPerfCtlValue(0xAE, 0b10000000, true, true, false, false, true, false, 0, 0, false, false)); // fp flush recovery
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

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues("IntSch0", "IntSch1", "IntSch2", "IntSch3", "FpSch", "FpFlush");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "Int Sched 0 Full", "Int Sched 1 Full", "Int Sched 2 Full", "Int Sched 3 Full", "FP Sched Full", "FP Flush Recovery" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        string.Format("{0:F2}%", 100 * counterData.ctr0 / counterData.aperf),
                        string.Format("{0:F2}%", 100 * counterData.ctr1 / counterData.aperf),
                        string.Format("{0:F2}%", 100 * counterData.ctr2 / counterData.aperf),
                        string.Format("{0:F2}%", 100 * counterData.ctr3 / counterData.aperf),
                        string.Format("{0:F2}%", 100 * counterData.ctr4 / counterData.aperf),
                        string.Format("{0:F2}%", 100 * counterData.ctr5 / counterData.aperf)
                        };
            }
        }
        public class L2Config : MonitoringConfig
        {
            private Zen3 cpu;
            public string GetConfigName() { return "L2 Cache"; }

            public L2Config(Zen3 amdCpu) { cpu = amdCpu; }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0x64, 0b111, true, true, false, false, true, false, 0, 0, false, false), // IC fill reqs
                    GetPerfCtlValue(0x64, 0b1, true, true, false, false, true, false, 0, 0, false, false), // IC fill miss
                    GetPerfCtlValue(0x64, 0b11111000, true, true, false, false, true, false, 0, 0, false, false),  // LS read
                    GetPerfCtlValue(0x64, 0b1000, true, true, false, false, true, false, 0, 0, false, false),  // LS read miss
                    GetPerfCtlValue(0x71, 0xFF, true, true, false, false, true, false, 0, 0, false, false),  // L2 Prefetcher hit in L3
                    GetPerfCtlValue(0x72, 0xFF, true, true, false, false, true, false, 0, 0, false, false)); // L2 Prefetcher L3 miss
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

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues("L2 Code Read", "L2 Code Miss", "L2 Data Read", "L2 Data Miss", "L2 Prefetcher Hit in L3", "L2 Prefetcher Misses in L3");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "L2 Code Hitrate", "L2 Code Hit BW", "L2 Data Hitrate", "L2 Data Hit BW", "L2 Prefetcher Hits L3", "L2 Prefetcher Misses L3" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        string.Format("{0:F2}%", 100 * (1 - counterData.ctr1 / counterData.ctr0)),
                        FormatLargeNumber(64 * counterData.ctr0 - counterData.ctr1) + "B/s",
                        string.Format("{0:F2}%", 100 * (1 - counterData.ctr3 / counterData.ctr2)),
                        FormatLargeNumber(64 * counterData.ctr3 - counterData.ctr2) + "B/s",
                        FormatLargeNumber(counterData.ctr4),
                        FormatLargeNumber(counterData.ctr5)
                        };
            }
        }

    }
}

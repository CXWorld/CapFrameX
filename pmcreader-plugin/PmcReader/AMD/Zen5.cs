using System.Collections.Generic;

namespace PmcReader.AMD
{
    public class Zen5 : Amd19hCpu
    {
        public Zen5()
        {
            List<MonitoringConfig> configList = new List<MonitoringConfig>
            {
                new Zen4TopDown(this, 8),
                new Zen4TDFrontend(this, 8),
                new Zen4TDBackend(this, 8),
                new FetchConfig(this),
                new FrontendOps(this),
                new ICMissConfig(this),
                new BPULatency(this),
                new DispatchStall(this),
                new DispatchStallSched(this),
                new L2Config(this),
                new FpPipes(this),
                new DCFill(this),
                new DemandDCFill(this),
            };
            monitoringConfigs = configList.ToArray();
            architectureName = "Zen 5";
        }

        public class BPULatency : MonitoringConfig
        {
            private Zen5 cpu;
            public string GetConfigName() { return "Branch Predictor"; }

            public BPULatency(Zen5 amdCpu) { cpu = amdCpu; }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0x8B, 0, true, true, false, false, true, false, cmask: 1, 0, false, false),  // pipe correction or cancel (l2 btb)
                    GetPerfCtlValue(0x8E, 0, true, true, false, false, true, false, cmask: 1, 0, false, false),  // ITA
                    GetPerfCtlValue(0x91, 0, true, true, false, false, true, false, 0, 0, false, false),  // decoder override/early redirect
                    GetPerfCtlValue(0x9F, 2, true, true, false, false, true, false, 0, 0, false, false), // bp redirect exredir
                    GetPerfCtlValue(0xC3, 0, true, true, false, false, true, false, 0, 0, false, false), // mispredict
                    GetPerfCtlValue(0xC2, 0, true, true, false, false, true, false, 0, 0, false, false)  // Branches
                    );
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
                results.overallCounterValues = cpu.GetOverallCounterValues("L2 BTB Override", "ITA Override", "Decoder Override", "ExRedir", "Mispredicted Branch", "Branches");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "BPU Accuracy", "BP Correct/Cancel/Ki", "ITA/Ki", "Dec Override/Ki", "Ex Redir/Ki" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float totalOps = counterData.ctr2 + counterData.ctr3;
                float totalDispatchedOps = counterData.ctr4 + counterData.ctr5;
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        FormatPercentage(counterData.ctr5 - counterData.ctr4, counterData.ctr5),
                        string.Format("{0:F2}", 1000* counterData.ctr0 / counterData.instr),
                        string.Format("{0:F2}", 1000* counterData.ctr1 / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.ctr2 / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.ctr3 / counterData.instr),
                        };
            }
        }

        public class DemandDCFill : MonitoringConfig
        {
            private Zen5 cpu;
            public string GetConfigName() { return "Demand DC Fill"; }

            public DemandDCFill(Zen5 amdCpu) { cpu = amdCpu; }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0x43, 1, true, true, false, false, true, false, cmask: 1, 0, false, false),  // L2
                    GetPerfCtlValue(0x43, 2, true, true, false, false, true, false, cmask: 1, 0, false, false),  // CCX
                    GetPerfCtlValue(0x43, 8, true, true, false, false, true, false, 0, 0, false, false),  // DRAM/IO
                    GetPerfCtlValue(0x43, 4, true, true, false, false, true, false, 0, 0, false, false), // other CCX
                    GetPerfCtlValue(0x35, 0, true, true, false, false, true, false, 0, 0, false, false), // stlf hits
                    GetPerfCtlValue(0x41, 0x7, true, true, false, false, true, false, 0, 0, false, false)  // lsmaballoc ldst
                    );
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
                results.overallCounterValues = cpu.GetOverallCounterValues("Dmd DC Fill from L2", "Dmd Fill from CCX", "Dmd Fill from DRAM", "Dmd Fill from other CCX", "stlf hit", "lsmaballoc ldst");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "Fill from L2", "Fill from CCX", "Fill from DRAM", "Other CCX", "Store Forwarded", "LDST MAB Alloc" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float totalOps = counterData.ctr2 + counterData.ctr3;
                float totalDispatchedOps = counterData.ctr4 + counterData.ctr5;
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        string.Format("{0:F2}", 1000* counterData.ctr0 / counterData.instr),
                        string.Format("{0:F2}", 1000* counterData.ctr1 / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.ctr2 / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.ctr3 / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.ctr4 / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.ctr5 / counterData.instr),
                        };
            }
        }

        public class DCFill : MonitoringConfig
        {
            private Zen5 cpu;
            public string GetConfigName() { return "DC Fill"; }

            public DCFill(Zen5 amdCpu) { cpu = amdCpu; }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0x44, 1, true, true, false, false, true, false, cmask: 1, 0, false, false),  // L2
                    GetPerfCtlValue(0x44, 2, true, true, false, false, true, false, cmask: 1, 0, false, false),  // CCX
                    GetPerfCtlValue(0x44, 8, true, true, false, false, true, false, 0, 0, false, false),  // DRAM/IO
                    GetPerfCtlValue(0x44, 4, true, true, false, false, true, false, 0, 0, false, false), // other CCX
                    GetPerfCtlValue(0x45, 0xF0, true, true, false, false, true, false, 0, 0, false, false), // L2 DTLB Miss
                    GetPerfCtlValue(0x45, 0xF, true, true, false, false, true, false, 0, 0, false, false)  // Reload from L2 TLB
                    );
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
                results.overallCounterValues = cpu.GetOverallCounterValues("DC Fill from L2", "Fill from CCX", "Fill from DRAM", "Fill from other CCX", "L2 DTLB Miss", "Fill from L2 DTLB");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "Fill from L2", "Fill from CCX", "Fill from DRAM", "Other CCX", "L2 DTLB Miss", "DTLB Miss" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float totalOps = counterData.ctr2 + counterData.ctr3;
                float totalDispatchedOps = counterData.ctr4 + counterData.ctr5;
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        string.Format("{0:F2}", 1000* counterData.ctr0 / counterData.instr),
                        string.Format("{0:F2}", 1000* counterData.ctr1 / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.ctr2 / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.ctr3 / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.ctr4 / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.ctr5 / counterData.instr),
                        };
            }
        }

        public class BackendLatency : MonitoringConfig
        {
            private Zen5 cpu;
            public string GetConfigName() { return "Backend Latency"; }

            public BackendLatency(Zen5 amdCpu) { cpu = amdCpu; }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0x41, 0xF, true, true, false, false, true, false, 0, 0, false, false),  // MAB Alloc
                    GetPerfCtlValue(0x5F, 0, true, true, false, false, true, false, 0, 0, false, false),  // Allocated DC Misses
                    GetPerfCtlValue(0xD6, 0xA2, true, true, false, false, true, false, 0, 0, false, false),  // No retire, waiting on load
                    GetPerfCtlValue(0xD6, 2, true, true, false, false, true, false, 0, 0, false, false), // No retire, non-load
                    GetPerfCtlValue(0xD6, 0xA2, true, true, edge: true, false, true, false, 0, 0, false, false), // L2 DTLB Miss
                    GetPerfCtlValue(0xD6, 0xF, true, true, edge: true, false, true, false, 0, 0, false, false)  // Reload from L2 TLB
                    );
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
                results.overallCounterValues = cpu.GetOverallCounterValues("DC Fill from L2", "Fill from CCX", "Fill from DRAM", "Fill from other CCX", "L2 DTLB Miss", "Fill from L2 DTLB");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "Fill from L2", "Fill from CCX", "Fill from DRAM", "Other CCX", "L2 DTLB Miss", "DTLB Miss" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float totalOps = counterData.ctr2 + counterData.ctr3;
                float totalDispatchedOps = counterData.ctr4 + counterData.ctr5;
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        string.Format("{0:F2}", 1000* counterData.ctr0 / counterData.instr),
                        string.Format("{0:F2}", 1000* counterData.ctr1 / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.ctr2 / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.ctr3 / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.ctr4 / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.ctr5 / counterData.instr),
                        };
            }
        }

        public class FrontendOps : MonitoringConfig
        {
            private Zen5 cpu;
            public string GetConfigName() { return "Ops from Frontend"; }

            public FrontendOps(Zen5 amdCpu) { cpu = amdCpu; }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(
                    GetPerfCtlValue(0xAA, 1, true, true, false, false, true, false, cmask: 1, 0, false, false),  // decoder active
                    GetPerfCtlValue(0xAA, 0b10, true, true, false, false, true, false, cmask: 1, 0, false, false),  // oc active
                    GetPerfCtlValue(0xAA, 1, true, true, false, false, true, false, 0, 0, false, false),  // uop from decoder
                    GetPerfCtlValue(0xAA, 0b10, true, true, false, false, true, false, 0, 0, false, false), // uop from op cache
                    GetPerfCtlValue(0xAB, 8, true, true, false, false, true, false, 0, 0, false, false), // integer dispatch
                    GetPerfCtlValue(0xAB, 4, true, true, false, false, true, false, 0, 0, false, false)  // fp dispatch
                    ); 
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
                results.overallCounterValues = cpu.GetOverallCounterValues("Decoder Cycles", "OC Cycles", "Decoder Ops", "OC Ops", "Integer op dispatch", "FP op dispatch");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "OC %", "Decoder %", "OC Active", "Decoder Active", "OC Ops", "Decoder Ops", "Integer Ops", "FP Ops" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float totalOps = counterData.ctr2 + counterData.ctr3;
                float totalDispatchedOps = counterData.ctr4 + counterData.ctr5;
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        FormatPercentage(counterData.ctr3, totalOps),
                        FormatPercentage(counterData.ctr2, totalOps),
                        FormatPercentage(counterData.ctr1, counterData.aperf),
                        FormatPercentage(counterData.ctr0, counterData.aperf),
                        FormatLargeNumber(counterData.ctr3),
                        FormatLargeNumber(counterData.ctr2),
                        FormatLargeNumber(counterData.ctr4),
                        FormatLargeNumber(counterData.ctr5)
                        };
            }
        }

        public class DispatchStall : MonitoringConfig
        {
            private Zen5 cpu;
            public string GetConfigName() { return "Dispatch Stalls"; }

            public DispatchStall(Zen5 amdCpu) { cpu = amdCpu; }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(GetPerfCtlValue(0xAF, (byte)(1U << 5), true, true, false, false, true, false, 0, 0, false, false), // ROB
                    GetPerfCtlValue(0xAE, 1, true, true, false, false, true, false, 0, 0, false, false), // integer RF
                    GetPerfCtlValue(0xAE, (byte)(1U << 6), true, true, false, false, true, false, 0, 0, false, false), // FP NSQ
                    GetPerfCtlValue(0xAE, 2, true, true, false, false, true, false, 0, 0, false, false), // LDQ
                    GetPerfCtlValue(0xAE, 4, true, true, false, false, true, false, 0, 0, false, false), // STQ
                    GetPerfCtlValue(0xAE, 0x10, true, true, false, false, true, false, 0, 0, false, false)); // taken branch buffer
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
                results.overallCounterValues = cpu.GetOverallCounterValues("ROB", "INT Regs", "FP NSQ", "LDQ", "STQ", "Taken Branch Buffer");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "ROB", "INT Regs", "FP NSQ", "LDQ", "STQ", "Taken Branch Buffer"};

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
                        FormatPercentage(counterData.ctr0, counterData.aperf),
                        FormatPercentage(counterData.ctr2, counterData.aperf),
                        FormatPercentage(counterData.ctr4, counterData.aperf),
                        FormatPercentage(counterData.ctr1, counterData.aperf),
                        FormatPercentage(counterData.ctr3, counterData.aperf),
                        FormatPercentage(counterData.ctr5, counterData.aperf)
                        };
            }
        }

        public class DispatchStallSched : MonitoringConfig
        {
            private Zen5 cpu;
            public string GetConfigName() { return "Dispatch Stalls (Sched)"; }

            public DispatchStallSched(Zen5 amdCpu) { cpu = amdCpu; }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(GetPerfCtlValue(0xAF, 1, true, true, false, false, true, false, 0, 0, false, false), // ALU Tokens
                    GetPerfCtlValue(0xAF, 2, true, true, false, false, true, false, 0, 0, false, false), // AGU Tokens
                    GetPerfCtlValue(0xAE, (byte)(1U << 6), true, true, false, false, true, false, 0, 0, false, false), // FP NSQ
                    GetPerfCtlValue(0xAE, 4, true, true, false, false, true, false, 0, 0, false, false), // integer execution flush
                    GetPerfCtlValue(0xA2, 0x30, true, true, false, false, true, false, 0, 1, false, false), // misc
                    GetPerfCtlValue(0xAE, 0x10, true, true, false, false, true, false, 0, 0, false, false)); // taken branch buffer (unused)
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
                results.overallCounterValues = cpu.GetOverallCounterValues("ALU Scheduler", "AGU Scheduler", "FP NSQ", "Integer Execution Flush", "Misc", "Taken Branch Buffer");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "ALU Scheduler", "AGU Scheduler", "FP NSQ", "Integer Execution Flush", "Misc", "Taken Branch Buffer" };

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
                        FormatPercentage(counterData.ctr0, counterData.aperf),
                        FormatPercentage(counterData.ctr1, counterData.aperf),
                        FormatPercentage(counterData.ctr2, counterData.aperf),
                        FormatPercentage(counterData.ctr3, counterData.aperf),
                        FormatPercentage(counterData.ctr4, counterData.aperf),
                        FormatPercentage(counterData.ctr5, counterData.aperf)
                        };
            }
        }

        public class FpPipes : MonitoringConfig
        {
            private Zen5 cpu;
            public string GetConfigName() { return "FP Pipes (undoc)"; }

            public FpPipes(Zen5 amdCpu) { cpu = amdCpu; }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(GetPerfCtlValue(0, 1, true, true, false, false, true, false, 0, 0, false, false),
                    GetPerfCtlValue(0, 2, true, true, false, false, true, false, 0, 0, false, false), 
                    GetPerfCtlValue(0, 4, true, true, false, false, true, false, 0, 0, false, false),
                    GetPerfCtlValue(0, 8, true, true, false, false, true, false, 0, 0, false, false),
                    GetPerfCtlValue(0, 0x10, true, true, false, false, true, false, 0, 0, false, false),
                    GetPerfCtlValue(0, 0x20, true, true, false, false, true, false, 0, 0, false, false));
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
                results.overallCounterValues = cpu.GetOverallCounterValues("0", "1", "2", "3", "4", "5");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "P0 FMA", "P2 FADD", "P4 FStore", "P1 FMA", "P3 FADD", "P5 FStore" };

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
                        FormatPercentage(counterData.ctr0, counterData.aperf),
                        FormatPercentage(counterData.ctr2, counterData.aperf),
                        FormatPercentage(counterData.ctr4, counterData.aperf),
                        FormatPercentage(counterData.ctr1, counterData.aperf),
                        FormatPercentage(counterData.ctr3, counterData.aperf),
                        FormatPercentage(counterData.ctr5, counterData.aperf)
                        };
            }
        }

        public class ICMissConfig : MonitoringConfig
        {
            private Zen5 cpu;
            public string GetConfigName() { return "L1i Miss"; }

            public ICMissConfig(Zen5 amdCpu) { cpu = amdCpu; }

            public string[] GetColumns() { return columns; }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(GetPerfCtlValue(0x8E, 0x1F, true, true, false, false, true, false, 0, 0x1, false, false), // IC access
                    GetPerfCtlValue(0x8E, 0x18, true, true, false, false, true, false, 0, 0x1, false, false),  // IC Miss
                    GetPerfCtlValue(0x8F, 0x7, true, true, false, false, true, false, 0, 0x2, false, false),  // OC Access
                    GetPerfCtlValue(0x8F, 0x4, true, true, false, false, true, false, 0, 0x2, false, false),  // OC Miss
                    GetPerfCtlValue(0x82, 0, true, true, false, false, true, false, 0, 0, false, false),  // IC refill from L2
                    GetPerfCtlValue(0x83, 0, true, true, false, false, true, false, 0, 0, false, false)); // IC refill from system
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
                results.overallCounterValues = cpu.GetOverallCounterValues("IC Access", "IC Miss", "OC Access", "OC Miss", "IC refill from L2", "IC refill from system ");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC",
                "Op$ Hitrate", "Op$ MPKI", "L1i Hitrate", "L1i MPKI", "L2 Code Hitrate", "L2 Code MPKI" };

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
                        string.Format("{0:F2}%", 100 * (1 - counterData.ctr3 / counterData.ctr2)),
                        string.Format("{0:F2}", 1000 * counterData.ctr3 / counterData.instr),
                        string.Format("{0:F2}%", 100 * (1 - counterData.ctr1 / counterData.ctr0)),
                        string.Format("{0:F2}", 1000 * counterData.ctr1 / counterData.instr),
                        FormatPercentage(counterData.ctr4, counterData.ctr4 + counterData.ctr5),
                        string.Format("{0:F2}", 1000 * counterData.ctr5 / counterData.instr),
                        };
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics.PerformanceData;
using PmcReader.Interop;

namespace PmcReader.AMD
{
    public class Zen2 : Amd17hCpu
    {
        public Zen2()
        {
            List<MonitoringConfig> monitoringConfigList = new List<MonitoringConfig>();
            monitoringConfigList.Add(new BpuMonitoringConfig(this));
            monitoringConfigList.Add(new BPUMonitoringConfig1(this));
            monitoringConfigList.Add(new ICMonitoringConfig(this));
            monitoringConfigList.Add(new OpCacheConfig(this));
            monitoringConfigList.Add(new DecodeHistogram(this));
            monitoringConfigList.Add(new ResourceStallMontitoringConfig(this));
            monitoringConfigList.Add(new IntSchedulerMonitoringConfig(this));
            monitoringConfigList.Add(new DtlbConfig(this));
            monitoringConfigList.Add(new PageWalkConfig(this));
            monitoringConfigList.Add(new LSConfig(this));
            monitoringConfigList.Add(new LSSwPrefetch(this));
            monitoringConfigList.Add(new DCFillSource(this, "L1D Demand Fill", 0x43));
            monitoringConfigList.Add(new DCBWMonitoringConfig(this));
            monitoringConfigList.Add(new DCFillLatencyConfig(this));
            monitoringConfigList.Add(new MABOccupancyConfig(this));
            monitoringConfigList.Add(new DCFillSource(this, "L1D HwPf Fill", 0x5A));
            monitoringConfigList.Add(new DCFillSource(this, "L1D TableWalker Fill", 0x5B));
            monitoringConfigList.Add(new L2MonitoringConfig(this));
            monitoringConfigList.Add(new FlopsMonitoringConfig(this));
            monitoringConfigList.Add(new RetireConfig(this));
            monitoringConfigList.Add(new RetireBurstConfig(this));
            monitoringConfigList.Add(new PowerConfig(this));
            monitoringConfigList.Add(new TestConfig(this));
            monitoringConfigList.Add(new Locks(this));
            monitoringConfigList.Add(new MiscConfig(this));
            monitoringConfigList.Add(new FpDispFault(this));
            monitoringConfigList.Add(new L2Latency(this));
            monitoringConfigList.Add(new PmcMonitoringConfig(this));
            monitoringConfigList.Add(new TopDownConfig(this));
            monitoringConfigs = monitoringConfigList.ToArray();

            architectureName = "Zen 2";
        }

        public class OpCacheConfig : MonitoringConfig
        {
            private Zen2 cpu;
            public string GetConfigName() { return "Decode/Op Cache"; }

            public OpCacheConfig(Zen2 amdCpu)
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
                    // Set PERF_CTR0 to count ops delivered from op cache
                    Ring0.WriteMsr(MSR_PERF_CTL_0, GetPerfCtlValue(0xAA, 0x2, true, true, false, false, true, false, 0, 0, false, false));

                    // PERF_CTR1 = ops delivered from op cache, cmask=1
                    Ring0.WriteMsr(MSR_PERF_CTL_1, GetPerfCtlValue(0xAA, 0x2, true, true, false, false, true, false, 1, 0, false, false));

                    // PERF_CTR2 = ops delivered from decoder
                    Ring0.WriteMsr(MSR_PERF_CTL_2, GetPerfCtlValue(0xAA, 0x1, true, true, false, false, true, false, 0, 0, false, false));

                    // PERF_CTR3 = ops delivered from decoder, cmask=1
                    Ring0.WriteMsr(MSR_PERF_CTL_3, GetPerfCtlValue(0xAA, 0x1, true, true, false, false, true, false, 1, 0, false, false));

                    // PERF_CTR4 = retired micro ops
                    Ring0.WriteMsr(MSR_PERF_CTL_4, GetPerfCtlValue(0xC1, 0, true, true, false, false, true, false, 0, 0, false, false));

                    // PERF_CTR5 = micro-op queue empty cycles
                    Ring0.WriteMsr(MSR_PERF_CTL_5, GetPerfCtlValue(0xA9, 0, true, true, false, false, true, false, 0, 0, false, false));
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

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues("Op Cache Ops", "Op Cache Ops cmask=1", "Decoder Ops", "Decoder Ops cmask=1", "Retired Ops", "Mop Queue Empty Cycles");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "Ops/C", "Op$ Hitrate", "Op$ Ops/C", "Op$ Active", "Op$ Ops", "Decoder Ops/C", "Decoder Active", "Decoder Ops", "Bogus Ops", "Op Queue Empty Cycles" };

            public string GetHelpText()
            {
                return "Op$ throughput is 8 ops/c\n" + 
                    "Decoder throughput is 4 instr/c\n" + 
                    "Bogus Ops - Micro-ops dispatched, but never retired (wasted work from bad speculation)\n" + 
                    "Op Queue Empty Cycles - could indicate a frontend bottleneck";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float bogusOps = 100 * (counterData.ctr0 + counterData.ctr2 - counterData.ctr4) / (counterData.ctr0 + counterData.ctr2);
                if (counterData.ctr4 > counterData.ctr0 + counterData.ctr2) bogusOps = 0;
                return new string[] { label,
                    FormatLargeNumber(counterData.aperf),
                    FormatLargeNumber(counterData.instr),
                    string.Format("{0:F2}", counterData.instr / counterData.aperf),
                    string.Format("{0:F2}", counterData.ctr4 / counterData.aperf),
                    string.Format("{0:F2}%", 100 * counterData.ctr0 / (counterData.ctr0 + counterData.ctr2)),
                    string.Format("{0:F2}", counterData.ctr0 / counterData.ctr1),
                    string.Format("{0:F2}%", 100 * counterData.ctr1 / counterData.aperf),
                    FormatLargeNumber(counterData.ctr0),
                    string.Format("{0:F2}", counterData.ctr2 / counterData.ctr3),
                    string.Format("{0:F2}%", 100 * counterData.ctr3 / counterData.aperf),
                    FormatLargeNumber(counterData.ctr2),
                    string.Format("{0:F2}%", bogusOps),
                    string.Format("{0:F2}%", 100 * counterData.ctr5 / counterData.aperf) };
            }
        }

        public class TopDownConfig : MonitoringConfig
        {
            private Zen2 cpu;
            public string GetConfigName() { return "Top Down?"; }

            public TopDownConfig(Zen2 amdCpu)
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
                    // Set PERF_CTR0 to count ops delivered from the frontend (any path)
                    Ring0.WriteMsr(MSR_PERF_CTL_0, GetPerfCtlValue(0xAA, 0x3, true, true, false, false, true, false, 0, 0, false, false));

                    // PERF_CTR1 = dispatch stalls 1
                    Ring0.WriteMsr(MSR_PERF_CTL_1, GetPerfCtlValue(0xAE, 0xFF, true, true, false, false, true, false, 0, 0, false, false));

                    // PERF_CTR2 = dispatch stalls 2
                    Ring0.WriteMsr(MSR_PERF_CTL_2, GetPerfCtlValue(0xAF, 0x7F, true, true, false, false, true, false, 0, 0, false, false));

                    // PERF_CTR3 = cycles FE active
                    Ring0.WriteMsr(MSR_PERF_CTL_3, GetPerfCtlValue(0xAA, 0x3, true, true, false, false, true, false, cmask: 1, 0, false, false));

                    // PERF_CTR4 = retired micro ops
                    Ring0.WriteMsr(MSR_PERF_CTL_4, GetPerfCtlValue(0xC1, 0, true, true, false, false, true, false, 0, 0, false, false));

                    // PERF_CTR5 = micro-op queue empty cycles
                    Ring0.WriteMsr(MSR_PERF_CTL_5, GetPerfCtlValue(0xA9, 0, true, true, false, false, true, false, 0, 0, false, false));
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

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues("FE Ops", "Dispatch Stall 1", "Dispatch Stall 2", "FE Active", "Retired Ops", "Op Queue Empty");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", 
                "Ops/C", "FE Ops/C", "FE active", "Op Queue Empty", "Dispatch Stall (total)", "Width Used" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float bogusOps = 100 * (counterData.ctr0 + counterData.ctr2 - counterData.ctr4) / (counterData.ctr0 + counterData.ctr2);
                if (counterData.ctr4 > counterData.ctr0 + counterData.ctr2) bogusOps = 0;
                return new string[] { label,
                    FormatLargeNumber(counterData.aperf),
                    FormatLargeNumber(counterData.instr),
                    string.Format("{0:F2}", counterData.instr / counterData.aperf), // ipc
                    string.Format("{0:F2}", counterData.ctr4 / counterData.aperf), // ops/c
                    string.Format("{0:F2}", counterData.ctr0 / counterData.aperf), // fe ops/c
                    string.Format("{0:F2}%", 100 * counterData.ctr3 / counterData.aperf), // fe active
                    string.Format("{0:F2}%", 100 * counterData.ctr5 / counterData.aperf), // op queue empty
                    string.Format("{0:F2}%", 100 * (counterData.ctr1 + counterData.ctr2) / counterData.aperf), // dispatch stall
                    string.Format("{0:F2}%", 100 * counterData.ctr4 / (5*counterData.aperf))}; // width used
            }
        }

        public class BpuMonitoringConfig : MonitoringConfig
        {
            private Zen2 cpu;
            public string GetConfigName() { return "Branch Prediction and Fusion"; }

            public BpuMonitoringConfig(Zen2 amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(GetPerfCtlValue(0xC2, 0, true, true, false, false, true, false, 0, 0, false, false),
                    GetPerfCtlValue(0xC3, 0, true, true, false, false, true, false, 0, 0, false, false),
                    GetPerfCtlValue(0x8A, 0, true, true, false, false, true, false, 0, 0, false, false),
                    GetPerfCtlValue(0x8B, 0, true, true, false, false, true, false, 0, 0, false, false),
                    GetPerfCtlValue(0x91, 0, true, true, false, false, true, false, 0, 0, false, false),
                    GetPerfCtlValue(0xD0, 0, true, true, false, false, true, false, 0, 1, false, false));
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

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "BPU Accuracy", "Branch MPKI", "L1 BTB Overhead", "L2 BTB Overhead", "Decoder Overrides/1K Instr", "% Branches Fused" };

            public string GetHelpText()
            {
                return "BPU Accuracy - (1 - retired mispredicted branches / all retired branches)\n" + 
                    "BTB overhead - Zen uses a 3-level overriding predictor\n" + 
                    "- L1 BTB overriding L0 creates a 1-cycle bubble\n" + 
                    "- L2 BTB overriding L1 creates a 4-cycle bubble\n" + 
                    "The BTB Overhead columns  show bubbles / total cycles\n" + 
                    "Decoder Overrides - BTB miss I think. Shown as events per 1000 instr\n" +
                    "Branches Fused - % of branches fused with a previous instruction, so 2 instr counts as 1";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {

                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        string.Format("{0:F2}%", 100 * (1 - counterData.ctr1 / counterData.ctr0)),
                        string.Format("{0:F2}", counterData.ctr1 / counterData.instr * 1000),
                        string.Format("{0:F2}%", counterData.ctr2 / counterData.aperf * 100),
                        string.Format("{0:F2}%", (4 * counterData.ctr3) / counterData.aperf * 100),
                        string.Format("{0:F2}", counterData.ctr4 / counterData.instr * 1000),
                        string.Format("{0:F2}%", counterData.ctr5 / counterData.ctr0 * 100) };
            }
        }

        public class FlopsMonitoringConfig : MonitoringConfig
        {
            private Zen2 cpu;
            public string GetConfigName() { return "Floppy Flops"; }
            public string[] columns = new string[] { "Item", "Instructions", "IPC", "FLOPs", "FMA FLOPs", "Non-FMA FLOPs", "FLOPs/c", "FMA FLOPS/c", "Non-FMA FLOPS/c", "FP Sch Full Stall", "FP Regs Full Stall" };

            public FlopsMonitoringConfig(Zen2 amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public string GetHelpText()
            {
                return "Floating point operations per second\n" +
                    "FMA FLOPs - FLOPs from fused multiply add ops\n" +
                    "FP Sch Full - Dispatch from frontend blocked because the FP scheduler was full\n" +
                    "FP Regs Full -Incoming FP op needed a result register and none were available";
            }

            public void Initialize()
            {
                cpu.EnablePerformanceCounters();
                cpu.InitializeCoreTotals();
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    ThreadAffinity.Set(1UL << threadIdx);

                    // Set PERF_CTR0 to count mac flops
                    // counting these separately because they have to be multiplied by 2
                    // PPR says "MacFLOPs count as 2 FLOPs", and max increment is 64 (8-wide retire, each 256b vector can be 8x FP32
                    // so max increment for retiring 8x 256b FMAs should be 128 if it's already counting double)
                    ulong macFlops = GetPerfCtlValue(0x3, 0x8, true, true, false, false, true, false, 0, 0, false, false);
                    Ring0.WriteMsr(MSR_PERF_CTL_0, macFlops);

                    // PERF_CTR1 = merge
                    ulong merge = GetPerfCtlValue(0xFF, 0, false, false, false, false, true, false, 0, 0xF, false, false);
                    Ring0.WriteMsr(MSR_PERF_CTL_1, merge);

                    // PERF_CTR2 = div/sqrt/fmul/fadd flops
                    ulong nonMacFlops = GetPerfCtlValue(0x3, 0x7, true, true, false, false, true, false, 0, 0, false, false);
                    Ring0.WriteMsr(MSR_PERF_CTL_2, nonMacFlops);

                    // PERF_CTR3 = merge
                    Ring0.WriteMsr(MSR_PERF_CTL_3, merge);

                    // PERF_CTR4 = dispatch stall because FP scheduler is full
                    ulong fpSchedulerFullStall = GetPerfCtlValue(0xAE, 0x40, true, true, false, false, true, false, 0, 0, false, false);
                    Ring0.WriteMsr(MSR_PERF_CTL_4, fpSchedulerFullStall);

                    // PERF_CTR5 = dispatch stall because FP register file is full
                    ulong fpRegsFullStall = GetPerfCtlValue(0xAE, 0x20, true, true, false, false, true, false, 0, 0, false, false);
                    Ring0.WriteMsr(MSR_PERF_CTL_5, fpRegsFullStall);
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
                results.overallCounterValues = cpu.GetOverallCounterValues("MAC FLOPs", "(merge)", "Non-MAC FLOPs", "(merge)", "FP Scheduler Full", "FP Registers Full");
                return results;
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                return new string[] { "Overall",
                        FormatLargeNumber(counterData.aperf) + "/s",
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        FormatLargeNumber(counterData.ctr0 + counterData.ctr2) + "/s",
                        FormatLargeNumber(counterData.ctr0) + "/s",
                        FormatLargeNumber(counterData.ctr2) + "/s",
                        string.Format("{0:F1}", (counterData.ctr0 + counterData.ctr2) / counterData.aperf),
                        string.Format("{0:F1}", counterData.ctr0 / counterData.aperf),
                        string.Format("{0:F1}", (float)counterData.ctr2 / counterData.aperf),
                        string.Format("{0:F2}%", counterData.ctr4 / counterData.aperf),
                        string.Format("{0:F2}%", counterData.ctr5 / counterData.aperf) };
            }
        }

        public class ResourceStallMontitoringConfig : MonitoringConfig
        {
            private Zen2 cpu;
            public string GetConfigName() { return "Dispatch Stalls"; }

            public ResourceStallMontitoringConfig(Zen2 amdCpu)
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

                    // PERF_CTR0 = Dispatch resource stall cycles, retire tokens unavailable
                    Ring0.WriteMsr(MSR_PERF_CTL_0, GetPerfCtlValue(0xAF, 0x20, true, true, false, false, true, false, 0, 0, false, false));

                    // PERF_CTR1 = Dispatch resource stall cycles (1), load queue
                    Ring0.WriteMsr(MSR_PERF_CTL_1, GetPerfCtlValue(0xAE, 0x2, true, true, false, false, true, false, 0, 0, false, false));

                    // PERF_CTR2 = Dispatch resource stall cycles (1), store queue
                    Ring0.WriteMsr(MSR_PERF_CTL_2, GetPerfCtlValue(0xAE, 0x4, true, true, false, false, true, false, 0, 0, false, false));

                    // PERF_CTR3 = Dispatch resource stall cycles (1), taken branch buffer stall
                    Ring0.WriteMsr(MSR_PERF_CTL_3, GetPerfCtlValue(0xAE, 0x10, true, true, false, false, true, false, 0, 0, false, false));

                    // PERF_CTR4 = Dispatch resource stall cycles, SC AGU dispatch stall
                    Ring0.WriteMsr(MSR_PERF_CTL_4, GetPerfCtlValue(0xAF, 0x4, true, true, false, false, true, false, 0, 0, false, false));

                    // PERF_CTR5 = Dispatch resource stall cycles, AGSQ token stall
                    Ring0.WriteMsr(MSR_PERF_CTL_5, GetPerfCtlValue(0xAF, 0x10, true, true, false, false, true, false, 0, 0, false, false));
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
                results.overallCounterValues = cpu.GetOverallCounterValues("ROB Full", "LDQ Full", "STQ Full", "Taken Branch Buffer Full", "ScAguDispatchStall", "AGSQ Full");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "ROB Full", "LDQ Full", "STQ Full", "Taken Branch Buffer Full", "AGU Sched Stall", "AGSQ Token Stall" };

            public string GetHelpText()
            {
                return "Dispatch from frontend -> backend blocked because:\n" + 
                    "ROB: Reorder buffer full. Instructions in flight limit reached\n" + 
                    "LDQ: Load queue full. Loads in flight limit reached\n" + 
                    "STQ: Store queue full. Stores in flight limit reached\n" + 
                    "Taken Branch Buffer: Used for fast recovery from branch mispredicts. Branches in flight limit reached\n" + 
                    "AGU Sched full: Can't track more memory ops waiting to be executed\n" + 
                    "AGSQ tokens: Also for AGU scheduling queue? Not sure how this differs from AGU Sched\n";
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
                    string.Format("{0:F2}%", 100 * counterData.ctr5 / counterData.aperf),
                };
            }
        }

        public class IntSchedulerMonitoringConfig : MonitoringConfig
        {
            private Zen2 cpu;
            public string GetConfigName() { return "Dispatch Stalls 1 (Int Sched)"; }

            public IntSchedulerMonitoringConfig(Zen2 amdCpu)
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

                    // PERF_CTR0 = Dispatch resource stall cycles, ALSQ3_0 token stall (adc)
                    ulong alsq3_0TokenStall = GetPerfCtlValue(0xAF, 0x4, true, true, false, false, true, false, 0, 0, false, false);
                    Ring0.WriteMsr(MSR_PERF_CTL_0, alsq3_0TokenStall);

                    // PERF_CTR1 = Dispatch resource stall cycles, ALSQ 1 resources unavailable (int, mul)
                    ulong alsq1TokenStall = GetPerfCtlValue(0xAF, 0x1, true, true, false, false, true, false, 0, 0, false, false);
                    Ring0.WriteMsr(MSR_PERF_CTL_1, alsq1TokenStall);

                    // PERF_CTR2 = Dispatch resource stall cycles, ALSQ 2 resources unavailable (int, div)
                    ulong alsq2TokenStall = GetPerfCtlValue(0xAF, 0x2, true, true, false, false, true, false, 0, 0, false, false);
                    Ring0.WriteMsr(MSR_PERF_CTL_2, alsq2TokenStall);

                    // PERF_CTR3 = Dispatch resource stall cycles, ALU tokens unavailable
                    ulong aluTokenStall = GetPerfCtlValue(0xAF, 0x10, true, true, false, false, true, false, 0, 0, false, false);
                    Ring0.WriteMsr(MSR_PERF_CTL_3, aluTokenStall);

                    // PERF_CTR4 = Dispatch resource stall cycles (1), integer physical register file resource stall
                    ulong intPrfStall = GetPerfCtlValue(0xAE, 0x1, true, true, false, false, true, false, 0, 0, false, false);
                    Ring0.WriteMsr(MSR_PERF_CTL_4, intPrfStall);

                    // PERF_CTR5 = Dispatch resource stall cycles (1), integer scheduler misc stall
                    ulong robFullStall = GetPerfCtlValue(0xAE, 0x40, true, true, false, false, true, false, 0, 0, false, false);
                    Ring0.WriteMsr(MSR_PERF_CTL_5, robFullStall);
                }
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "ALSQ3_0 Full Stall", "ALSQ1 Full Stall", "ALSQ2 Full Stall", "ALU Token Stall", "Int Regs Full Stall", "Int Sched Misc Stall" };

            public string GetHelpText()
            {
                return "Dispatch from frontend -> backend blocked because:\n" + 
                    "ALSQ3_0 - Scheduler queue for ALU0 or ALU3 ports full\n" +
                    "ALSQ1 - Scheduler queue for ALU1 full (multiplier lives here)\n" +
                    "ALSQ2 - Scheduler queue for ALU2 full (divider here)\n" +
                    "ALU Token Stall - Some structure that tracks ALU ops is full\n" +
                    "Int regs full - Incoming op needed an INT result register, but no regs were free";
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
                results.overallCounterValues = cpu.GetOverallCounterValues("ALSQ3_0 Full", "ALSQ1 Full", "ALSQ2 Full", "ALU Tokens Unavailable", "Int Regs Full", "Int Sched Misc Stall");
                return results;
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
                    string.Format("{0:F2}%", 100 * counterData.ctr5 / counterData.aperf),
                };
            }
        }
        public class L2MonitoringConfig : MonitoringConfig
        {
            private Zen2 cpu;
            public string GetConfigName() { return "L2 Cache"; }

            public L2MonitoringConfig(Zen2 amdCpu)
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
                ulong l2CodeRequests = GetPerfCtlValue(0x64, 0x7, true, true, false, false, true, false, 0, 0, false, false);
                ulong l2CodeMiss = GetPerfCtlValue(0x64, 0x1, true, true, false, false, true, false, 0, 0, false, false);
                ulong l2DataRequests = GetPerfCtlValue(0x64, 0xF8, true, true, false, false, true, false, 0, 0, false, false);
                ulong l2DataMiss = GetPerfCtlValue(0x64, 0x8, true, true, false, false, true, false, 0, 0, false, false);
                ulong l2PrefetchRequests = GetPerfCtlValue(0x60, 0x2, true, true, false, false, true, false, 0, 0, false, false);
                ulong l2PrefetchHits = GetPerfCtlValue(0x70, 0xFF, true, true, false, false, true, false, 0, 0, false, false);

                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    ThreadAffinity.Set(1UL << threadIdx);
                    Ring0.WriteMsr(MSR_PERF_CTL_0, l2CodeRequests);
                    Ring0.WriteMsr(MSR_PERF_CTL_1, l2CodeMiss);
                    Ring0.WriteMsr(MSR_PERF_CTL_2, l2DataRequests);
                    Ring0.WriteMsr(MSR_PERF_CTL_3, l2DataMiss);
                    Ring0.WriteMsr(MSR_PERF_CTL_4, l2PrefetchRequests);
                    Ring0.WriteMsr(MSR_PERF_CTL_5, l2PrefetchHits);
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
                results.overallCounterValues = cpu.GetOverallCounterValues("L2 Code Req", "L2 Code Miss", "L2 Data Req", "L2 Data Miss", "L2 Prefetch Requests", "L2 Prefetch Hit");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "L2 Hitrate", "L2 Hit BW", "L2 Code Hitrate", "L2 Code Hit BW", "L2 Data Hitrate", "L2 Data Hit BW", "L2 Prefetch Hitrate", "L2 Prefetch BW" };

            public string GetHelpText()
            {
                return "Hitrate - hitrate for all requests, including prefetch\n" +
                    "Code hitrate - hitrate for instruction cache fills\n" +
                    "Code hit bw - instruction cache fill hits * 64B, assuming each hit is for a 64B cache line\n" +
                    "Data - ^^ for data cache fills\n" +
                    "Prefetch - ^^ for data cache prefetch fills";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float l2CodeRequests = counterData.ctr0;
                float l2CodeMisses = counterData.ctr1;
                float l2DataRequests = counterData.ctr2;
                float l2DataMisses = counterData.ctr3;
                float l2PrefetchRequests = counterData.ctr4;
                float l2PrefetchHits = counterData.ctr5;
                float l2Hitrate = (l2PrefetchHits + l2CodeRequests + l2DataRequests - l2CodeMisses - l2DataMisses) / (l2CodeRequests + l2DataRequests + l2PrefetchRequests) * 100;
                float l2HitBw = (l2PrefetchHits + l2CodeRequests + l2DataRequests - l2CodeMisses - l2DataMisses) * 64;
                float l2CodeHitrate = (1 - l2CodeMisses / l2CodeRequests) * 100;
                float l2CodeHitBw = (l2CodeRequests - l2CodeMisses) * 64;
                float l2DataHitrate = (1 - l2DataMisses / l2DataRequests) * 100;
                float l2DataHitBw = (l2DataRequests - l2DataMisses) * 64 ;
                float l2PrefetchHitrate = l2PrefetchHits / l2PrefetchRequests * 100;
                float l2PrefetchBw = l2PrefetchHits * 64;
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        string.Format("{0:F2}%", l2Hitrate),
                        FormatLargeNumber(l2HitBw) + "B/s",
                        string.Format("{0:F2}%", l2CodeHitrate),
                        FormatLargeNumber(l2CodeHitBw) + "B/s",
                        string.Format("{0:F2}%", l2DataHitrate),
                        FormatLargeNumber(l2DataHitBw) + "B/s",
                        string.Format("{0:F2}%", l2PrefetchHitrate),
                        FormatLargeNumber(l2PrefetchBw) + "B/s"};
            }
        }

        public class DCFillSource : MonitoringConfig
        {
            private Zen2 cpu;
            private string cfgName;
            private byte evt;
            public string GetConfigName() { return cfgName; }

            public DCFillSource(Zen2 amdCpu, string name, byte evt)
            {
                cpu = amdCpu;
                cfgName = name;
                this.evt = evt;
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.EnablePerformanceCounters();
                ulong dcAccess = GetPerfCtlValue(0x40, 0, true, true, false, false, true, false, 0, 0, false, false);

                // include DcPrefetcher in miss, for LsMabAlloc
                ulong lsMabAlloc = GetPerfCtlValue(0x41, 0xB, true, true, false, false, true, false, 0, 0, false, false);
                ulong dcRefillFromL2 = GetPerfCtlValue(evt, 0x1, true, true, false, false, true, false, 0, 0, false, false);
                ulong dcRefillFromL3 = GetPerfCtlValue(evt, 0x12, true, true, false, false, true, false, 0, 0, false, false);
                ulong dcRefillFromDram = GetPerfCtlValue(evt, 0x48, true, true, false, false, true, false, 0, 0, false, false);
                ulong mabMatch = GetPerfCtlValue(0x55, 0, true, true, false, false, true, false, 0, 0, false, false);

                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    ThreadAffinity.Set(1UL << threadIdx);
                    Ring0.WriteMsr(MSR_PERF_CTL_0, dcAccess);
                    Ring0.WriteMsr(MSR_PERF_CTL_1, lsMabAlloc);
                    Ring0.WriteMsr(MSR_PERF_CTL_2, dcRefillFromL2);
                    Ring0.WriteMsr(MSR_PERF_CTL_3, dcRefillFromL3);
                    Ring0.WriteMsr(MSR_PERF_CTL_4, dcRefillFromDram);
                    Ring0.WriteMsr(MSR_PERF_CTL_5, mabMatch);
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

                results.overallCounterValues = cpu.GetOverallCounterValues("DC Access", "LsMabAlloc", "DC Refill From L2", "DC Refill From L3", "DC Refill From DRAM", "MAB Match");
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "L1D Hitrate", "L1D Hit BW?", "MAB Match", "L1D MPKI", "L2 Refill BW", 
                "Fill from L2/Ki", "L2 Hitrate", "L3 Refill BW", "Fill from L3/Ki", "L3 Hitrate", "DRAM Refill BW", "Fill from DRAM/Ki", };

            public string GetHelpText()
            {
                return "L1D Hitrate/BW - (data cache access - miss address buffer allocations) is used to count hits\n" +
                    "That means only 1 miss is counted per cache line.\n" + "Subsequent misses to the same 64B cache line are counted as hits\n" +
                    "L2 refill bw - demand refills from local L2 * 64B\n" + 
                    "L3 refill bw - demand refills from local or remote L3 * 64B\n" +
                    "DRAM refill bw - demand refills from local or remote DRAM * 64B";
            }

            public string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float dcAccess = counterData.ctr0;
                float lsMabAlloc = counterData.ctr1;
                float dcRefillFromL2 = counterData.ctr2;
                float dcRefillFromL3 = counterData.ctr3;
                float dcRefillFromDram = counterData.ctr4;
                float mabMatch = counterData.ctr5;
                float dcHitrate = (1 - (lsMabAlloc + mabMatch) / dcAccess) * 100;
                float dcHitBw = (dcAccess - lsMabAlloc) * 8; // "each increment represents an eight byte access"
                float l2RefillBw = dcRefillFromL2 * 64;
                float l3RefillBw = dcRefillFromL3 * 64;
                float dramRefillBw = dcRefillFromDram * 64;
                return new string[] { label,
                    FormatLargeNumber(counterData.aperf),
                    FormatLargeNumber(counterData.instr),
                    string.Format("{0:F2}", counterData.instr / counterData.aperf),
                    string.Format("{0:F2}%", dcHitrate),
                    FormatLargeNumber(dcHitBw) + "B/s",
                    FormatPercentage(mabMatch, dcAccess),
                    string.Format("{0:F2}", 1000 * lsMabAlloc / counterData.instr),
                    FormatLargeNumber(l2RefillBw) + "B/s",
                    string.Format("{0:F2}", 1000 * dcRefillFromL2 / counterData.instr),
                    FormatPercentage(dcRefillFromL2, dcRefillFromL2 + dcRefillFromL3 + dcRefillFromDram),
                    FormatLargeNumber(l3RefillBw) + "B/s",
                    string.Format("{0:F2}", 1000 * dcRefillFromL3 / counterData.instr),
                    FormatPercentage(dcRefillFromL3, dcRefillFromL3 + dcRefillFromDram),
                    FormatLargeNumber(dramRefillBw) + "B/s",
                    string.Format("{0:F2}", 1000 * dcRefillFromDram / counterData.instr),
                };
            }
        }

        public class DCFillLatencyConfig : MonitoringConfig
        {
            private Zen2 cpu;
            public string GetConfigName() { return "L1D Fill Latency"; }

            public DCFillLatencyConfig(Zen2 amdCpu)
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
                ulong dcHwPrefetch = GetPerfCtlValue(0x5A, 0x5B, true, true, false, false, true, false, 0, 0, false, false);

                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    ThreadAffinity.Set(1UL << threadIdx);
                    // dc access
                    Ring0.WriteMsr(MSR_PERF_CTL_0, GetPerfCtlValue(0x40, 0, true, true, false, false, true, false, 0, 0, false, false));
                    // lsMabAlloc
                    Ring0.WriteMsr(MSR_PERF_CTL_1, GetPerfCtlValue(0x41, 0b1011, true, true, false, false, true, false, 0, 0, false, false));
                    // allocated MABs
                    Ring0.WriteMsr(MSR_PERF_CTL_2, GetPerfCtlValue(0x5F, 0, true, true, false, false, true, false, 0, 0, false, false));
                    // (merge)
                    Ring0.WriteMsr(MSR_PERF_CTL_3, GetPerfCtlValue(0xFF, 0, false, false, false, false, true, false, 0, 0xF, false, false));
                    // mab match
                    Ring0.WriteMsr(MSR_PERF_CTL_4, GetPerfCtlValue(0x55, 0, true, true, false, false, true, false, 0, 0, false, false));
                    Ring0.WriteMsr(MSR_PERF_CTL_5, dcHwPrefetch);
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
                    results.unitMetrics[threadIdx] = computeMetrics("Thread " + threadIdx, cpu.NormalizedThreadCounts[threadIdx], false);
                }

                results.overallCounterValues = cpu.GetOverallCounterValues("DC Access", "LsMabAlloc", "Allocated MABs", "(merge)", "MAB Match", "DC HW Prefetch");
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts, true);
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "L1D Hitrate", "MAB Hit %", "MABs Allocated", "L1D Fill Latency", "L1D Fill Latency (ns)" };

            public string GetHelpText()
            {
                return "";
            }

            public string[] computeMetrics(string label, NormalizedCoreCounterData counterData, bool total)
            {
                float clk = (total ? counterData.tsc / cpu.threadCount : counterData.tsc) * counterData.aperf / counterData.mperf;
                float cycleTime = 1e9f / clk;
                float l1dFillLatencyClks = counterData.ctr2 / counterData.ctr1;
                return new string[] { label,
                    FormatLargeNumber(counterData.aperf),
                    FormatLargeNumber(counterData.instr),
                    string.Format("{0:F2}", counterData.instr / counterData.aperf),
                    string.Format("{0:F2}%", 100 * (1 - (counterData.ctr1 + counterData.ctr4) / counterData.ctr0)), // L1D hitrate, counting MAB hit as L1D miss
                    string.Format("{0:F2}%", 100 * counterData.ctr4 / counterData.ctr0), // MAB hit %
                    string.Format("{0:F2}", counterData.ctr2 / counterData.aperf), // MABs allocated per cycle
                    string.Format("{0:F2} clk", l1dFillLatencyClks),
                    string.Format("{0:F2} ns", l1dFillLatencyClks * cycleTime)
                };
            }
        }

        public class MABOccupancyConfig : MonitoringConfig
        {
            private Zen2 cpu;
            public string GetConfigName() { return "MAB Occupancy"; }

            public MABOccupancyConfig(Zen2 amdCpu)
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
                ulong dcHwPrefetch = GetPerfCtlValue(0x5A, 0x5B, true, true, false, false, true, false, 0, 0, false, false);

                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    ThreadAffinity.Set(1UL << threadIdx);
                    // MABs allocated + merge
                    Ring0.WriteMsr(MSR_PERF_CTL_0, GetPerfCtlValue(0x5F, 0, true, true, false, false, true, false, 0, 0, false, false));
                    // (merge)
                    Ring0.WriteMsr(MSR_PERF_CTL_1, GetPerfCtlValue(0xFF, 0, false, false, false, false, true, false, 0, 0xF, false, false));
                    // >=11 allocated MABs + merge
                    Ring0.WriteMsr(MSR_PERF_CTL_2, GetPerfCtlValue(0x5F, 0, true, true, false, false, true, false, cmask: 13, 0, false, false));
                    Ring0.WriteMsr(MSR_PERF_CTL_3, GetPerfCtlValue(0xFF, 0, false, false, false, false, true, false, 0, 0xF, false, false));
                    // >= 16 allocated MABs + merge
                    Ring0.WriteMsr(MSR_PERF_CTL_4, GetPerfCtlValue(0x5F, 0, true, true, false, false, true, false, cmask: 17, 0, false, false));
                    Ring0.WriteMsr(MSR_PERF_CTL_5, GetPerfCtlValue(0xFF, 0, false, false, false, false, true, false, 0, 0xF, false, false));
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
                    results.unitMetrics[threadIdx] = computeMetrics("Thread " + threadIdx, cpu.NormalizedThreadCounts[threadIdx], false);
                }

                results.overallCounterValues = cpu.GetOverallCounterValues("MABs allocated", "(merge)", ">12 MABs", "(merge)", ">=16 MABs", "(merge)");
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts, true);
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "MAB allocated", ">12 MABs", ">=16 MABs" };

            public string GetHelpText()
            {
                return "";
            }

            public string[] computeMetrics(string label, NormalizedCoreCounterData counterData, bool total)
            {
                return new string[] { label,
                    FormatLargeNumber(counterData.aperf),
                    FormatLargeNumber(counterData.instr),
                    string.Format("{0:F2}", counterData.instr / counterData.aperf),
                    string.Format("{0:F2}", counterData.ctr0 / counterData.aperf),
                    string.Format("{0:F2}%", 100 * counterData.ctr2 / counterData.aperf),
                    string.Format("{0:F2}%", 100 * counterData.ctr4 / counterData.aperf),
                };
            }
        }

        public class DCBWMonitoringConfig : MonitoringConfig
        {
            private Zen2 cpu;
            public string GetConfigName() { return "L1D BW"; }

            public DCBWMonitoringConfig(Zen2 amdCpu)
            {
                cpu = amdCpu;
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.ProgramPerfCounters(GetPerfCtlValue(0x40, 0, true, true, false, false, true, false, cmask: 1, 0, false, false), // dc access cmask 1
                    GetPerfCtlValue(0x40, 0, true, true, false, false, true, false, cmask: 2, 0, false, false),  // dc access cmask 2
                    GetPerfCtlValue(0x40, 0, true, true, false, false, true, false, cmask: 3, 0, false, false),  // dc access cmask 3
                    GetPerfCtlValue(0x29, 0b11, true, true, false, false, true, false, cmask: 0, 0, false, false),  // ls dispatch cmask 1
                    GetPerfCtlValue(0xAF, 0x10, true, true, false, false, true, false, cmask: 0, 0, false, false),  // agsq full
                    GetPerfCtlValue(0xAE, 0x2, true, true, false, false, true, false, cmask: 0, 0, false, false)); // ldq full
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
                results.overallCounterValues = cpu.GetOverallCounterValues("DC Access Cmask 1", "DC Access Cmask 2", "DC Access Cmask 3", "LS Dispatch", "AGSQ Full", "LDQ Full");
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                return results;
            }

            public string[] columns = new string[] { "Item", "Power", "Active Cycles", "Instructions", "IPC", "Instr/Watt", 
                "L1D Lookups", "L1D Active", "L1D 2 Access", "L1D 3 Access", "LS Dispatches", "L1D Accesses/LS Dispatch", "AGSQ Full", "LDQ Full" };

            public string GetHelpText()
            {
                return "cmask on LS/L1 events";
            }

            public string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float dc1Access = counterData.ctr0 - counterData.ctr1; // cycles with 1 dc access
                float dc2Access = counterData.ctr1 - counterData.ctr2;
                float dc3Access = counterData.ctr2;
                float dcAccess = dc1Access + 2 * dc2Access + 3 * dc3Access;
                float lsDispatch = counterData.ctr3;
                return new string[] { label,
                    string.Format("{0:F2} W", counterData.watts),
                    FormatLargeNumber(counterData.aperf),
                    FormatLargeNumber(counterData.instr),
                    string.Format("{0:F2}", counterData.instr / counterData.aperf),
                    FormatLargeNumber(counterData.instr / counterData.watts),
                    FormatLargeNumber(dcAccess),
                    FormatPercentage(counterData.ctr0, counterData.aperf),
                    FormatPercentage(dc2Access, counterData.aperf),
                    FormatPercentage(dc3Access, counterData.aperf),
                    FormatLargeNumber(lsDispatch),
                    string.Format("{0:F2}", dcAccess / lsDispatch),
                    FormatPercentage(counterData.ctr4, counterData.aperf),
                    FormatPercentage(counterData.ctr5, counterData.aperf),
                    };
            }
        }

        public class ICMonitoringConfig : MonitoringConfig
        {
            private Zen2 cpu;
            public string GetConfigName() { return "Instruction Fetch"; }

            public ICMonitoringConfig(Zen2 amdCpu)
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
                ulong l2CodeRequests = GetPerfCtlValue(0x64, 0x7, true, true, false, false, true, false, 0, 0, false, false);
                ulong itlbHit = GetPerfCtlValue(0x94, 0x7, true, true, false, false, true, false, 0, 0, false, false);
                ulong l2ItlbHit = GetPerfCtlValue(0x84, 0, true, true, false, false, true, false, 0, 0, false, false);
                ulong l2ItlbMiss = GetPerfCtlValue(0x85, 0x7, true, true, false, false, true, false, 0, 0, false, false);
                ulong l2IcRefill = GetPerfCtlValue(0x82, 0, true, true, false, false, true, false, 0, 0, false, false);
                ulong sysIcRefill = GetPerfCtlValue(0x83, 0, true, true, false, false, true, false, 0, 0, false, false);

                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    ThreadAffinity.Set(1UL << threadIdx);
                    Ring0.WriteMsr(MSR_PERF_CTL_0, l2CodeRequests);
                    Ring0.WriteMsr(MSR_PERF_CTL_1, itlbHit);
                    Ring0.WriteMsr(MSR_PERF_CTL_2, l2ItlbHit);
                    Ring0.WriteMsr(MSR_PERF_CTL_3, l2ItlbMiss);
                    Ring0.WriteMsr(MSR_PERF_CTL_4, l2IcRefill);
                    Ring0.WriteMsr(MSR_PERF_CTL_5, sysIcRefill);
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
                results.overallCounterValues = cpu.GetOverallCounterValues("L2 Code Requests", "ITLB Hit", "ITLB Miss L2 ITLB Hit", "L2 ITLB Miss", "IC Refill From L2", "IC Refill From System");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "L1i Hitrate", "L1i MPKI", "ITLB Hitrate", "ITLB MPKI", "L2 ITLB Hitrate", "L2 ITLB MPKI", "L2->L1i BW", "Sys->L1i BW", "L1i Misses" };

            public string GetHelpText()
            {
                return "Instruction cache misses are bad and way harder to hide than data cache misses";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float l2CodeRequests = counterData.ctr0;
                float itlbHits = counterData.ctr1;
                float itlbMpki = (counterData.ctr2 + counterData.ctr3) / counterData.instr * 100;
                float l2ItlbHits = counterData.ctr2;
                float l2ItlbMisses = counterData.ctr3;
                float l2IcRefills = counterData.ctr4;
                float sysIcRefills = counterData.ctr5;
                float icHitrate = (1 - l2CodeRequests / (itlbHits + l2ItlbHits + l2ItlbMisses)) * 100;
                float icMpki = l2CodeRequests / counterData.instr * 1000;
                float itlbHitrate = itlbHits / (itlbHits + l2ItlbHits + l2ItlbMisses) * 100;
                float l2ItlbHitrate = l2ItlbHits / (l2ItlbHits + l2ItlbMisses) * 100;
                float l2ItlbMpki = l2ItlbMisses / counterData.instr * 1000;
                float l2RefillBw = l2IcRefills * 64;
                float sysRefillBw = sysIcRefills * 64;
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        string.Format("{0:F2}%", icHitrate),
                        string.Format("{0:F2}", icMpki),
                        string.Format("{0:F2}%", itlbHitrate),
                        string.Format("{0:F2}", itlbMpki),
                        string.Format("{0:F2}%", l2ItlbHitrate),
                        string.Format("{0:F2}", l2ItlbMpki),
                        FormatLargeNumber(l2RefillBw) + "B/s",
                        FormatLargeNumber(sysRefillBw) + "B/s",
                        FormatLargeNumber(l2CodeRequests)
                };
            }
        }

        public class BPUMonitoringConfig1 : MonitoringConfig
        {
            private Zen2 cpu;
            public string GetConfigName() { return "Branch Prediction 1"; }

            public BPUMonitoringConfig1(Zen2 amdCpu)
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
                    // retired branches
                    Ring0.WriteMsr(MSR_PERF_CTL_0, GetPerfCtlValue(0xC2, 0, true, true, false, false, true, false, 0, 0, false, false));
                    // retired taken branches
                    Ring0.WriteMsr(MSR_PERF_CTL_1, GetPerfCtlValue(0xC4, 0, true, true, false, false, true, false, 0, 0, false, false));
                    // dynamic indirect predictions
                    Ring0.WriteMsr(MSR_PERF_CTL_2, GetPerfCtlValue(0x8E, 0, true, true, false, false, true, false, 0, 0, false, false));
                    // retired mispredicted indirect branches
                    Ring0.WriteMsr(MSR_PERF_CTL_3, GetPerfCtlValue(0xCA, 0, true, true, false, false, true, false, 0, 0, false, false));
                    // retired near returns
                    Ring0.WriteMsr(MSR_PERF_CTL_4, GetPerfCtlValue(0xC8, 0, true, true, false, false, true, false, 0, 0, false, false));
                    // misp near returns
                    Ring0.WriteMsr(MSR_PERF_CTL_5, GetPerfCtlValue(0xC9, 0, true, true, false, false, true, false, 0, 0, false, false));
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

                results.overallCounterValues = cpu.GetOverallCounterValues("Retired Branches", "Retired Taken Branches", "Dynamic Indirect Predictions", "Retired Mispredicted Indirect Branches", "Retired Near Returns", "Retired Near Returns Mispredicted");
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "% Branches", "% Branches Taken", "ITA Overhead", "Indirect Branch MPKI", "Indirect Predict Accuracy", "RET Predict Accuracy", "RET MPKI" };

            public string GetHelpText() 
            { 
                return "Taken branches reduce frontend bandwidth\n" + 
                    "Indirect predictions have L2 BTB override latency\n" + 
                    "Returns use a 32-deep (or 2x15 with SMT) return stack. Return prediction should be really accurate\n"+
                    "unless you have crazy stuff like mismatched call/ret or lots of nested calls...like recursion"; 
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf) + "/s",
                        FormatLargeNumber(counterData.instr) + "/s",
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        string.Format("{0:F2}%", counterData.ctr0 / counterData.instr * 100),
                        string.Format("{0:F2}%", counterData.ctr1 / counterData.ctr0 * 100),
                        string.Format("{0:F2}", FormatPercentage(4 * counterData.ctr2, counterData.aperf)),
                        string.Format("{0:F2}", counterData.ctr3 / counterData.instr * 1000),
                        string.Format("{0:F2}", FormatPercentage(counterData.ctr3, counterData.ctr2)),
                        string.Format("{0:F2}%", (1 - counterData.ctr5 / counterData.ctr4) * 100),
                        string.Format("{0:F2}", 1000 * counterData.ctr5 / counterData.instr)
                };
            }
        }

        public class PageWalkConfig : MonitoringConfig
        {
            private Zen2 cpu;
            public string GetConfigName() { return "Page Walk"; }

            public PageWalkConfig(Zen2 amdCpu)
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
                    // tablewalk in progress, i-side 0
                    Ring0.WriteMsr(MSR_PERF_CTL_0, GetPerfCtlValue(0x4E, 0x2, true, true, false, false, true, false, 0, 0, false, false));
                    // tablewalk in progress, i-side 1
                    Ring0.WriteMsr(MSR_PERF_CTL_1, GetPerfCtlValue(0x4E, 0x8, true, true, false, false, true, false, 0, 0, false, false));
                    // tablewalk in progress, d-side 0
                    Ring0.WriteMsr(MSR_PERF_CTL_2, GetPerfCtlValue(0x4E, 0x1, true, true, false, false, true, false, 0, 0, false, false));
                    // tablewalk in progress, d-side 1
                    Ring0.WriteMsr(MSR_PERF_CTL_3, GetPerfCtlValue(0x4E, 0x4, true, true, false, false, true, false, 0, 0, false, false));
                    // total d-side walks
                    Ring0.WriteMsr(MSR_PERF_CTL_4, GetPerfCtlValue(0x46, 0x3, true, true, false, false, true, false, 0, 0, false, false));
                    // total i-side walks
                    Ring0.WriteMsr(MSR_PERF_CTL_5, GetPerfCtlValue(0x46, 0b1100, true, true, false, false, true, false, 0, 0, false, false));
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

                results.overallCounterValues = cpu.GetOverallCounterValues("i-side 0 walk in progress", "i-side 1 walk in progress", "d-side 0 walk in progress", "d-side 1 walk in progress", "d-side walks", "i-side walks");
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "Instr Tablewalker 0 Busy", "Instr Tablewalker 1 Busy", "Data Tablewalker 0 Busy", "Data Tablewalker 1 Busy", "Instr Walk Duration", "Data Walk Duration", "Instr Walk/Ki", "Data Walk/Ki" };

            public string GetHelpText() 
            { 
                return ""; 
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                // PPR says to count all this (incl coalesced page hit) as DTLB miss?
                float dtlbMiss = counterData.ctr1 + counterData.ctr2 + counterData.ctr3 + counterData.ctr4;
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf) + "/s",
                        FormatLargeNumber(counterData.instr) + "/s",
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        FormatPercentage(counterData.ctr0, counterData.aperf),
                        FormatPercentage(counterData.ctr1, counterData.aperf),
                        FormatPercentage(counterData.ctr2, counterData.aperf),
                        FormatPercentage(counterData.ctr3, counterData.aperf),
                        string.Format("{0:F2} clks", (counterData.ctr0 + counterData.ctr1) / counterData.ctr5),
                        string.Format("{0:F2} clks", (counterData.ctr2 + counterData.ctr3) / counterData.ctr4),
                        string.Format("{0:F2}", counterData.ctr5 * 1000 / counterData.instr),
                        string.Format("{0:F2}", counterData.ctr4 * 1000 / counterData.instr)
                };
            }
        }

        public class DtlbConfig : MonitoringConfig
        {
            private Zen2 cpu;
            public string GetConfigName() { return "DTLB"; }

            public DtlbConfig(Zen2 amdCpu)
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
                    // dc access
                    Ring0.WriteMsr(MSR_PERF_CTL_0, GetPerfCtlValue(0x40, 0, true, true, false, false, true, false, 0, 0, false, false));
                    // L1 dtlb miss, L2 tlb hit (4k, 2m, or 1g)
                    Ring0.WriteMsr(MSR_PERF_CTL_1, GetPerfCtlValue(0x45, 0b1101, true, true, false, false, true, false, 0, 0, false, false));
                    // L1 dtlb miss, l2 tlb miss
                    Ring0.WriteMsr(MSR_PERF_CTL_2, GetPerfCtlValue(0x45, 0b11010000, true, true, false, false, true, false, 0, 0, false, false));
                    // l1 dtlb miss, coalesced page hit (why is this counted under the miss event?)
                    Ring0.WriteMsr(MSR_PERF_CTL_3, GetPerfCtlValue(0x45, 0b10, true, true, false, false, true, false, 0, 0, false, false));
                    // l1 dtlb miss, coalesced page miss
                    Ring0.WriteMsr(MSR_PERF_CTL_4, GetPerfCtlValue(0x45, 0b100000, true, true, false, false, true, false, 0, 0, false, false));
                    // tlb flush
                    Ring0.WriteMsr(MSR_PERF_CTL_5, GetPerfCtlValue(0x78, 0xFF, true, true, false, false, true, false, 0, 0, false, false));
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

                results.overallCounterValues = cpu.GetOverallCounterValues("DC Access", "DTLB Miss STLB Hit", "STLB Miss", "Coalesced Page Hit", "Coalesced Page Miss", "TLB Flush");
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "DC Access", "DTLB Hitrate", "DTLB MPKI", "L2 TLB Hitrate", "L2 TLB MPKI", "Coalesced page hit", "Coalesced page miss", "TLB Flush", "Data Page Walk" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                // PPR says to count all this (incl coalesced page hit) as DTLB miss?
                float dtlbMiss = counterData.ctr1 + counterData.ctr2 + counterData.ctr3 + counterData.ctr4;
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf) + "/s",
                        FormatLargeNumber(counterData.instr) + "/s",
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        FormatLargeNumber(counterData.ctr0),
                        string.Format("{0:F2}%", (1 - dtlbMiss / counterData.ctr0) * 100),
                        string.Format("{0:F2}", dtlbMiss / counterData.instr * 1000),
                        string.Format("{0:F2}%", counterData.ctr1 / dtlbMiss * 100),
                        string.Format("{0:F2}", counterData.ctr2 / counterData.instr * 1000),
                        FormatLargeNumber(counterData.ctr3),
                        FormatLargeNumber(counterData.ctr4),
                        FormatLargeNumber(counterData.ctr5),
                        FormatLargeNumber(counterData.ctr4 + counterData.ctr2)
                };
            }
        }

        public class TestConfig : MonitoringConfig
        {
            private Zen2 cpu;
            public string GetConfigName() { return "Testing"; }

            public TestConfig(Zen2 amdCpu)
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
                    // zen 1 fpu pipe assignment, pipe 0
                    Ring0.WriteMsr(MSR_PERF_CTL_0, GetPerfCtlValue(0x0, 0x1, true, true, false, false, true, false, 0, 0, false, false));
                    // pipe 1
                    Ring0.WriteMsr(MSR_PERF_CTL_1, GetPerfCtlValue(0x0, 0x2, true, true, false, false, true, false, 0, 0, false, false));
                    // pipe 2
                    Ring0.WriteMsr(MSR_PERF_CTL_2, GetPerfCtlValue(0x0, 0x4, true, true, false, false, true, false, 0, 0, false, false));
                    // pipe 3
                    Ring0.WriteMsr(MSR_PERF_CTL_3, GetPerfCtlValue(0x0, 0x8, true, true, false, false, true, false, 0, 0, false, false));
                    // l2 requests, not counting bus locks, self modifying code, ic/dc sized read
                    Ring0.WriteMsr(MSR_PERF_CTL_4, GetPerfCtlValue(0x60, 0xFE, true, true, false, false, true, false, 0, 0, false, false));
                    // zen 1 l2 latency event
                    Ring0.WriteMsr(MSR_PERF_CTL_5, GetPerfCtlValue(0x62, 1, true, true, false, false, true, false, 0, 0, false, false));
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
                    results.unitMetrics[threadIdx] = computeMetrics("Thread " + threadIdx, cpu.NormalizedThreadCounts[threadIdx], false);
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts, true);
                results.overallCounterValues = cpu.GetOverallCounterValues("FP0", "FP1", "FP2", "FP3", "L2 Requests Group 1", "L2 Request Latency");
                return results;
            }

            public string[] columns = new string[] { "Item", "Clk", "TSC", "MPERF", "APERF", "Power", "Instr", "IPC", "Instr/Watt", "FP0 FMUL/AES", "FP1 FMUL/AES", "FP2 FADD/FStore", "FP3 FADD/CVT", "L2 Miss Latency?", "L2 Miss Latency?", "L2 Pend Miss/C?"};

            public string GetHelpText() { return "FP pipe utilization events are for Zen 1, but not documented for Zen 2\nSame with L2 miss latency events"; }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData, bool total, float pwr = 0)
            {
                float l2MissLatency = counterData.ctr5 * 4 / counterData.ctr4;
                float coreClock = counterData.tsc * counterData.aperf / counterData.mperf;
                float watts = counterData.watts;
                if (total) coreClock = coreClock / cpu.GetThreadCount();
                return new string[] { label,
                        FormatLargeNumber(coreClock),
                        FormatLargeNumber(counterData.tsc),
                        FormatLargeNumber(counterData.mperf),
                        FormatLargeNumber(counterData.aperf),
                        string.Format("{0:F2} W", watts),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        FormatLargeNumber(counterData.instr / watts),
                        string.Format("{0:F2}%", 100 * counterData.ctr0 / counterData.aperf),
                        string.Format("{0:F2}%", 100 * counterData.ctr1 / counterData.aperf),
                        string.Format("{0:F2}%", 100 * counterData.ctr2 / counterData.aperf),
                        string.Format("{0:F2}%", 100 * counterData.ctr3 / counterData.aperf),
                        string.Format("{0:F2} clk", l2MissLatency),
                        string.Format("{0:F2} ns", (1000000000 / coreClock) * l2MissLatency),
                        total ? FormatLargeNumber(counterData.ctr5 * 4 / coreClock) : FormatLargeNumber(counterData.ctr5 * 4 / counterData.aperf) };
            }
        }

        public class L2Latency : MonitoringConfig
        {
            private Zen2 cpu;
            public string GetConfigName() { return "L2 Miss Latency"; }

            public L2Latency(Zen2 amdCpu)
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
                    // Zen 1 event 0x62 = L2 latency, divided by 4
                    ThreadAffinity.Set(1UL << threadIdx);
                    cpu.ProgramPerfCounters(GetPerfCtlValue(0x60, 0xFE, true, true, false, false, true, false, 0, 0, false, false),
                        GetPerfCtlValue(0x62, 1, true, true, false, false, true, false, 0, 0, false, false), // L2 miss latency
                        GetPerfCtlValue(0x62, 1, true, true, false, false, true, false, cmask: 1, 0, false, false), // L2 miss present
                        GetPerfCtlValue(0x62, 1, true, true, false, false, true, false, cmask: 2, 0, false, false), // >= 8 misses
                        GetPerfCtlValue(0x62, 1, true, true, false, false, true, false, cmask: 4, 0, false, false), // >= 16 misses
                        GetPerfCtlValue(0x62, 1, true, true, false, false, true, false, cmask: 8, 0, false, false)); // >= 32 misses
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
                }

                //float coreL2Misses = 0; // latency count is per core
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    // I suspect this is not per core, but per thread now
                    /*if (cpu.threadCount == cpu.coreCount)
                    {*/
                        results.unitMetrics[threadIdx] = computeMetrics("Thread " + threadIdx, cpu.NormalizedThreadCounts[threadIdx], false, cpu.NormalizedThreadCounts[threadIdx].ctr0);
                    //}
                    /*else
                    {
                        // with SMT we should have an even thread count, but just in case have a boundary check
                        if (threadIdx % 2 == 0 && threadIdx < cpu.GetThreadCount() - 1)
                        {
                            coreL2Misses = cpu.NormalizedThreadCounts[threadIdx].ctr0 + cpu.NormalizedThreadCounts[threadIdx + 1].ctr0;
                        }

                        results.unitMetrics[threadIdx] = computeMetrics("Thread " + threadIdx, cpu.NormalizedThreadCounts[threadIdx], false, coreL2Misses);
                    }*/
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts, true, cpu.NormalizedTotalCounts.ctr0);
                results.overallCounterValues = cpu.GetOverallCounterValues("L2 Requests Group 1", "L2 Req Latency", "L2 Latency Cmask 1", "L2 Latency Cmask 2", "L2 Latency Cmask 4", "L2 Latency Cmask 8");
                return results;
            }

            public string[] columns = new string[] { "Item", "Clk", "APERF", "Power", "Instr", "IPC", "Instr/Watt", "L2 Misses", "Miss Latency", "cmask 1", "cmask 2", "cmask 4", "cmask 8", "L2 Miss Latency?", "L2 Miss Latency?", "L2 Pend Miss/C?" };

            public string GetHelpText() { return "FP pipe utilization events are for Zen 1, but not documented for Zen 2\nSame with L2 miss latency events"; }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData, bool total, float coreL2Misses)
            {
                float l2MissLatency = counterData.ctr1 * 4 / coreL2Misses;
                float coreClock = counterData.tsc * counterData.aperf / counterData.mperf;
                float watts = counterData.watts;
                if (total) coreClock = coreClock / cpu.GetThreadCount();
                return new string[] { label,
                        FormatLargeNumber(coreClock),
                        FormatLargeNumber(counterData.aperf),
                        string.Format("{0:F2} W", watts),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        FormatLargeNumber(counterData.instr / watts),
                        FormatLargeNumber(coreL2Misses),
                        FormatLargeNumber(counterData.ctr1),
                        FormatPercentage(counterData.ctr2, counterData.aperf),
                        FormatPercentage(counterData.ctr3, counterData.aperf),
                        FormatPercentage(counterData.ctr4, counterData.aperf),
                        FormatPercentage(counterData.ctr5, counterData.aperf),
                        string.Format("{0:F2} clk", l2MissLatency),
                        string.Format("{0:F2} ns", (1000000000 / coreClock) * l2MissLatency),
                        total ? FormatLargeNumber(counterData.ctr1 * 4 / coreClock) : FormatLargeNumber(counterData.ctr1 * 4 / coreClock) };
            }
        }

        public class MiscConfig : MonitoringConfig
        {
            private Zen2 cpu;
            public string GetConfigName() { return "Misc"; }

            public MiscConfig(Zen2 amdCpu)
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
                    // WCB Full
                    Ring0.WriteMsr(MSR_PERF_CTL_0, GetPerfCtlValue(0x37, 1, true, true, false, false, true, false, cmask: 0, 0, false, false));
                    // Interrupts Taken
                    Ring0.WriteMsr(MSR_PERF_CTL_1, GetPerfCtlValue(0x2C, 0, true, true, false, false, true, false, cmask: 0, 0, false, false));
                    // Far control transfers
                    Ring0.WriteMsr(MSR_PERF_CTL_2, GetPerfCtlValue(0xC6, 0, true, true, false, false, true, false, cmask: 0, 0, false, false));
                    // Divider busy cycles
                    Ring0.WriteMsr(MSR_PERF_CTL_3, GetPerfCtlValue(0xD3, 0, true, true, false, false, true, false, 0, 0, false, false));
                    // Divider ops
                    Ring0.WriteMsr(MSR_PERF_CTL_4, GetPerfCtlValue(0xD4, 0, true, true, false, false, true, false, cmask: 0, 0, false, false));
                    // rdtsc
                    Ring0.WriteMsr(MSR_PERF_CTL_5, GetPerfCtlValue(0x2D, 0, true, true, false, false, true, false, cmask: 0, 0, false, false));
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
                    results.unitMetrics[threadIdx] = computeMetrics("Thread " + threadIdx, cpu.NormalizedThreadCounts[threadIdx], false);
                }

                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts, true, cpu.ReadPackagePowerCounter());
                results.overallCounterValues = cpu.GetOverallCounterValues("WCB Full Store Commit Cancel", "Interrupts", "Far Control Transfer", "Divider Busy Cycles", "Divider Ops", "TSC Read");
                return results;
            }

            public string[] columns = new string[] { "Item", "Clk", "TSC", "MPERF", "APERF", "Power", "Instr", "IPC", "Instr/Watt", "WCB Full/Ki", "WCB Full", "Interrupts/Ki", "Interrupts", "Far Calls/Ki", "Divder Busy", "Divides/Ki", "TSC Read/Ki", "TSC Reads" };

            public string GetHelpText() { return "Not brrrr"; }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData, bool total, float pwr = 0)
            {
                float coreClock = counterData.tsc * counterData.aperf / counterData.mperf;
                float watts = pwr == 0 ? counterData.watts : pwr;
                if (total) coreClock = coreClock / cpu.GetThreadCount();
                return new string[] { label,
                        FormatLargeNumber(coreClock),
                        FormatLargeNumber(counterData.tsc),
                        FormatLargeNumber(counterData.mperf),
                        FormatLargeNumber(counterData.aperf),
                        string.Format("{0:F2} W", watts),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        FormatLargeNumber(counterData.instr / watts),
                        string.Format("{0:F2}", 1000 * counterData.ctr0 / counterData.instr),
                        FormatLargeNumber(counterData.ctr0),
                        string.Format("{0:F2}", 1000 * counterData.ctr1 / counterData.instr),
                        FormatLargeNumber(counterData.ctr1),
                        string.Format("{0:F2}", 1000 * counterData.ctr2 / counterData.instr),
                        string.Format("{0:F2}%", 100 * counterData.ctr3 / counterData.aperf),
                        string.Format("{0:F2}", 1000 * counterData.ctr4 / counterData.instr),
                        string.Format("{0:F2}", 100 * counterData.ctr5 / counterData.instr),
                        FormatLargeNumber(counterData.ctr5)
                };
            }
        }

        public class RetireConfig : MonitoringConfig
        {
            private Zen2 cpu;
            public string GetConfigName() { return "Retire Histogram"; }

            public RetireConfig(Zen2 amdCpu)
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
                    // ret uops, cmask 1
                    Ring0.WriteMsr(MSR_PERF_CTL_0, GetPerfCtlValue(0xC1, 0, true, true, false, false, true, false, cmask: 1, 0, false, false));
                    // ret uops, cmask 2
                    Ring0.WriteMsr(MSR_PERF_CTL_1, GetPerfCtlValue(0xC1, 0, true, true, false, false, true, false, cmask: 2, 0, false, false));
                    // ^^ cmask 3
                    Ring0.WriteMsr(MSR_PERF_CTL_2, GetPerfCtlValue(0xC1, 0, true, true, false, false, true, false, cmask: 3, 0, false, false));
                    // ^^ cmask 4
                    Ring0.WriteMsr(MSR_PERF_CTL_3, GetPerfCtlValue(0xC1, 0, true, true, false, false, true, false, cmask: 4, 0, false, false));
                    // ^^ cmask 5
                    Ring0.WriteMsr(MSR_PERF_CTL_4, GetPerfCtlValue(0xC1, 0, true, true, false, false, true, false, cmask: 5, 0, false, false));
                    // ^^ cmask 6
                    Ring0.WriteMsr(MSR_PERF_CTL_5, GetPerfCtlValue(0xC1, 0, true, true, false, false, true, false, cmask: 6, 0, false, false));
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
                results.overallCounterValues = cpu.GetOverallCounterValues("Ret uops cmask 1", "Ret uops cmask 2", "Ret uops cmask 3", "ret uops cmask 4", "ret uops cmask 5", "ret uops cmask 6");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "Retire Stall", "1 op", "2 ops", "3 ops", "4 ops", "5 ops", ">5 ops" };

            public string GetHelpText() { return ""; }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf) + "/s",
                        FormatLargeNumber(counterData.instr) + "/s",
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        string.Format("{0:F2}%", 100 * (counterData.aperf - counterData.ctr0) / counterData.aperf),
                        string.Format("{0:F2}%", 100 * (counterData.ctr0 - counterData.ctr1) / counterData.ctr0),
                        string.Format("{0:F2}%", 100 * (counterData.ctr1 - counterData.ctr2) / counterData.ctr0),
                        string.Format("{0:F2}%", 100 * (counterData.ctr2 - counterData.ctr3) / counterData.ctr0),
                        string.Format("{0:F2}%", 100 * (counterData.ctr3 - counterData.ctr4) / counterData.ctr0),
                        string.Format("{0:F2}%", 100 * (counterData.ctr4 - counterData.ctr5) / counterData.ctr0),
                        string.Format("{0:F2}%", 100 * counterData.ctr5 / counterData.ctr0) };
            }
        }

        public class RetireBurstConfig : MonitoringConfig
        {
            private Zen2 cpu;
            public string GetConfigName() { return "Retire (Burst)"; }

            public RetireBurstConfig(Zen2 amdCpu)
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
                    // ret uops, cmask 1
                    Ring0.WriteMsr(MSR_PERF_CTL_0, GetPerfCtlValue(0xC1, 0, true, true, false, false, true, false, cmask: 1, 0, false, false));
                    // ret uops, cmask 8
                    Ring0.WriteMsr(MSR_PERF_CTL_1, GetPerfCtlValue(0xC1, 0, true, true, false, false, true, false, cmask: 8, 0, false, false));
                    // cmask 8, edge
                    Ring0.WriteMsr(MSR_PERF_CTL_2, GetPerfCtlValue(0xC1, 0, true, true, edge: true, false, true, false, cmask: 8, 0, false, false));
                    // cmask 1, edge
                    Ring0.WriteMsr(MSR_PERF_CTL_3, GetPerfCtlValue(0xC1, 0, true, true, edge: true, false, true, false, cmask: 1, 0, false, false));
                    // no ret uops
                    Ring0.WriteMsr(MSR_PERF_CTL_4, GetPerfCtlValue(0xC1, 0, true, true, false, false, true, invert: true, cmask: 1, 0, false, false));
                    // no ret uops, edge
                    Ring0.WriteMsr(MSR_PERF_CTL_5, GetPerfCtlValue(0xC1, 0, true, true, edge: true, false, true, invert: true, cmask: 1, 0, false, false));
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
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "Retire Stall", "retire active duration", "8 ops", "8 ops ret duration", "no uops cycles", "no uops duration" };

            public string GetHelpText() { return ""; }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf) + "/s",
                        FormatLargeNumber(counterData.instr) + "/s",
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        string.Format("{0:F2}%", 100 * (counterData.aperf - counterData.ctr0) / counterData.aperf),
                        string.Format("{0:F2}", counterData.ctr0 / counterData.ctr3),
                        string.Format("{0:F2}%", 100 * counterData.ctr2 / counterData.ctr0),
                        string.Format("{0:F2}", counterData.ctr1 / counterData.ctr2),
                        string.Format("{0:F2}%", 100 * counterData.ctr4 / counterData.aperf),
                        string.Format("{0:F2}", counterData.ctr4 / counterData.ctr5)
                };
            }
        }

        public class DecodeHistogram : MonitoringConfig
        {
            private Zen2 cpu;
            public string GetConfigName() { return "Decoder Histogram"; }

            public DecodeHistogram(Zen2 amdCpu)
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
                    // uops from decoder, cmask 1
                    Ring0.WriteMsr(MSR_PERF_CTL_0, GetPerfCtlValue(0xAA, 1, true, true, false, false, true, false, cmask: 1, 0, false, false));
                    // ^^ cmask 2
                    Ring0.WriteMsr(MSR_PERF_CTL_1, GetPerfCtlValue(0xAA, 1, true, true, false, false, true, false, cmask: 2, 0, false, false));
                    // ^^ cmask 3
                    Ring0.WriteMsr(MSR_PERF_CTL_2, GetPerfCtlValue(0xAA, 1, true, true, false, false, true, false, cmask: 3, 0, false, false));
                    // ^^ cmask 4
                    Ring0.WriteMsr(MSR_PERF_CTL_3, GetPerfCtlValue(0xAA, 1, true, true, false, false, true, false, cmask: 4, 0, false, false));
                    // op cache active
                    Ring0.WriteMsr(MSR_PERF_CTL_4, GetPerfCtlValue(0xAA, 2, true, true, false, false, true, false, cmask: 1 , 0, false, false));
                    // all uops from op cache
                    Ring0.WriteMsr(MSR_PERF_CTL_5, GetPerfCtlValue(0xAA, 2, true, true, false, false, true, false, cmask: 0, 0, false, false));
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
                results.overallCounterValues = cpu.GetOverallCounterValues("Decoder Ops cmask 1", "Decoder Ops cmask 2", "Decoder Ops cmask 3", "Decoder Ops cmask 4", "Op Cache cmask 1", "Op Cache Ops");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "OC Active", "Decoder Active", "1 op", "2 ops", "3 ops", "4 ops", "Decoder Ops/C", "Decoder Ops", "Op Cache Ops/C", "Op Cache Ops" };

            public string GetHelpText() { return "In theory the decoder can deliver >4 ops if instructions generate more than one op\nBut I guess that doesn't happen?"; }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float decoderOps = 4 * counterData.ctr3 + 3 * (counterData.ctr2 - counterData.ctr3) + 2 * (counterData.ctr1 - counterData.ctr2) + (counterData.ctr0 - counterData.ctr1);
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf) + "/s",
                        FormatLargeNumber(counterData.instr) + "/s",
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        string.Format("{0:F2}%", 100 * counterData.ctr4 / counterData.aperf),
                        string.Format("{0:F2}%", 100 * counterData.ctr0 / counterData.aperf),
                        string.Format("{0:F2}%", 100 * (counterData.ctr0 - counterData.ctr1) / counterData.ctr0),
                        string.Format("{0:F2}%", 100 * (counterData.ctr1 - counterData.ctr2) / counterData.ctr0),
                        string.Format("{0:F2}%", 100 * (counterData.ctr2 - counterData.ctr3) / counterData.ctr0),
                        string.Format("{0:F2}%", 100 * counterData.ctr3 / counterData.ctr0),
                        string.Format("{0:F2}",  decoderOps / counterData.ctr0),
                        FormatLargeNumber(decoderOps),
                        string.Format("{0:F2}", counterData.ctr5 / counterData.ctr4),
                        FormatLargeNumber(counterData.ctr5)
                };
            }
        }

        

        public class LSConfig : MonitoringConfig
        {
            private Zen2 cpu;
            public string GetConfigName() { return "Load/Store Unit"; }

            public LSConfig(Zen2 amdCpu)
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
                    // ls dispatch, loads/load-op-store
                    Ring0.WriteMsr(MSR_PERF_CTL_0, GetPerfCtlValue(0x29, 0b101, true, true, false, false, true, false, 0, 0, false, false));
                    // store to load forward
                    Ring0.WriteMsr(MSR_PERF_CTL_1, GetPerfCtlValue(0x35, 0, true, true, false, false, true, false, 0, 0, false, false));
                    // stlf fail, StilNoState = no DC hit / valid DC way for a forward
                    Ring0.WriteMsr(MSR_PERF_CTL_2, GetPerfCtlValue(0x24, 1, true, true, false, false, true, false, 0, 0, false, false));
                    // stlf fail, StilOther = partial overlap, non-cacheable store, etc.
                    Ring0.WriteMsr(MSR_PERF_CTL_3, GetPerfCtlValue(0x24, 2, true, true, false, false, true, false, 0, 0, false, false));
                    // stlf fail, StlfNoData = forwarding checks out but no store data
                    Ring0.WriteMsr(MSR_PERF_CTL_4, GetPerfCtlValue(0x24, 4, true, true, false, false, true, false, 0, 0, false, false));
                    // Misaligned loads
                    Ring0.WriteMsr(MSR_PERF_CTL_5, GetPerfCtlValue(0x47, 0, true, true, false, false, true, false, 0, 0, false, false));
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

                results.overallCounterValues = cpu.GetOverallCounterValues("LS Dispatch Load/LoadOpStore", "Store Forwarded", "StilNoState", "StilOther", "StilNoData", "Misaligned Loads");
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "Loads", "Store Forwarded", "StilNoState", "StilOther", "StlfNoData", "Misaligned Loads" };

            public string GetHelpText() 
            {
                return "Loads = loads and load-op-stores dispatched\n" +
                    "StilNoState = Store forwarding fail, no L1D hit and a L1D way\n" +
                    "StilOther = Store forwarding fail, other reasons like partial overlap or non-cacheable store\n" +
                    "StlfNoData = Store data not yet available for forwarding\n";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf) + "/s",
                        FormatLargeNumber(counterData.instr) + "/s",
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

        public class LSSwPrefetch : MonitoringConfig
        {
            private Zen2 cpu;
            public string GetConfigName() { return "Software Prefetch"; }

            public LSSwPrefetch(Zen2 amdCpu)
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
                    // software prefetches, all documented umask bits (prefetch, prefetchw, prefetchnta)
                    Ring0.WriteMsr(MSR_PERF_CTL_0, GetPerfCtlValue(0x4B, 0b111, true, true, false, false, true, false, 0, 0, false, false));
                    // ineffective sw prefetches, DataPipeSwPfDcHit
                    Ring0.WriteMsr(MSR_PERF_CTL_1, GetPerfCtlValue(0x52, 0b1, true, true, false, false, true, false, 0, 0, false, false));
                    // ineffective sw prefetches, MabMchCnt
                    Ring0.WriteMsr(MSR_PERF_CTL_2, GetPerfCtlValue(0x52, 0b10, true, true, false, false, true, false, 0, 0, false, false));
                    // sw prefetch L2 hit
                    Ring0.WriteMsr(MSR_PERF_CTL_3, GetPerfCtlValue(0x59, 0b1, true, true, false, false, true, false, 0, 0, false, false));
                    // sw prefetch, l3 hit
                    Ring0.WriteMsr(MSR_PERF_CTL_4, GetPerfCtlValue(0x59, 0x12, true, true, false, false, true, false, 0, 0, false, false));
                    // sw prefetch, dram
                    Ring0.WriteMsr(MSR_PERF_CTL_5, GetPerfCtlValue(0x59, 0x48, true, true, false, false, true, false, 0, 0, false, false));
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

                results.overallCounterValues = cpu.GetOverallCounterValues("Software Prefetches", "DataPipeSwPfDcHit", "MabMchCnt", "Sw Prefetch L2 Hit", "Sw Prefetch L3 Hit", "Sw Prefetch DRAM Hit");
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "Sw Prefetches", "% useless", "Useless SwPf, DC hit", "Useless SwPf, MAB Hit", "SwPf, L2 hit", "SwPf, L3 hit", "SwPf, DRAM" };

            public string GetHelpText()
            {
                return "Useless SwPf, DC hit - requested data already in L1D\n" +
                    "Useless SwPf, MAB hit - request for data already pending";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf) + "/s",
                        FormatLargeNumber(counterData.instr) + "/s",
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        FormatLargeNumber(counterData.ctr0),
                        string.Format("{0:F2}%", 100 * (counterData.ctr1 + counterData.ctr2) / counterData.ctr0),
                        FormatLargeNumber(counterData.ctr1),
                        FormatLargeNumber(counterData.ctr2),
                        FormatLargeNumber(counterData.ctr3),
                        FormatLargeNumber(counterData.ctr4),
                        FormatLargeNumber(counterData.ctr5)
                };
            }
        }

        public class PowerConfig : MonitoringConfig
        {
            private Zen2 cpu;
            public string GetConfigName() { return "Power Efficiency"; }

            public PowerConfig(Zen2 amdCpu)
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
                    // retired flops
                    Ring0.WriteMsr(MSR_PERF_CTL_0, GetPerfCtlValue(0x3, 0xF, true, true, false, false, true, false, 0, 0, false, false));
                    // merge
                    Ring0.WriteMsr(MSR_PERF_CTL_1, GetPerfCtlValue(0xFF, 0, false, false, false, false, true, false, 0, 0xF, false, false));
                    // retired uops
                    Ring0.WriteMsr(MSR_PERF_CTL_2, GetPerfCtlValue(0xC1, 0, true, true, false, false, true, false, 0, 0, false, false));
                    // retired mmx/fp
                    Ring0.WriteMsr(MSR_PERF_CTL_3, GetPerfCtlValue(0xCB, 0b111, true, true, false, false, true, false, 0, 0, false, false));
                    // dispatch stall 1
                    Ring0.WriteMsr(MSR_PERF_CTL_4, GetPerfCtlValue(0xAE, 0xFF, true, true, false, false, true, false, 0, 0, false, false));
                    // dispatch stall 2
                    Ring0.WriteMsr(MSR_PERF_CTL_5, GetPerfCtlValue(0xAF, 0x7F, true, true, false, false, true, false, 0, 0, false, false));
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
                    results.unitMetrics[threadIdx] = computeMetrics("Thread " + threadIdx, cpu.NormalizedThreadCounts[threadIdx], false);
                }

                results.overallMetrics = computeMetrics("Package", cpu.NormalizedTotalCounts, true, cpu.ReadPackagePowerCounter());
                results.overallCounterValues = cpu.GetOverallCounterValues("Retired Flops", "(merge)", "Retired Ops", "Retired MMX/FP", "Dispatch Stall 1", "Dispatch Stall 2");
                return results;
            }

            public string[] columns = new string[] { "Item", "Clk", "Power", "MPERF %", "Active Cycles", "Instructions", "IPC", "Instr/Watt", "Ops/C", "Ops/Watt", "FLOPS", "FLOPS/Watt", "MMX/FP Instr", "Dispatch Stall 1", "Dispatch Stall 2" };

            public string GetHelpText() 
            { 
                return "First row counts package power, not sum of core power\n" + 
                    "MPERF % - time spent at max performance state"; 
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData, bool total, float pwr = 0)
            {
                float coreClock = counterData.tsc * counterData.aperf / counterData.mperf;
                float watts = pwr == 0 ? counterData.watts : pwr;
                if (total) coreClock = coreClock / cpu.GetThreadCount();
                return new string[] { label,
                        FormatLargeNumber(coreClock),
                        string.Format("{0:F2} W", watts),
                        string.Format("{0:F1}%", 100 * counterData.mperf / counterData.tsc),
                        FormatLargeNumber(counterData.aperf),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.aperf),
                        FormatLargeNumber(counterData.instr / watts),
                        string.Format("{0:F2}", counterData.ctr2 / counterData.aperf),
                        FormatLargeNumber(counterData.ctr2 / watts),
                        FormatLargeNumber(counterData.ctr0),
                        FormatLargeNumber(counterData.ctr0 / watts),
                        FormatLargeNumber(counterData.ctr3),
                        string.Format("{0:F2}%", 100 * counterData.ctr4 / counterData.aperf),
                        string.Format("{0:F2}%", 100 * counterData.ctr5 / counterData.aperf)
                };
            }
        }

        public class Locks : MonitoringConfig
        {
            private Zen2 cpu;
            public string GetConfigName() { return "Locks"; }

            public Locks(Zen2 amdCpu)
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
                    // dc refill, umask bit 2 (0x4) from zen 3 ppr
                    Ring0.WriteMsr(MSR_PERF_CTL_0, GetPerfCtlValue(0x44, 0x4, true, true, false, false, true, false, 0, 0, false, false));
                    // sw prefetch, remote cache/dram
                    Ring0.WriteMsr(MSR_PERF_CTL_1, GetPerfCtlValue(0x59, 0x50, true, true, false, false, true, false, 0, 0, false, false));
                    // cacheable lock speculation succeeded (lo)
                    Ring0.WriteMsr(MSR_PERF_CTL_2, GetPerfCtlValue(0x25, 0b0100, true, true, false, false, true, false, 0, 0, false, false));
                    // cacheable lock speculation succeeded (high)
                    Ring0.WriteMsr(MSR_PERF_CTL_3, GetPerfCtlValue(0x25, 0b1000, true, true, false, false, true, false, 0, 0, false, false));
                    // non-speculative lock
                    Ring0.WriteMsr(MSR_PERF_CTL_4, GetPerfCtlValue(0x25, 0x2, true, true, false, false, true, false, 0, 0, false, false));
                    // bus lock
                    Ring0.WriteMsr(MSR_PERF_CTL_5, GetPerfCtlValue(0x25, 0x1, true, true, false, false, true, false, 0, 0, false, false));
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

                results.overallCounterValues = cpu.GetOverallCounterValues("(DC Fill ExtCacheLocal)", "SW Prefetch Remote Cache/DRAM", "SpecLockLo", "SpecLockHi", "NonSpecLock", "BusLock");
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "(DC Fill ExtCacheLocal)", "SW Prefetch, Remote", "SpecLockLo", "SpecLockHi", "NonSpecLock", "BusLock" };

            public string GetHelpText()
            {
                return "?";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf) + "/s",
                        FormatLargeNumber(counterData.instr) + "/s",
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

        public class FpDispFault : MonitoringConfig
        {
            private Zen2 cpu;
            public string GetConfigName() { return "FP Dispatch Faults"; }

            public FpDispFault(Zen2 amdCpu)
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
                    // ymm spill fault
                    Ring0.WriteMsr(MSR_PERF_CTL_0, GetPerfCtlValue(0xE, 0b1000, true, true, false, false, true, false, 0, 0, false, false));
                    // ymm fill fault
                    Ring0.WriteMsr(MSR_PERF_CTL_1, GetPerfCtlValue(0xE, 0b0100, true, true, false, false, true, false, 0, 0, false, false));
                    // xmm fill fault
                    Ring0.WriteMsr(MSR_PERF_CTL_2, GetPerfCtlValue(0xE, 0b0010, true, true, false, false, true, false, 0, 0, false, false));
                    // x87 fill fault
                    Ring0.WriteMsr(MSR_PERF_CTL_3, GetPerfCtlValue(0xE, 1, true, true, false, false, true, false, 0, 0, false, false));
                    // SSE serializing op
                    Ring0.WriteMsr(MSR_PERF_CTL_4, GetPerfCtlValue(0x5, 0b1100, true, true, false, false, true, false, 0, 0, false, false));
                    // x87 serializing op
                    Ring0.WriteMsr(MSR_PERF_CTL_5, GetPerfCtlValue(0x5, 0b0011, true, true, false, false, true, false, 0, 0, false, false));
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

                results.overallCounterValues = cpu.GetOverallCounterValues("YMM Spill Fault", "YMM Fill Fault", "XMM Fill Fault", "x87 Fill Fault", "SSE Serializing", "x87 Serializing");
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "YMM Spill Fault", "YMM Fill Fault", "XMM Fill Fault", "x87 Fill Fault", "SSE Serializing", "x87 Serializing" };

            public string GetHelpText()
            {
                return "?";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                return new string[] { label,
                        FormatLargeNumber(counterData.aperf) + "/s",
                        FormatLargeNumber(counterData.instr) + "/s",
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
    }
}

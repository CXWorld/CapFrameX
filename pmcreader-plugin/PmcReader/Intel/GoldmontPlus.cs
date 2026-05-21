using System.Collections.Generic;
using PmcReader.Interop;

namespace PmcReader.Intel
{
    public class GoldmontPlus : ModernIntelCpu
    {
        public GoldmontPlus()
        {
            List<MonitoringConfig> configs = new List<MonitoringConfig>();
            configs.Add(new ArchitecturalCounters(this));
            configs.Add(new BAClears(this));
            configs.Add(new IFetch(this));
            configs.Add(new Decode(this));
            configs.Add(new InstrPageWalk(this));
            configs.Add(new IssueHistogram(this));
            configs.Add(new BackendStall(this));
            configs.Add(new LSU(this));
            configs.Add(new MemMachineClear(this));
            configs.Add(new MachineClear1(this));
            configs.Add(new DTLB(this));
            configs.Add(new MemLoads(this));
            configs.Add(new RetireHistogram(this));
            configs.Add(new OffcoreL2(this));
            configs.Add(new OffcoreHitm(this));
            monitoringConfigs = configs.ToArray();
            architectureName = "Goldmont Plus";
        }

        /// <summary>
        /// What to put in Goldmont MSR_OFFCORE_RSPx MSRs
        /// </summary>
        /// <returns>Value to put in offcore response config MSR</returns>
        public static ulong GetGoldmontOffcoreRspRegisterValue(ushort reqType, ushort l2Hit, ushort l2Miss)
        {
            return (ulong)reqType |
                (ulong)l2Hit << 16 |
                (ulong)l2Miss << 31;
        }

        public class BAClears : MonitoringConfig
        {
            private GoldmontPlus cpu;
            public string GetConfigName() { return "BAClears"; }
            public string GetHelpText() { return ""; }

            public BAClears(GoldmontPlus intelCpu)
            {
                cpu = intelCpu;
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
                    ulong branches = GetPerfEvtSelRegisterValue(0xC4, 0, usr: true, os: true, edge: false, pc: false, interrupt: false, anyThread: false, enable: true, invert: false, cmask: 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL0, branches);

                    ulong baClears = GetPerfEvtSelRegisterValue(0xE6, 0x1, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL1, baClears);

                    ulong baClearsCond = GetPerfEvtSelRegisterValue(0xE6, 0x10, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL2, baClearsCond);

                    ulong baClearsReturn = GetPerfEvtSelRegisterValue(0xE6, 0x8, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL3, baClearsReturn);
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
                results.overallCounterValues = cpu.GetOverallCounterValues("Branches", "BAClears", "BAClears.Cond", "BAClears.Return");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "Pkg Pwr", "Instr/Watt", 
                "% Branches", "BAClears/Ki", "BAClears.Cond/Ki", "BAClears.Return/Ki", "BAClears/Branch" };

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatLargeNumber(counterData.instr / counterData.packagePower),
                        FormatPercentage(counterData.pmc[1], counterData.pmc[0]),
                        string.Format("{0:F2}", 1000 * counterData.pmc[1] / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.pmc[2] / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.pmc[3] / counterData.instr),
                        string.Format("{0:F2}", counterData.pmc[1] / counterData.pmc[0])
                };
            }
        }

        public class BadSpec : MonitoringConfig
        {
            private GoldmontPlus cpu;
            public string GetConfigName() { return "Bad Speculation"; }
            public string GetHelpText() { return ""; }

            public BadSpec(GoldmontPlus intelCpu)
            {
                cpu = intelCpu;
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
                    ulong uopsIssued = GetPerfEvtSelRegisterValue(0xE, 0, usr: true, os: true, edge: false, pc: false, interrupt: false, anyThread: false, enable: true, invert: false, cmask: 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL0, uopsIssued);

                    ulong uopsRetired = GetPerfEvtSelRegisterValue(0xC2, 0, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL1, uopsRetired);

                    ulong uopsNotDelivered = GetPerfEvtSelRegisterValue(0x9C, 0, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL2, uopsNotDelivered);

                    ulong baClearsReturn = GetPerfEvtSelRegisterValue(0xE6, 0x8, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL3, baClearsReturn);
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
                results.overallCounterValues = cpu.GetOverallCounterValues("Branches", "BAClears", "BAClears.Cond", "BAClears.Return");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "Pkg Pwr", "Instr/Watt",
                "% Branches", "BAClears/Ki", "BAClears.Cond/Ki", "BAClears.Return/Ki", "BAClears/Branch" };

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatLargeNumber(counterData.instr / counterData.packagePower),
                        FormatPercentage(counterData.pmc[1], counterData.pmc[0]),
                        string.Format("{0:F2}", 1000 * counterData.pmc[1] / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.pmc[2] / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.pmc[3] / counterData.instr),
                        string.Format("{0:F2}", counterData.pmc[1] / counterData.pmc[0])
                };
            }
        }

        public class IFetch : MonitoringConfig
        {
            private GoldmontPlus cpu;
            public string GetConfigName() { return "Instruction Fetch"; }
            public string GetHelpText() { return ""; }

            public IFetch(GoldmontPlus intelCpu)
            {
                cpu = intelCpu;
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
                    ulong icAccess = GetPerfEvtSelRegisterValue(0x80, 0x03, usr: true, os: true, edge: false, pc: false, interrupt: false, anyThread: false, enable: true, invert: false, cmask: 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL0, icAccess);

                    ulong icHit = GetPerfEvtSelRegisterValue(0x80, 0x01, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL1, icHit);

                    ulong fetchStall = GetPerfEvtSelRegisterValue(0x86, 0x00, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL2, fetchStall);

                    ulong itlbMiss = GetPerfEvtSelRegisterValue(0x81, 0x4, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL3, itlbMiss);
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
                results.overallCounterValues = cpu.GetOverallCounterValues("IC Access", "IC Hit", "IFetch Stall", "ITLB Miss");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "Pkg Pwr", "Instr/Watt", "L1i Hitrate", "L1i MPKI", "L1i Hit BW", "IFetch Stall", "ITLB MPKI" };

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatLargeNumber(counterData.instr / counterData.packagePower),
                        FormatPercentage(counterData.pmc[1], counterData.pmc[0]),
                        string.Format("{0:F2}", 1000 * ((counterData.pmc[0] - counterData.pmc[1]) / counterData.instr)),
                        FormatLargeNumber(counterData.pmc[1] * 64) + "B/s",
                        FormatPercentage(counterData.pmc[2], counterData.activeCycles),
                        string.Format("{0:F2}", 1000 * counterData.pmc[3] / counterData.instr),
                };
            }
        }

        public class InstrPageWalk : MonitoringConfig
        {
            private GoldmontPlus cpu;
            public string GetConfigName() { return "Instr Page Walk"; }
            public string GetHelpText() { return ""; }

            public InstrPageWalk(GoldmontPlus intelCpu)
            {
                cpu = intelCpu;
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
                    ulong itlbWalk1G = GetPerfEvtSelRegisterValue(0x85, 0x08, usr: true, os: true, edge: false, pc: false, interrupt: false, anyThread: false, enable: true, invert: false, cmask: 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL0, itlbWalk1G);

                    ulong itlbWalk2M = GetPerfEvtSelRegisterValue(0x85, 0x04, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL1, itlbWalk2M);

                    ulong itlbWalk4K = GetPerfEvtSelRegisterValue(0x85, 0x02, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL2, itlbWalk4K);

                    ulong itlbWalkPending = GetPerfEvtSelRegisterValue(0x85, 0x10, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL3, itlbWalkPending);
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
                results.overallCounterValues = cpu.GetOverallCounterValues("1G Walk Completed", "2/4M Walk Completed", "4K Walk Completed", "ITLB Walk Pending");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "Pkg Pwr", "Instr/Watt", 
                "ITLB Walk/Ki", "ITLB Walk Duration", "ITLB Walk Active", "1G Walk/Ki", "2M/4M Walk/Ki", "4K Walk/Ki" };

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float completedWalks = counterData.pmc[0] + counterData.pmc[1] + counterData.pmc[2];
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatLargeNumber(counterData.instr / counterData.packagePower),
                        string.Format("{0:F2}", 1000 * completedWalks / counterData.instr),
                        string.Format("{0:F2} clks", counterData.pmc[3] / completedWalks),
                        FormatPercentage(counterData.pmc[3], counterData.activeCycles),
                        string.Format("{0:F2}", 1000 * counterData.pmc[0] / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.pmc[1] / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.pmc[2] / counterData.instr)
                };
            }
        }

        public class DTLB : MonitoringConfig
        {
            private GoldmontPlus cpu;
            public string GetConfigName() { return "Data TLB"; }
            public string GetHelpText() { return ""; }

            public DTLB(GoldmontPlus intelCpu)
            {
                cpu = intelCpu;
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
                    ulong dtlbWalk1G2M = GetPerfEvtSelRegisterValue(0x49, 0x08 | 0x4, usr: true, os: true, edge: false, pc: false, interrupt: false, anyThread: false, enable: true, invert: false, cmask: 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL0, dtlbWalk1G2M);

                    ulong uTLBBlock = GetPerfEvtSelRegisterValue(0x3, 0x8, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL1, uTLBBlock);

                    ulong dtlbWalk4K = GetPerfEvtSelRegisterValue(0x49, 0x02, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL2, dtlbWalk4K);

                    ulong dtlbWalkPending = GetPerfEvtSelRegisterValue(0x49, 0x10, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL3, dtlbWalkPending);
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
                results.overallCounterValues = cpu.GetOverallCounterValues("2M/4M/1G Walk Completed", "uTLB Miss Load Block", "4K Walk Completed", "ITLB Walk Pending");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "Pkg Pwr", "Instr/Watt",
                "DTLB Walk/Ki", "DTLB Walk Duration", "DTLB Walk Active", "2M/4M/1G Walk/Ki", "4K Walk/Ki", "uTLB Miss/Ki" };

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float completedWalks = counterData.pmc[0] + counterData.pmc[2];
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatLargeNumber(counterData.instr / counterData.packagePower),
                        string.Format("{0:F2}", 1000 * completedWalks / counterData.instr),
                        string.Format("{0:F2} clks", counterData.pmc[3] / completedWalks),
                        FormatPercentage(counterData.pmc[3], counterData.activeCycles),
                        string.Format("{0:F2}", 1000 * counterData.pmc[0] / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.pmc[2] / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.pmc[1] / counterData.instr)
                };
            }
        }

        public class LSU : MonitoringConfig
        {
            private GoldmontPlus cpu;
            public string GetConfigName() { return "Load/Store Unit"; }
            public string GetHelpText() { return ""; }

            public LSU(GoldmontPlus intelCpu)
            {
                cpu = intelCpu;
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
                    ulong alias4k = GetPerfEvtSelRegisterValue(0x3, 0x4, usr: true, os: true, edge: false, pc: false, interrupt: false, anyThread: false, enable: true, invert: false, cmask: 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL0, alias4k);

                    ulong blockedLoad = GetPerfEvtSelRegisterValue(0x3, 0x10, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL1, blockedLoad);

                    ulong blockedDataUnknown = GetPerfEvtSelRegisterValue(0x3, 0x1, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL2, blockedDataUnknown);

                    ulong forwardedStoreMismatch = GetPerfEvtSelRegisterValue(0x3, 0x2, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL3, forwardedStoreMismatch);
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
                results.overallCounterValues = cpu.GetOverallCounterValues("Load Blocked 4K Alias", "Load Blocked", "Load Blocked Data Unknown", "Load Blocked Forward Size Mismatch");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "Pkg Pwr", "Instr/Watt",
                "Cycles w/Blocked Load", "Loads Blocked/Ki", "4K Alias/Ki", "Stlf Store Data Unavailable/Ki", "Mismatched Stlf/Ki" };

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatLargeNumber(counterData.instr / counterData.packagePower),
                        FormatPercentage(counterData.pmc[1], counterData.activeCycles),
                        string.Format("{0:F2}", 1000 * counterData.pmc[1] / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.pmc[0] / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.pmc[2] / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.pmc[3] / counterData.instr),
                };
            }
        }

        public class MemMachineClear : MonitoringConfig
        {
            private GoldmontPlus cpu;
            public string GetConfigName() { return "Machine Clears (Mem)"; }
            public string GetHelpText() { return ""; }

            public MemMachineClear(GoldmontPlus intelCpu)
            {
                cpu = intelCpu;
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
                    ulong utlbBlock = GetPerfEvtSelRegisterValue(0x3, 0x8, usr: true, os: true, edge: false, pc: false, interrupt: false, anyThread: false, enable: true, invert: false, cmask: 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL0, utlbBlock);

                    ulong machineClearDisambiguation = GetPerfEvtSelRegisterValue(0xC3, 0x8, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL1, machineClearDisambiguation);

                    ulong machineClearMemoryOrdering = GetPerfEvtSelRegisterValue(0xC3, 0x2, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL2, machineClearMemoryOrdering);

                    ulong machineClearPageFault = GetPerfEvtSelRegisterValue(0xC3, 0x20, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL3, machineClearPageFault);
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
                results.overallCounterValues = cpu.GetOverallCounterValues("LD Block UTLB Miss", "Clear for Mem Disambiguation", "Clear for Memory Ordering", "Clear for Page Fault");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "Pkg Pwr", "Instr/Watt",
                "LD Block UTLB Miss/Ki", "Mem Disambiguation Clear/Ki", "Mem Ordering Clear/Ki", "Page Fault Clear/Ki" };

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {

                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatLargeNumber(counterData.instr / counterData.packagePower),
                        string.Format("{0:F2}", 1000 * counterData.pmc[0] / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.pmc[1] / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.pmc[2] / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.pmc[3] / counterData.instr),
                };
            }
        }

        public class MachineClear1 : MonitoringConfig
        {
            private GoldmontPlus cpu;
            public string GetConfigName() { return "Machine Clears 1"; }
            public string GetHelpText() { return ""; }

            public MachineClear1(GoldmontPlus intelCpu)
            {
                cpu = intelCpu;
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
                    ulong machineClears = GetPerfEvtSelRegisterValue(0xC3, 0, usr: true, os: true, edge: false, pc: false, interrupt: false, anyThread: false, enable: true, invert: false, cmask: 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL0, machineClears);

                    ulong machineClearFpAssist = GetPerfEvtSelRegisterValue(0xC3, 0x4, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL1, machineClearFpAssist);

                    ulong machineClearSMC = GetPerfEvtSelRegisterValue(0xC3, 0x1, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL2, machineClearSMC);

                    ulong tlbFlush = GetPerfEvtSelRegisterValue(0xBD, 0x20, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL3, tlbFlush);
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
                results.overallCounterValues = cpu.GetOverallCounterValues("All Machine Clears", "FP Assist Clear", "SMC Clear", "TLB Flush");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "Pkg Pwr", "Instr/Watt",
                "All Clears/Ki", "FP Assist Clear/Ki", "SMC Clear/Ki", "TLB Flush/Ki", "TLB Flushes" };

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {

                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatLargeNumber(counterData.instr / counterData.packagePower),
                        string.Format("{0:F2}", 1000 * counterData.pmc[0] / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.pmc[1] / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.pmc[2] / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.pmc[3] / counterData.instr),
                        FormatLargeNumber(counterData.pmc[3])
                };
            }
        }

        public class MemLoads : MonitoringConfig
        {
            private GoldmontPlus cpu;
            public string GetConfigName() { return "Load Data Sources"; }
            public string GetHelpText() { return ""; }

            public MemLoads(GoldmontPlus intelCpu)
            {
                cpu = intelCpu;
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
                    ulong allLoads = GetPerfEvtSelRegisterValue(0xD0, 0x81, usr: true, os: true, edge: false, pc: false, interrupt: false, anyThread: false, enable: true, invert: false, cmask: 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL0, allLoads);

                    ulong l1Miss = GetPerfEvtSelRegisterValue(0xD1, 0x8, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL1, l1Miss);

                    ulong l2Miss = GetPerfEvtSelRegisterValue(0xD1, 0x2, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL2, l2Miss);

                    ulong dtlbMiss = GetPerfEvtSelRegisterValue(0xD0, 0x13, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL3, dtlbMiss);
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
                results.overallCounterValues = cpu.GetOverallCounterValues("All Loads", "L1 Miss", "L2 Miss", "DTLB Miss");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "Pkg Pwr", "Instr/Watt",
                "Load Uops Retired", "L1D Hitrate", "L1D MPKI", "L2 Hitrate", "L2 MPKI", "DTLB Hitrate", "DTLB MPKI" };

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {

                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatLargeNumber(counterData.instr / counterData.packagePower),
                        FormatLargeNumber(counterData.pmc[0]),
                        FormatPercentage((counterData.pmc[0] - counterData.pmc[1]), counterData.pmc[0]),
                        string.Format("{0:F2}", 1000 * counterData.pmc[1] / counterData.instr),
                        FormatPercentage((counterData.pmc[1] - counterData.pmc[2]), counterData.pmc[1]),
                        string.Format("{0:F2}", 1000 * counterData.pmc[2] / counterData.instr),
                        FormatPercentage((counterData.pmc[0] - counterData.pmc[3]), counterData.pmc[0]),
                        string.Format("{0:F2}", 1000 * counterData.pmc[3] / counterData.instr),
                };
            }
        }

        public class OffcoreL2 : MonitoringConfig
        {
            private GoldmontPlus cpu;
            public string GetConfigName() { return "Offcore: L2"; }
            public string GetHelpText() { return ""; }

            public OffcoreL2(GoldmontPlus intelCpu)
            {
                cpu = intelCpu;
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
                    ulong offcoreRsp0 = GetPerfEvtSelRegisterValue(OFFCORE_RESPONSE_EVENT, 0x1, usr: true, os: true, edge: false, pc: false, interrupt: false, anyThread: false, enable: true, invert: false, cmask: 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL0, offcoreRsp0);

                    ulong offcoreRsp1 = GetPerfEvtSelRegisterValue(OFFCORE_RESPONSE_EVENT, 0x2, usr: true, os: true, edge: false, pc: false, interrupt: false, anyThread: false, enable: true, invert: false, cmask: 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL1, offcoreRsp1);

                    Ring0.WriteMsr(MSR_OFFCORE_RSP0, 0x48000); //  L2 hit
                    Ring0.WriteMsr(MSR_OFFCORE_RSP1, 0x18000); // any response

                    ulong l2qReject = GetPerfEvtSelRegisterValue(0x31, 0, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL2, l2qReject);

                    ulong xqReject = GetPerfEvtSelRegisterValue(0x30, 0, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL3, xqReject);
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
                    results.unitMetrics[threadIdx] = computeMetrics("Thread " + threadIdx, cpu.NormalizedThreadCounts[threadIdx], null);
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts, cpu.RawTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues("L2 Hit", "L2 Request", "L2Q Reject", "XQ Reject");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "Pkg Pwr", "Instr/Watt",
                "L2 Hitrate", "L2 Hit BW", "L2 MPKI", "L2Q Reject/Cycles", "XQ Reject/Cycles", "Total Instructions", "Total L2 Hit Data", "Total L2 Miss Data" };

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData, RawTotalCoreCounterData totals)
            {

                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatLargeNumber(counterData.instr / counterData.packagePower), // instr/watt
                        FormatPercentage(counterData.pmc[0], counterData.pmc[1]),
                        FormatLargeNumber(64 * counterData.pmc[0]) + "B/s",
                        string.Format("{0:F2}", 1000 * (counterData.pmc[1] - counterData.pmc[0]) / counterData.instr),
                        FormatPercentage(counterData.pmc[2], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[3], counterData.activeCycles),
                        totals == null ? "-" : FormatLargeNumber(totals.instr),
                        totals == null ? "-" : FormatLargeNumber(64 * totals.pmc[0]) + "B",
                        totals == null ? "-" : FormatLargeNumber(64 * (totals.pmc[1] - totals.pmc[0])) + "B"
                };
            }
        }

        public class BackendStall : MonitoringConfig
        {
            private GoldmontPlus cpu;
            public string GetConfigName() { return "Backend Stalls"; }
            public string GetHelpText() { return ""; }

            public BackendStall(GoldmontPlus intelCpu)
            {
                cpu = intelCpu;
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
                    ulong resourceFullSlots = GetPerfEvtSelRegisterValue(0xCA, 0x1, usr: true, os: true, edge: false, pc: false, interrupt: false, anyThread: false, enable: true, invert: false, cmask: 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL0, resourceFullSlots);

                    ulong recoverySlots = GetPerfEvtSelRegisterValue(0xCA, 0x2, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL1, recoverySlots);

                    ulong allStallSlots = GetPerfEvtSelRegisterValue(0xCA, 0, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL2, allStallSlots);

                    ulong uopsNotDelivered = GetPerfEvtSelRegisterValue(0x9C, 0, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL3, uopsNotDelivered);
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
                results.overallCounterValues = cpu.GetOverallCounterValues("Resource Full", "Recovery", "All Slots Not Consumed", "Uops Not Delivered");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "Pkg Pwr", "Instr/Watt",
                "Resource Full", "Recovery", "All Backend Bound", "All FE Bound" };

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float totalSlots = counterData.activeCycles * 4;
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatLargeNumber(counterData.instr / counterData.packagePower),
                        FormatPercentage(counterData.pmc[0], totalSlots),
                        FormatPercentage(counterData.pmc[1], totalSlots),
                        FormatPercentage(counterData.pmc[2], totalSlots),
                        FormatPercentage(counterData.pmc[3], totalSlots)
                };
            }
        }

        public class Decode : MonitoringConfig
        {
            private GoldmontPlus cpu;
            public string GetConfigName() { return "Decode/Uops"; }
            public string GetHelpText() { return ""; }

            public Decode(GoldmontPlus intelCpu)
            {
                cpu = intelCpu;
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
                    ulong predecodeWrong = GetPerfEvtSelRegisterValue(0xE9, 0x1, usr: true, os: true, edge: false, pc: false, interrupt: false, anyThread: false, enable: true, invert: false, cmask: 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL0, predecodeWrong);

                    ulong microcodeEntry = GetPerfEvtSelRegisterValue(0xE7, 0x1, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL1, microcodeEntry);

                    ulong uopsRetired = GetPerfEvtSelRegisterValue(0xC2, 0, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL2, uopsRetired);

                    ulong uopsIssued = GetPerfEvtSelRegisterValue(0xE, 0, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL3, uopsIssued);
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
                results.overallCounterValues = cpu.GetOverallCounterValues("Predecode Wrong", "MS Entry", "Uops Retired", "Uops Renamed");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "Pkg Pwr", "Instr/Watt",
                "Predecode Wrong", "Microcode Entry", "Bad Speculation", "Uops/Instr" };

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float totalSlots = counterData.activeCycles * 4;
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatLargeNumber(counterData.instr / counterData.packagePower),
                        string.Format("{0:F2}", 1000 * counterData.pmc[0] / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.pmc[1] / counterData.instr),
                        FormatPercentage(counterData.pmc[3] - counterData.pmc[2], counterData.pmc[3]),
                        string.Format("{0:F2}", counterData.pmc[2] / counterData.instr)
                };
            }
        }

        public class IssueHistogram : MonitoringConfig
        {
            private GoldmontPlus cpu;
            public string GetConfigName() { return "Rename/Alloc Histogram"; }
            public string GetHelpText() { return ""; }

            public IssueHistogram(GoldmontPlus intelCpu)
            {
                cpu = intelCpu;
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
                    Ring0.WriteMsr(IA32_PERFEVTSEL0, GetPerfEvtSelRegisterValue(0xE, 0, true, true, false, false, false, false, true, false, 1));
                    Ring0.WriteMsr(IA32_PERFEVTSEL1, GetPerfEvtSelRegisterValue(0xE, 0, true, true, false, false, false, false, true, false, 2));
                    Ring0.WriteMsr(IA32_PERFEVTSEL2, GetPerfEvtSelRegisterValue(0xE, 0, true, true, false, false, false, false, true, false, 3));
                    Ring0.WriteMsr(IA32_PERFEVTSEL3, GetPerfEvtSelRegisterValue(0xE, 0, true, true, false, false, false, false, true, false, 4));
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
                results.overallCounterValues = cpu.GetOverallCounterValues("Issue cmask 1", "Issue cmask 2", "Issue cmask 3", "Issue cmask 4");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "Pkg Pwr", "Instr/Watt",
                "Ops Issued/Clk", "Issue Active", "1 Op Issued", "2 Ops Issued", "3 Ops Issued", "4 Ops Issued" };

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float oneOp = counterData.pmc[0] - counterData.pmc[1];
                float twoOps = counterData.pmc[1] - counterData.pmc[2];
                float threeOps = counterData.pmc[2] - counterData.pmc[3];
                float fourOps = counterData.pmc[3];
                float totalOps = oneOp + 2 * twoOps + 3 * threeOps + 4 * fourOps;
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatLargeNumber(counterData.instr / counterData.packagePower),
                        string.Format("{0:F2}", totalOps / counterData.activeCycles),
                        FormatPercentage(counterData.pmc[0], counterData.activeCycles),
                        FormatPercentage(oneOp, counterData.activeCycles),
                        FormatPercentage(twoOps, counterData.activeCycles),
                        FormatPercentage(threeOps, counterData.activeCycles),
                        FormatPercentage(fourOps, counterData.activeCycles),
                };
            }
        }

        public class OffcoreHitm : MonitoringConfig
        {
            private GoldmontPlus cpu;
            public string GetConfigName() { return "Offcore: C2C"; }
            public string GetHelpText() { return ""; }

            public OffcoreHitm(GoldmontPlus intelCpu)
            {
                cpu = intelCpu;
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
                    ulong offcoreRsp0 = GetPerfEvtSelRegisterValue(OFFCORE_RESPONSE_EVENT, 0x1, usr: true, os: true, edge: false, pc: false, interrupt: false, anyThread: false, enable: true, invert: false, cmask: 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL0, offcoreRsp0);

                    ulong offcoreRsp1 = GetPerfEvtSelRegisterValue(OFFCORE_RESPONSE_EVENT, 0x2, usr: true, os: true, edge: false, pc: false, interrupt: false, anyThread: false, enable: true, invert: false, cmask: 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL1, offcoreRsp1);

                    Ring0.WriteMsr(MSR_OFFCORE_RSP0, 0x1000008000); //  L2 miss, hit modified in another core
                    Ring0.WriteMsr(MSR_OFFCORE_RSP1, 0x18000); // any response

                    ulong l2qReject = GetPerfEvtSelRegisterValue(0x31, 0, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL2, l2qReject);

                    ulong xqReject = GetPerfEvtSelRegisterValue(0x30, 0, true, true, false, false, false, false, true, false, 0);
                    Ring0.WriteMsr(IA32_PERFEVTSEL3, xqReject);
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
                results.overallCounterValues = cpu.GetOverallCounterValues("L2 Miss HitM in Other Core", "L2 Request", "L2Q Reject", "XQ Reject");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "Pkg Pwr", "Instr/Watt",
                "% Offcore Reqs = Cross-Core", "Cross-Core BW", "Cross-Core/Ki", "L2Q Reject/Cycles", "XQ Reject/Cycles" };

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {

                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatLargeNumber(counterData.instr / counterData.packagePower), // instr/watt
                        FormatPercentage(counterData.pmc[0], counterData.pmc[1]),
                        FormatLargeNumber(64 * counterData.pmc[0]) + "B/s",
                        string.Format("{0:F2}", 1000 * counterData.pmc[0] / counterData.instr),
                        FormatPercentage(counterData.pmc[2], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[3], counterData.activeCycles)
                };
            }
        }

    }
}

using System;
using System.Collections.Generic;
using PmcReader.Interop;

namespace PmcReader.Intel
{
    public class ArrowLake : ModernIntelCpu
    {
        public static byte ADL_P_CORE_TYPE = 0x40;
        public static byte ADL_E_CORE_TYPE = 0x20;

        public ArrowLake()
        {
            List<MonitoringConfig> configs = new List<MonitoringConfig>();
            architectureName = "Arrow Lake";
            if (coreTypes.Length > 1)
                architectureName += " (Hybrid)";

            // Fix enumeration vs HW support
            for (byte coreIdx = 0; coreIdx < coreTypes.Length; coreIdx++)
            {
                if (coreTypes[coreIdx].Type == ADL_P_CORE_TYPE)
                {
                    coreTypes[coreIdx].Name = "P-Core";
                }
                if (coreTypes[coreIdx].Type == ADL_E_CORE_TYPE)
                {
                    coreTypes[coreIdx].AllocWidth = 8;
                    coreTypes[coreIdx].Name = "E-Core";
                }
            }

            // Create supported configs
            configs.Add(new ArchitecturalCounters(this));
            configs.Add(new RetireHistogram(this));
            for (byte coreIdx = 0; coreIdx < coreTypes.Length; coreIdx++)
            {
                if (coreTypes[coreIdx].Type == ADL_P_CORE_TYPE)
                {
                    configs.Add(new PCoreTopDown(this));
                    configs.Add(new PCoreBranch(this));
                    configs.Add(new PCoreIFetch(this));
                    configs.Add(new PCoreMem(this));
                    configs.Add(new PCoreReadEvt(this));
                    configs.Add(new PCoreL2(this));
                    configs.Add(new PCoreMemStalls(this));
                    configs.Add(new PCoreIntMisc(this));
                    configs.Add(new PCoreRetireHistogram(this));
                    configs.Add(new PCoreRetireBurst(this));
                    configs.Add(new PCoreIFetchExperiemntal(this));
                }
                if (coreTypes[coreIdx].Type == ADL_E_CORE_TYPE)
                {
                    configs.Add(new ECoreTopDown(this));
                    configs.Add(new ECoreBranch(this));
                    configs.Add(new ECoreIFetch(this));
                    configs.Add(new ECoreDecode(this));
                    configs.Add(new ECoreDispatchStall(this));
                    configs.Add(new ECoreMemBound(this));
                    configs.Add(new ECoreLoadData(this));
                    configs.Add(new ECoreMachineClear(this));
                    configs.Add(new ECoreSerialization(this));
                    configs.Add(new ECoreMemBound1(this));
                }
            }
            monitoringConfigs = configs.ToArray();
        }

        public static ulong GetArlPerfEvtSelValue(byte perfEvent,
                                   byte umask,
                                   bool usr = true,
                                   bool os = true,
                                   bool edge = false,
                                   bool pc = false,
                                   bool interrupt = false,
                                   bool anyThread = false,
                                   bool enable = true,
                                   bool invert = false,
                                   byte cmask = 0,
                                   byte umaskExt = 0)
        {
            ulong value = (ulong)perfEvent |
                (ulong)umask << 8 |
                (usr ? 1UL : 0UL) << 16 |
                (os ? 1UL : 0UL) << 17 |
                (edge ? 1UL : 0UL) << 18 |
                (pc ? 1UL : 0UL) << 19 |
                (interrupt ? 1UL : 0UL) << 20 |
                (anyThread ? 1UL : 0UL) << 21 |
                (enable ? 1UL : 0UL) << 22 |
                (invert ? 1UL : 0UL) << 23 |
                (ulong)cmask << 24 |
                (ulong)umaskExt << 48;
            return value;
        }

        #region pcore
        public class PCoreMem : MonitoringConfig
        {
            private ArrowLake cpu;
            private CoreType coreType;
            public string GetConfigName() { return "P Cores: Mem Load"; }

            public PCoreMem(ArrowLake intelCpu)
            {
                cpu = intelCpu;
                foreach (CoreType type in cpu.coreTypes)
                {
                    if (type.Type == ADL_P_CORE_TYPE)
                    {
                        coreType = type;
                        break;
                    }
                }
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.DisablePerformanceCounters();

                ulong[] pmc = new ulong[8];
                pmc[0] = 0x100004300D1; // L1 hit L1
                pmc[1] = GetPerfEvtSelRegisterValue(0xD1, 2); // hit L2
                pmc[2] = GetPerfEvtSelRegisterValue(0xD1, 4); // hit L3
                pmc[3] = GetPerfEvtSelRegisterValue(0xD1, 0x20); // L3 miss
                pmc[4] = GetPerfEvtSelRegisterValue(0xE5, 0xF); // memory uops retired
                pmc[5] = GetPerfEvtSelRegisterValue(0x2E, 0x41); // LLC Miss (architectural)
                pmc[6] = GetPerfEvtSelRegisterValue(0x2E, 0x4F); // LLC Ref (architectural)
                pmc[7] = GetPerfEvtSelRegisterValue(0x42, 2); // l1d locked
                cpu.ProgramPerfCounters(pmc, coreType.Type);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[coreType.CoreCount][];
                cpu.InitializeCoreTotals();
                int pCoreIdx = 0;
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    if (((coreType.CoreMask >> threadIdx) & 0x1) != 0x1)
                        continue;
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[pCoreIdx] = computeMetrics(coreType.Name + " " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                    pCoreIdx++;
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues(new String[] {
                    "l1 hit l1", "l2 hit", "l3 hit", "l3 miss", "mem uops retired", "llc miss", "llc ref", "l1d lock cycles" });
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "PkgPower", "Instr/Watt",
                "~L1D Hitrate", "L1D MPKI", "L1.5D Hitrate", "L1.5D MPKI", "L2D Hitrate", "L2D MPKI", "L3D Hitrate", "L3D MPKI", "L3 Hitrate", "L3 MPKI", "L1D Locked" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float memUops = counterData.pmc[4];
                float l1hitl1 = counterData.pmc[0];
                float hitl2 = counterData.pmc[1];
                float hitl3 = counterData.pmc[2];
                float l3miss = counterData.pmc[3];
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatLargeNumber(counterData.instr / counterData.packagePower),
                        FormatPercentage(memUops - l1hitl1 - hitl2 - hitl3 - l3miss, memUops),
                        string.Format("{0:F2}", 1000 * (l1hitl1 + hitl2 + hitl3+ l3miss) / counterData.instr),
                        FormatPercentage(l1hitl1, l1hitl1 + hitl2 + hitl3 + l3miss),
                        string.Format("{0:F2}", 1000 * (hitl2 + hitl3 + l3miss) / counterData.instr),
                        FormatPercentage(hitl2, hitl2 + hitl3 + l3miss),
                        string.Format("{0:F2}", 1000 * (hitl3 + l3miss) / counterData.instr),
                        FormatPercentage(hitl3, l3miss + hitl3),
                        string.Format("{0:F2}", 1000 * l3miss / counterData.instr),
                        FormatPercentage(counterData.pmc[6] - counterData.pmc[5], counterData.pmc[6]),
                        string.Format("{0:F2}", 1000 * counterData.pmc[5] / counterData.instr),
                        FormatPercentage(counterData.pmc[7], counterData.activeCycles),
                };
            }
        }

        public class PCoreMemStalls : MonitoringConfig
        {
            private ArrowLake cpu;
            private CoreType coreType;
            public string GetConfigName() { return "P Cores: Mem Bound"; }

            public PCoreMemStalls(ArrowLake intelCpu)
            {
                cpu = intelCpu;
                foreach (CoreType type in cpu.coreTypes)
                {
                    if (type.Type == ADL_P_CORE_TYPE)
                    {
                        coreType = type;
                        break;
                    }
                }
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.DisablePerformanceCounters();

                ulong[] pmc = new ulong[8];
                pmc[0] = GetPerfEvtSelRegisterValue(0x20, 0x1); // Offcore Outstanding Data Read Occupancy (demand)
                pmc[1] = GetPerfEvtSelRegisterValue(0x21, 0x1); // Offcore data reads (demand)
                pmc[2] = GetPerfEvtSelRegisterValue(0x20, 0x2); // Offcore code reads occupancy
                pmc[3] = GetPerfEvtSelRegisterValue(0x21, 0x2); // Offcore code reads
                pmc[4] = GetPerfEvtSelRegisterValue(0x46, 1); // stall L1 bound
                pmc[5] = GetPerfEvtSelRegisterValue(0x46, 2); // stall L2 bound
                pmc[6] = GetPerfEvtSelRegisterValue(0x46, 4); // stall L3 bound
                pmc[7] = GetPerfEvtSelRegisterValue(0x46, 8); // stall mem bound
                cpu.ProgramPerfCounters(pmc, coreType.Type);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[coreType.CoreCount][];
                cpu.InitializeCoreTotals();
                int pCoreIdx = 0;
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    if (((coreType.CoreMask >> threadIdx) & 0x1) != 0x1)
                        continue;
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[pCoreIdx] = computeMetrics(coreType.Name + " " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                    pCoreIdx++;
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues(new String[] {
                    "Offcore Data Reads Occupancy", "Offcore Data Reads", "Offcore Code Reads Occupancy", "Offcore Code Reads", "L1 Bound Cycles", "L2 Bound Cycles", "L3 Bound Cycles", "Mem Bound Cycles" });
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "PkgPower", "Instr/Watt",
                "L1 Bound", "L2 Bound", "L3 Bound", "Mem Bound", "Offcore Data Rd BW", "Offcore Data Latency", "Offcore Code Rd BW", "Offcore Code Rd Latency" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatLargeNumber(counterData.instr / counterData.packagePower),
                        FormatPercentage(counterData.pmc[4], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[5], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[6], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[7], counterData.activeCycles),
                        FormatLargeNumber(counterData.pmc[1] * 64) + "B/s",
                        string.Format("{0:F2} clks", counterData.pmc[0] / counterData.pmc[1]),
                        FormatLargeNumber(counterData.pmc[3] * 64) + "B/s",
                        string.Format("{0:F2} clks", counterData.pmc[2] / counterData.pmc[3])
                };
            }
        }

        public class PCoreRetireHistogram : MonitoringConfig
        {
            private ArrowLake cpu;
            private CoreType coreType;
            public string GetConfigName() { return "P Cores: Retire Histogram"; }

            public PCoreRetireHistogram(ArrowLake intelCpu)
            {
                cpu = intelCpu;
                foreach (CoreType type in cpu.coreTypes)
                {
                    if (type.Type == ADL_P_CORE_TYPE)
                    {
                        coreType = type;
                        break;
                    }
                }
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.DisablePerformanceCounters();

                ulong[] pmc = new ulong[8];
                for (byte i = 0; i < 8; i++)
                {
                    // UOPS_RETIRED.SLOTS
                    pmc[i] = GetPerfEvtSelRegisterValue(0xC2, 2, cmask: (byte)(i + 1));
                }

                cpu.ProgramPerfCounters(pmc, coreType.Type);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[coreType.CoreCount][];
                cpu.InitializeCoreTotals();
                int pCoreIdx = 0;
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    if (((coreType.CoreMask >> threadIdx) & 0x1) != 0x1)
                        continue;
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[pCoreIdx] = computeMetrics(coreType.Name + " " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                    pCoreIdx++;
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues(new string[] {
                    ">=1 uop", ">=2 uops", ">=3 uops", ">=4 uops", ">=5 uops", ">=6 uops", ">=7 uops", ">=8 uops" });
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "PkgPower", "Instr/Watt",
                ">=1 uop", ">=2 uops", ">=3 uops", ">=4 uops", ">=5 uops", ">=6 uops", ">=7 uops", ">=8 uops" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatLargeNumber(counterData.instr / counterData.packagePower),
                        FormatPercentage(counterData.pmc[0], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[1], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[2], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[3], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[4], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[5], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[6], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[7], counterData.activeCycles),
                };
            }
        }

        public class PCoreRetireBurst : MonitoringConfig
        {
            private ArrowLake cpu;
            private CoreType coreType;
            public string GetConfigName() { return "P Cores: Retire Burst"; }

            public PCoreRetireBurst(ArrowLake intelCpu)
            {
                cpu = intelCpu;
                foreach (CoreType type in cpu.coreTypes)
                {
                    if (type.Type == ADL_P_CORE_TYPE)
                    {
                        coreType = type;
                        break;
                    }
                }
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.DisablePerformanceCounters();

                ulong[] pmc = new ulong[8];
                pmc[0] = GetPerfEvtSelRegisterValue(0xC2, 2, cmask: 1); // retire active
                pmc[1] = GetPerfEvtSelRegisterValue(0xC2, 2, cmask: 1, edge: true); // retire active, edge
                pmc[2] = GetPerfEvtSelRegisterValue(0xC2, 2, cmask: 12);
                pmc[3] = GetPerfEvtSelRegisterValue(0xC2, 2, cmask: 12, edge: true);
                pmc[4] = GetPerfEvtSelRegisterValue(0xC2, 2, cmask: 4); // can do cmask 1 - cmask 4 to get <4 ops retired cycles
                pmc[5] = GetPerfEvtSelRegisterValue(0xC2, 2, cmask: 4, edge: true); // ditto to get edge transitions ^

                cpu.ProgramPerfCounters(pmc, coreType.Type);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[coreType.CoreCount][];
                cpu.InitializeCoreTotals();
                int pCoreIdx = 0;
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    if (((coreType.CoreMask >> threadIdx) & 0x1) != 0x1)
                        continue;
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[pCoreIdx] = computeMetrics(coreType.Name + " " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                    pCoreIdx++;
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues(new string[] {
                    "Retire active", "Retire active edge", "Retire cmask 12", "Retire cmask 12 edge", "Retire cmask 4", "Retire cmask 4 edge" });
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "PkgPower", "Instr/Watt",
                "Retire Active", "Retire Active Duration", "Max Retire", "Max Retire Duration"};

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatLargeNumber(counterData.instr / counterData.packagePower),
                        FormatPercentage(counterData.pmc[0], counterData.activeCycles),
                        string.Format("{0:F2} clk", counterData.pmc[0] / counterData.pmc[1]),
                        FormatPercentage(counterData.pmc[2], counterData.activeCycles),
                        string.Format("{0:F2} clk", counterData.pmc[2] / counterData.pmc[3])
                };
            }
        }

        public class PCoreIntMisc : MonitoringConfig
        {
            private ArrowLake cpu;
            private CoreType coreType;
            public string GetConfigName() { return "P: INT MISC, L1D Miss"; }

            public PCoreIntMisc(ArrowLake intelCpu)
            {
                cpu = intelCpu;
                foreach (CoreType type in cpu.coreTypes)
                {
                    if (type.Type == ADL_P_CORE_TYPE)
                    {
                        coreType = type;
                        break;
                    }
                }
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.DisablePerformanceCounters();

                ulong[] pmc = new ulong[8];
                pmc[0] = GetPerfEvtSelRegisterValue(0x2D, 0x1); // XQ.FULL
                pmc[1] = GetPerfEvtSelRegisterValue(0x48, 0x1); // L1D_PENDING.LOAD
                pmc[2] = GetPerfEvtSelRegisterValue(0x49, 0x21); // L1D_MISS.Load
                pmc[3] = GetPerfEvtSelRegisterValue(0x49, 0x2); // L1D_MISS.FB_FULL
                pmc[4] = GetPerfEvtSelRegisterValue(0xAD, 0x40); // BPClear bubble cycles
                pmc[5] = GetPerfEvtSelRegisterValue(0xAD, 0x80); // clear_resteer_cycles (time until first uop arrives from corrected path)
                pmc[6] = GetPerfEvtSelRegisterValue(0xAD, 1); // recovery_cycles (allocator stalled)
                pmc[7] = GetPerfEvtSelRegisterValue(0xAD, 1, cmask: 1, edge: true); // clear count
                cpu.ProgramPerfCounters(pmc, coreType.Type);

                // Set MSR_PEBS_FRONTEND across all cores
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    if (((coreType.CoreMask >> threadIdx) & 0x1) != 0x1)
                        continue;
                    Ring0.WriteMsr(MSR_PEBS_FRONTEND, 0xB); // for BPClear bubble cycles
                }
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[coreType.CoreCount][];
                cpu.InitializeCoreTotals();
                int pCoreIdx = 0;
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    if (((coreType.CoreMask >> threadIdx) & 0x1) != 0x1)
                        continue;
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[pCoreIdx] = computeMetrics(coreType.Name + " " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                    pCoreIdx++;
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues(new String[] {
                    "XQ Full Cycles", "L1D Pending Miss Occupancy", "L1D Miss Loads", "L1D Miss FB Full Cycles", "BPClear Bubble Cycles", "Clear Resteer (cycles until uop arrives)", "Recovery Cycles", "Clear Resteer Edge Detect" });
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "PkgPower",
                "L1D Miss Latency", "L1D Misses Pending", "L1D Miss Req BW", "FB Full", "BPClear", "Resteer Clear Cycles", "Allocator Recovery Cycles", "Clears/Ki", "Clear Duration" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        string.Format("{0:F2} clk", counterData.pmc[1] / counterData.pmc[2]),
                        string.Format("{0:F2}", counterData.pmc[1] / counterData.activeCycles),
                        FormatLargeNumber(counterData.pmc[2] * 64),
                        FormatPercentage(counterData.pmc[3], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[4], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[5], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[6], counterData.activeCycles),
                        string.Format("{0:F2}", 1000 * counterData.pmc[7] / counterData.instr),
                        string.Format("{0:F2} clk", counterData.pmc[5] / counterData.pmc[7])
                };
            }
        }

        public class PCoreIFetch : MonitoringConfig
        {
            private ArrowLake cpu;
            private CoreType coreType;
            public string GetConfigName() { return "P Cores: Instr Fetch"; }

            public PCoreIFetch(ArrowLake intelCpu)
            {
                cpu = intelCpu;
                foreach (CoreType type in cpu.coreTypes)
                {
                    if (type.Type == ADL_P_CORE_TYPE)
                    {
                        coreType = type;
                        break;
                    }
                }
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.DisablePerformanceCounters();

                ulong[] pmc = new ulong[8];
                pmc[0] = GetArlPerfEvtSelValue(0x79, 8); // DSB Uops
                pmc[1] = GetPerfEvtSelRegisterValue(0x79, 4); // mite uops
                pmc[2] = GetPerfEvtSelRegisterValue(0xA8, 1); // lsd uops
                pmc[3] = GetPerfEvtSelRegisterValue(0x80, 4); // icache data miss cycles
                pmc[4] = GetPerfEvtSelRegisterValue(0x79, 0x20); // MS uops
                pmc[5] = GetPerfEvtSelRegisterValue(0x9C, 1, cmask: 8); // frontend latency bound cycles
                pmc[6] = GetPerfEvtSelRegisterValue(0x9C, 1); // frontend bw bound slots
                pmc[7] = GetPerfEvtSelRegisterValue(0x80, 2, cmask: 1, edge: true); // frontend latency bound periods
                cpu.ProgramPerfCounters(pmc, coreType.Type);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[coreType.CoreCount][];
                cpu.InitializeCoreTotals();
                int pCoreIdx = 0;
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    if (((coreType.CoreMask >> threadIdx) & 0x1) != 0x1)
                        continue;
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[pCoreIdx] = computeMetrics(coreType.Name + " " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                    pCoreIdx++;
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues(new String[] {
                    "DSB Ops", "MITE Ops", "LSD Ops", "IC Data Miss Stall Cycles", "MS Ops", "FE Latency Cycles", "FE BW Bound Slots", "IC Data Miss Stall Cycles Edge"});
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "PkgPower", "Instr/Watt",
                "LSD", "DSB", "MITE", "MS", "IC Miss Stall", "Avg IC Miss Stall", "Frontend Latency Bound", "Frontend BW Bound", "Frontend Latency Avg Duration" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float feOps = counterData.pmc[0] + counterData.pmc[1] + counterData.pmc[2] + counterData.pmc[4];
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatLargeNumber(counterData.instr / counterData.packagePower),
                        FormatPercentage(counterData.pmc[2], feOps),
                        FormatPercentage(counterData.pmc[0], feOps),
                        FormatPercentage(counterData.pmc[1], feOps),
                        FormatPercentage(counterData.pmc[4], feOps),
                        FormatPercentage(counterData.pmc[3], counterData.activeCycles),
                        string.Format("{0:F2} clk", counterData.pmc[3] / counterData.pmc[7]),
                        FormatPercentage(counterData.pmc[5], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[6], counterData.activeCycles * 8),
                        string.Format("{0:F2} clk", counterData.pmc[5] / counterData.pmc[7])
                };
            }
        }
        public class PCoreIFetchExperiemntal : MonitoringConfig
        {
            private ArrowLake cpu;
            private CoreType coreType;
            public string GetConfigName() { return "P Cores: IF Experiment"; }

            public PCoreIFetchExperiemntal(ArrowLake intelCpu)
            {
                cpu = intelCpu;
                foreach (CoreType type in cpu.coreTypes)
                {
                    if (type.Type == ADL_P_CORE_TYPE)
                    {
                        coreType = type;
                        break;
                    }
                }
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.DisablePerformanceCounters();

                ulong[] pmc = new ulong[8];
                pmc[0] = GetArlPerfEvtSelValue(0x83, 1); // iftag hit
                pmc[1] = GetPerfEvtSelRegisterValue(0x83, 2); // iftag miss
                pmc[2] = GetPerfEvtSelRegisterValue(0xA8, 1); // lsd uops
                pmc[3] = GetPerfEvtSelRegisterValue(0x80, 4); // icache data miss cycles
                pmc[4] = GetPerfEvtSelRegisterValue(0x79, 0x20); // MS uops
                pmc[5] = GetPerfEvtSelRegisterValue(0x9C, 1, cmask: 8); // frontend latency bound cycles
                pmc[6] = GetPerfEvtSelRegisterValue(0x9C, 1); // frontend bw bound slots
                pmc[7] = GetPerfEvtSelRegisterValue(0x80, 2, cmask: 1, edge: true); // frontend latency bound periods
                cpu.ProgramPerfCounters(pmc, coreType.Type);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[coreType.CoreCount][];
                cpu.InitializeCoreTotals();
                int pCoreIdx = 0;
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    if (((coreType.CoreMask >> threadIdx) & 0x1) != 0x1)
                        continue;
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[pCoreIdx] = computeMetrics(coreType.Name + " " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                    pCoreIdx++;
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues(new String[] {
                    "ic hit?", "ic miss?", "LSD Ops", "IC Data Miss Stall Cycles", "MS Ops", "FE Latency Cycles", "FE BW Bound Slots", "IC Data Miss Stall Cycles Edge"});
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "PkgPower", "Instr/Watt",
                "IC Hitrate?", "IC MPKI?", "IC Hit BW?" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float feOps = counterData.pmc[0] + counterData.pmc[1] + counterData.pmc[2] + counterData.pmc[4];
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatLargeNumber(counterData.instr / counterData.packagePower),
                        FormatPercentage(counterData.pmc[0], counterData.pmc[0] + counterData.pmc[1]),
                        string.Format("{0:F2}", 1000 * counterData.pmc[1] / counterData.instr),
                        FormatLargeNumber(64 * counterData.pmc[0]) + "B/s"
                };
            }
        }

        public class PCoreBranch : MonitoringConfig
        {
            private ArrowLake cpu;
            private CoreType coreType;
            public string GetConfigName() { return "P Cores: Branch"; }

            public PCoreBranch(ArrowLake intelCpu)
            {
                cpu = intelCpu;
                foreach (CoreType type in cpu.coreTypes)
                {
                    if (type.Type == ADL_P_CORE_TYPE)
                    {
                        coreType = type;
                        break;
                    }
                }
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.DisablePerformanceCounters();

                ulong[] pmc = new ulong[8];
                pmc[0] = GetArlPerfEvtSelValue(0x60, 1); // BAClear.any (unknown branch)
                pmc[1] = GetPerfEvtSelRegisterValue(0xad, 0x40); // BAClear cycles
                pmc[2] = GetPerfEvtSelRegisterValue(0xad, 0x80); // int_misc.clear_resteer cycles (after recovery from mispredict until first uop arrives)
                pmc[3] = GetPerfEvtSelRegisterValue(0xC5, 0); // mispredicted branches
                pmc[4] = GetPerfEvtSelRegisterValue(0xC4, 0); // retired branches
                pmc[5] = GetPerfEvtSelRegisterValue(0xad, 1); // allocator stalled from earlier clear event (int_misc.recovery_cycles)
                pmc[6] = GetPerfEvtSelRegisterValue(0xA5, 7); // RS empty
                pmc[7] = GetPerfEvtSelRegisterValue(0xA5, 1); // RS empty, resource allocation stall
                cpu.ProgramPerfCounters(pmc, coreType.Type);

                // Set MSR_PEBS_FRONTEND across all cores
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    if (((coreType.CoreMask >> threadIdx) & 0x1) != 0x1)
                        continue;
                    Ring0.WriteMsr(MSR_PEBS_FRONTEND, 0x7); // for BAClear bubble cycles
                }
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[coreType.CoreCount][];
                cpu.InitializeCoreTotals();
                int pCoreIdx = 0;
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    if (((coreType.CoreMask >> threadIdx) & 0x1) != 0x1)
                        continue;
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[pCoreIdx] = computeMetrics(coreType.Name + " " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                    pCoreIdx++;
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues(new String[] {
                    "BAClear", "BAClear Bubble Cycles", "INT_MISC.CLEAR_RESTEER cycles", "Mispredicted Branches", "Branches", "Allocator Stalled INT_MISC.RECOVERY_CYCLES", "RS Empty", "RS Empty Alloc Stall"});
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "PkgPower", "Instr/Watt",
                "BPU Accuracy", "BPU MPKI", "BAClear/Ki", "BAClear Bubble", "Clear Resteer Cycles", "Alloc Stalled", "RS Empty", "RS Empty Not Resource Bound" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float feOps = counterData.pmc[0] + counterData.pmc[1] + counterData.pmc[2] + counterData.pmc[4];
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatLargeNumber(counterData.instr / counterData.packagePower),
                        FormatPercentage(counterData.pmc[4] - counterData.pmc[3], counterData.pmc[4]),
                        string.Format("{0:F2}", 1000 * counterData.pmc[3] / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.pmc[0] / counterData.instr),
                        FormatPercentage(counterData.pmc[2], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[3], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[5], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[6], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[6] - counterData.pmc[5], counterData.activeCycles)
                };
            }
        }

        public class PCoreL2 : MonitoringConfig
        {
            private ArrowLake cpu;
            private CoreType coreType;
            public string GetConfigName() { return "P Cores: L2"; }

            public PCoreL2(ArrowLake intelCpu)
            {
                cpu = intelCpu;
                foreach (CoreType type in cpu.coreTypes)
                {
                    if (type.Type == ADL_P_CORE_TYPE)
                    {
                        coreType = type;
                        break;
                    }
                }
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.DisablePerformanceCounters();

                ulong[] pmc = new ulong[8];
                pmc[0] = GetPerfEvtSelRegisterValue(0x24, 0xFF); // L2 Ref
                pmc[1] = GetPerfEvtSelRegisterValue(0x24, 0x3F); // L2 Miss
                pmc[2] = GetPerfEvtSelRegisterValue(0x25, 0x1F); // L2 lines in
                pmc[3] = GetPerfEvtSelRegisterValue(0x26, 2); // L2 lines out, non-silent (WB)
                pmc[4] = GetPerfEvtSelRegisterValue(0x24, 0xE4); // L2 Code Read
                pmc[5] = GetPerfEvtSelRegisterValue(0x24, 0x24); // L2 Code Miss
                pmc[6] = GetPerfEvtSelRegisterValue(0x24, 0x41); // L2 demand data hit
                pmc[7] = GetPerfEvtSelRegisterValue(0x24, 0x21); // L2 demand data miss
                cpu.ProgramPerfCounters(pmc, coreType.Type);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[coreType.CoreCount][];
                cpu.InitializeCoreTotals();
                int pCoreIdx = 0;
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    if (((coreType.CoreMask >> threadIdx) & 0x1) != 0x1)
                        continue;
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[pCoreIdx] = computeMetrics(coreType.Name + " " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                    pCoreIdx++;
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues(new string[] {
                    "L2 Ref", "L2 Miss", "L2 Lines In", "L2 Lines Out Non-Silent", "L2 Code Read", "L2 Code Miss", "L2 Dmd Data Hit", "L2 Dmd Data Miss" });
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "PkgPower", "Instr/Watt",
                "L2 Hitrate", "L2 MPKI", "L2 Hit BW", "L2 Fill BW", "L2 WB BW", "L2 Code Hitrate", "L2 Code MPKI", "L2 Code Hit BW", 
                "L2 Dmd Data Hitrate", "L2 Dmd Data MPKI", "L2 Dmd Data Hit BW" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatLargeNumber(counterData.instr / counterData.packagePower),
                        FormatPercentage(counterData.pmc[0] - counterData.pmc[1], counterData.pmc[0]),
                        string.Format("{0:F2}", 1000 * counterData.pmc[1] / counterData.instr),
                        FormatLargeNumber((counterData.pmc[0] - counterData.pmc[1]) * 64) + "B/s",
                        FormatLargeNumber(counterData.pmc[2] * 64) + "B/s",
                        FormatLargeNumber(counterData.pmc[3] * 64) + "B/s",
                        FormatPercentage(counterData.pmc[4] - counterData.pmc[5], counterData.pmc[4]),
                        string.Format("{0:F2}", 1000 * counterData.pmc[5] / counterData.instr),
                        FormatLargeNumber((counterData.pmc[4] - counterData.pmc[5]) * 64) + "B/s",
                        FormatPercentage(counterData.pmc[6], counterData.pmc[6] + counterData.pmc[7]),
                        string.Format("{0:F2}", 1000 * counterData.pmc[7] / counterData.instr),
                        FormatLargeNumber(counterData.pmc[6] * 64) + "B/s"
                };
            }
        }

        public class PCoreTopDown : MonitoringConfig
        {
            private ArrowLake cpu;
            private CoreType coreType;
            public string GetConfigName() { return "P Cores: Top Down"; }

            public PCoreTopDown(ArrowLake intelCpu)
            {
                cpu = intelCpu;
                foreach (CoreType type in cpu.coreTypes)
                {
                    if (type.Type == ADL_P_CORE_TYPE)
                    {
                        coreType = type;
                        break;
                    }
                }
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.DisablePerformanceCounters();

                ulong[] pmc = new ulong[8];
                pmc[0] = GetPerfEvtSelRegisterValue(0xA4, 4); // bad spec 
                pmc[1] = GetPerfEvtSelRegisterValue(0x9C, 1); // FE bound
                pmc[2] = GetPerfEvtSelRegisterValue(0x9C, 1, cmask: 8); // FE latency bound cycles
                pmc[3] = GetPerfEvtSelRegisterValue(0xA4, 2); // backend bound
                pmc[4] = GetPerfEvtSelRegisterValue(0xA4, 0x10); // Mem Bound
                pmc[5] = GetPerfEvtSelRegisterValue(0xC2, 2); // uops retired
                pmc[6] = GetPerfEvtSelRegisterValue(0xA4, 1); // slots
                pmc[7] = 0;
                cpu.ProgramPerfCounters(pmc, coreType.Type);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[coreType.CoreCount][];
                cpu.InitializeCoreTotals();
                int pCoreIdx = 0;
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    if (((coreType.CoreMask >> threadIdx) & 0x1) != 0x1)
                        continue;
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[pCoreIdx] = computeMetrics(coreType.Name + " " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                    pCoreIdx++;
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues(new String[] {
                   "bad spec", "FE bound slots", "fe latency bound cycles", "backend bound", "backend mem bound", "uops retired", "slots", "unused" });
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "Uops/C", "PkgPower",
                "Bad Speculation", "FE Latency", "FE BW", "Core Bound", "Backend Mem Bound", "Retiring"};

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float slots = counterData.pmc[6];
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2}", counterData.pmc[5] / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatPercentage(counterData.pmc[0], slots),
                        FormatPercentage(counterData.pmc[2] * 8, slots), // frontend latency bound slots
                        FormatPercentage(counterData.pmc[1] - counterData.pmc[2] * 8, slots), // FE BW - FE latency bound
                        FormatPercentage(counterData.pmc[3] - counterData.pmc[4], slots), // BE bound - BE mem bound
                        FormatPercentage(counterData.pmc[4], slots), // BE mem bound
                        FormatPercentage(counterData.pmc[5], slots)
                };
            }
        }

        public class PCoreReadEvt : MonitoringConfig
        {
            private ArrowLake cpu;
            private CoreType coreType;
            public string GetConfigName() { return "P Cores: Read Events"; }

            public PCoreReadEvt(ArrowLake intelCpu)
            {
                cpu = intelCpu;
                foreach (CoreType type in cpu.coreTypes)
                {
                    if (type.Type == ADL_P_CORE_TYPE)
                    {
                        coreType = type;
                        break;
                    }
                }

                this.columns = new string[coreType.PmcCounters + 1];
                this.columns[0] = "Core";
                for (int i = 0; i < coreType.PmcCounters; i++) this.columns[i + 1] = "PMC" + i;
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[coreType.CoreCount][];
                cpu.InitializeCoreTotals();
                int pCoreIdx = 0;
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    if (((coreType.CoreMask >> threadIdx) & 0x1) != 0x1)
                        continue;

                    string[] unitMetrics = new string[coreType.PmcCounters + 1];
                    unitMetrics[0] = "P Core " + threadIdx;
                    for (byte pmcIdx = 0; pmcIdx < this.coreType.PmcCounters; pmcIdx++)
                    {
                        Ring0.ReadMsr(IA32_PERFEVTSEL[pmcIdx], out ulong eventSelect);
                        unitMetrics[pmcIdx + 1] = string.Format("{0:X}", eventSelect);
                    }

                    results.unitMetrics[pCoreIdx] = unitMetrics;
                    if (pCoreIdx == 0) results.overallMetrics = unitMetrics;
                    pCoreIdx++;
                }

                cpu.ReadPackagePowerCounter();
                return results;
            }

            public string[] columns;

            public string GetHelpText()
            {
                return "Debugging config for reading raw perf counter values";
            }
        }
        #endregion

        public class ECoreTopDown : MonitoringConfig
        {
            private ArrowLake cpu;
            private CoreType coreType;
            public string GetConfigName() { return "E Cores: Top Down"; }

            public ECoreTopDown(ArrowLake intelCpu)
            {
                cpu = intelCpu;
                foreach (CoreType type in cpu.coreTypes)
                {
                    if (type.Type == ADL_E_CORE_TYPE)
                    {
                        coreType = type;
                        break;
                    }
                }
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.DisablePerformanceCounters();

                // 0x71, 0x72: frontend latency
                // 0x71, 0x8d: frontend bandwidth
                // 0x73, 0x00: bad speculation slots
                // 0xa4, 0x02: backend bound (all)
                // 0xc2, 0x02: retiring
                // cmask retire blocked
                // 0x05, 0xf4: l1 bound at retire (oldest load blocked, store addr match, dtlb miss/page walk)
                // 0x05, 0xff: oldest load of load buffer stalled for any reason
                ulong[] pmc = new ulong[8];
                pmc[0] = GetPerfEvtSelRegisterValue(0x71, 0x72); // frontend latency
                pmc[1] = GetPerfEvtSelRegisterValue(0x71, 0x8D); // frontend bandwidth
                pmc[2] = GetPerfEvtSelRegisterValue(0x73, 0); // bad speculation
                pmc[3] = GetPerfEvtSelRegisterValue(0xA4, 2); // backend bound
                pmc[4] = GetPerfEvtSelRegisterValue(0xC2, 2); // retiring
                pmc[5] = GetPerfEvtSelRegisterValue(0xC2, 2, cmask: 1, invert: true); // retire blocked cycles
                pmc[6] = GetPerfEvtSelRegisterValue(5, 0xFF); // load blocking retire cycles
                pmc[7] = GetPerfEvtSelRegisterValue(5, 0x81); // load blocking retire, l1 miss
                cpu.ProgramPerfCounters(pmc, coreType.Type);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[coreType.CoreCount][];
                cpu.InitializeCoreTotals();
                int pCoreIdx = 0;
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    if (((coreType.CoreMask >> threadIdx) & 0x1) != 0x1)
                        continue;
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[pCoreIdx] = computeMetrics(coreType.Name + " " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                    pCoreIdx++;
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues(new String[] {
                   "Frontnd Latency", "Frontend Bandwidth", "Bad Speculation", "Backend Bound", "Retiring", "No retire cycles", "Retire blocked by load", "Retire blocked by l1 miss load"});
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "PkgPower",
                "Bad Speculation", "FE Latency", "FE BW", "BE Core Bound", "BE Mem Bound", "Retiring", "Load Blocking Ret", "L1D Miss Load Blocking Ret"};

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float slots = counterData.activeCycles * 8;
                float loadBlockPct = counterData.pmc[6] / counterData.pmc[5];
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatPercentage(counterData.pmc[2], slots), // Bad spec
                        FormatPercentage(counterData.pmc[0], slots), // FE latency
                        FormatPercentage(counterData.pmc[1], slots), // FE BW
                        FormatPercentage(counterData.pmc[3] * (1 - loadBlockPct), slots), // Backend Bound, Core
                        FormatPercentage(counterData.pmc[3] * loadBlockPct, slots), // Backend Bound, Mem
                        FormatPercentage(counterData.pmc[4], slots), // retiring
                        FormatPercentage(counterData.pmc[6], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[7], counterData.activeCycles)
                };
            }
        }

        public class ECoreBranch : MonitoringConfig
        {
            private ArrowLake cpu;
            private CoreType coreType;
            public string GetConfigName() { return "E Cores: Branch"; }

            public ECoreBranch(ArrowLake intelCpu)
            {
                cpu = intelCpu;
                foreach (CoreType type in cpu.coreTypes)
                {
                    if (type.Type == ADL_E_CORE_TYPE)
                    {
                        coreType = type;
                        break;
                    }
                }
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.DisablePerformanceCounters();

                // 0xE6, umask 1: baclear
                // 0x71, umask 2: branch detect slots
                // 0x71, umask 0x40: branch resteer (btclear)
                // 0x73, umask 4: mispredict slots
                // 0xC4, umask 0: all branches
                // 0xC5, umask 0: all mispredicted branches
                ulong[] pmc = new ulong[8];
                pmc[0] = GetPerfEvtSelRegisterValue(0xC4, 0); // branches
                pmc[1] = GetPerfEvtSelRegisterValue(0xC5, 0); // mispredicted branches
                pmc[2] = GetPerfEvtSelRegisterValue(0xE6, 1); // BAClear
                pmc[3] = GetPerfEvtSelRegisterValue(0x71, 0x40); // baclear slots
                pmc[4] = GetPerfEvtSelRegisterValue(0x71, 2); // btclear slots
                pmc[5] = GetPerfEvtSelRegisterValue(0x73, 2, cmask: 1); //mispredict slots
                pmc[6] = GetPerfEvtSelRegisterValue(0x71, 0x40, cmask: 1); // baclear cycles
                pmc[7] = GetPerfEvtSelRegisterValue(0x71, 0x40, cmask: 1, edge: true); // baclear occurrences
                cpu.ProgramPerfCounters(pmc, coreType.Type);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[coreType.CoreCount][];
                cpu.InitializeCoreTotals();
                int pCoreIdx = 0;
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    if (((coreType.CoreMask >> threadIdx) & 0x1) != 0x1)
                        continue;
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[pCoreIdx] = computeMetrics(coreType.Name + " " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                    pCoreIdx++;
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues(new String[] {
                   "Branches", "Mispredicted branches", "BAClear", "BAClear Slots", "BTClear slots", "Mispredict slots", "BAClear Slots cmask 1", "BAClear Slots cmask 1 edge"});
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "PkgPower",
                "BPU Accuracy", "BPU MPKI", "BAClear/Ki", "BAClear Slots", "BAClear Duration", "BTClear Slots"};

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float slots = counterData.activeCycles * 8;
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatPercentage(counterData.pmc[0] - counterData.pmc[1], counterData.pmc[0]),
                        string.Format("{0:F2}", 1000 * counterData.pmc[1] / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.pmc[2] / counterData.instr),
                        FormatPercentage(counterData.pmc[3], counterData.activeCycles * 8),
                        string.Format("{0:F2} clks", counterData.pmc[6] / counterData.pmc[7]),
                        FormatPercentage(counterData.pmc[4], counterData.activeCycles * 8),
                };
            }
        }

        public class ECoreIFetch : MonitoringConfig
        {
            private ArrowLake cpu;
            private CoreType coreType;
            public string GetConfigName() { return "E Cores: FE Latency"; }

            public ECoreIFetch(ArrowLake intelCpu)
            {
                cpu = intelCpu;
                foreach (CoreType type in cpu.coreTypes)
                {
                    if (type.Type == ADL_E_CORE_TYPE)
                    {
                        coreType = type;
                        break;
                    }
                }
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.DisablePerformanceCounters();

                // 0x80, 0x1: ic hit
                // 0x80, 0x2: ic miss
                // 0x35, 0x1: L1i stall cycles, l2 hit
                // 0x35, 0x6: L1i stall cycles, llc hit
                // 0x35, 0x78: L1i stall cycles, llc miss?
                // 0x71, 0x4: issue slots lost, ic miss
                // 0x71, 0x10: issue slots lost, itlb miss
                // unused
                ulong[] pmc = new ulong[8];
                pmc[0] = GetPerfEvtSelRegisterValue(0x80, 1); // ic hit
                pmc[1] = GetPerfEvtSelRegisterValue(0x80, 2); // ic miss
                pmc[2] = GetPerfEvtSelRegisterValue(0x35, 1); // stall fe l2 hit
                pmc[3] = GetPerfEvtSelRegisterValue(0x35, 6); // stall fe llc hit
                pmc[4] = GetPerfEvtSelRegisterValue(0x35, 0x78); // stall fe llc miss
                pmc[5] = GetPerfEvtSelRegisterValue(0x71, 4); // issue slots lost, ic miss
                pmc[6] = GetPerfEvtSelRegisterValue(0x71, 0x10); // issue slots lost, itlb miss
                pmc[7] = 0;
                cpu.ProgramPerfCounters(pmc, coreType.Type);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[coreType.CoreCount][];
                cpu.InitializeCoreTotals();
                int pCoreIdx = 0;
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    if (((coreType.CoreMask >> threadIdx) & 0x1) != 0x1)
                        continue;
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[pCoreIdx] = computeMetrics(coreType.Name + " " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                    pCoreIdx++;
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues(new String[] {
                   "IC Hit", "IC Miss", "L2 FE Mem Bound Core Stall Cycles", "LLC FE Mem Bound Core Stall Cycles", "LLC Miss FE Mem Bound Core Stall Cycles", "IC Miss Slots", "iTLB Miss Slots"});
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "PkgPower",
                "IC Hitrate", "IC MPKI", "FE L2 Bound", "FE LLC Bound", "FE LLC Miss Bound", "IC Miss TD", "iTLB Miss TD"};

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float slots = counterData.activeCycles * 8;
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatPercentage(counterData.pmc[0], counterData.pmc[0] + counterData.pmc[1]),
                        string.Format("{0:F2}", 1000 *counterData.pmc[1] / counterData.instr),
                        FormatPercentage(counterData.pmc[2], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[3], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[4], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[5], counterData.activeCycles * 8),
                        FormatPercentage(counterData.pmc[6], counterData.activeCycles * 8)
                };
            }
        }

        public class ECoreDecode : MonitoringConfig
        {
            private ArrowLake cpu;
            private CoreType coreType;
            public string GetConfigName() { return "E Cores: Decode"; }

            public ECoreDecode(ArrowLake intelCpu)
            {
                cpu = intelCpu;
                foreach (CoreType type in cpu.coreTypes)
                {
                    if (type.Type == ADL_E_CORE_TYPE)
                    {
                        coreType = type;
                        break;
                    }
                }
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.DisablePerformanceCounters();

                // 0x71, 0x4: slots lost to predecode wrong
                // 0x71, 0x8: slots lost to decode stalls
                // 0x71, 0x1: slots lost to microcode sequencer
                // 0x71, 0x80: slots lost to other misc reasons
                ulong[] pmc = new ulong[8];
                pmc[0] = GetPerfEvtSelRegisterValue(0x71, 4); // predecode wrong
                pmc[1] = GetPerfEvtSelRegisterValue(0x71, 8); // decode stall
                pmc[2] = GetPerfEvtSelRegisterValue(0x71, 1); // microcode sequencer (CISC)
                pmc[3] = GetPerfEvtSelRegisterValue(0x71, 0x80); // misc
                cpu.ProgramPerfCounters(pmc, coreType.Type);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[coreType.CoreCount][];
                cpu.InitializeCoreTotals();
                int pCoreIdx = 0;
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    if (((coreType.CoreMask >> threadIdx) & 0x1) != 0x1)
                        continue;
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[pCoreIdx] = computeMetrics(coreType.Name + " " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                    pCoreIdx++;
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues(new String[] {
                   "Predecode Wrong Slots", "Deocde Stall Slots", "Microcode Sequencer Slots", "Misc Slots"});
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "PkgPower",
                "Predecode Wrong", "Decode Stall", "MS", "Misc"};

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float slots = counterData.activeCycles * 8;
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatPercentage(counterData.pmc[0], slots),
                        FormatPercentage(counterData.pmc[1], slots),
                        FormatPercentage(counterData.pmc[2], slots),
                        FormatPercentage(counterData.pmc[3], slots)
                };
            }
        }

        public class ECoreDispatchStall : MonitoringConfig
        {
            private ArrowLake cpu;
            private CoreType coreType;
            public string GetConfigName() { return "E Cores: Dispatch Stall"; }

            public ECoreDispatchStall(ArrowLake intelCpu)
            {
                cpu = intelCpu;
                foreach (CoreType type in cpu.coreTypes)
                {
                    if (type.Type == ADL_E_CORE_TYPE)
                    {
                        coreType = type;
                        break;
                    }
                }
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.DisablePerformanceCounters();

                ulong[] pmc = new ulong[8];
                pmc[0] = GetPerfEvtSelRegisterValue(0x74, 0x40); // reorder buffer
                pmc[1] = GetPerfEvtSelRegisterValue(0x74, 0x20); // register files
                pmc[2] = GetPerfEvtSelRegisterValue(0x74, 8); // non-mem scheduler
                pmc[3] = GetPerfEvtSelRegisterValue(0x74, 2); // mem scheduler
                pmc[4] = GetPerfEvtSelRegisterValue(0x74, 1); // alloc restrictions
                pmc[5] = GetPerfEvtSelRegisterValue(0x74, 0x10); // serialization
                pmc[6] = GetPerfEvtSelRegisterValue(0x4, 0x2); // load buffer (cycles)
                pmc[7] = GetPerfEvtSelRegisterValue(0x4, 0x1); // store buffer full (cycles)
                cpu.ProgramPerfCounters(pmc, coreType.Type);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[coreType.CoreCount][];
                cpu.InitializeCoreTotals();
                int pCoreIdx = 0;
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    if (((coreType.CoreMask >> threadIdx) & 0x1) != 0x1)
                        continue;
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[pCoreIdx] = computeMetrics(coreType.Name + " " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                    pCoreIdx++;
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues(new String[] {
                   "ROB Slots", "Register File Slots", "Non-Mem Scheduler Slots", "Mem Scheduler Slots", "Alloc Restriction Slots",
                   "Serialization Slots", "Load Buffer Full Cycles", "Store Buffer Full Cycles"});
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "PkgPower",
                "ROB", "Registers", "Mon-Mem Scheduler", "Mem Scheduler", "Alloc Restriction", "Serialization", "LB Full", "SB Full"};

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float slots = counterData.activeCycles * 8;
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatPercentage(counterData.pmc[0], slots),
                        FormatPercentage(counterData.pmc[1], slots),
                        FormatPercentage(counterData.pmc[2], slots),
                        FormatPercentage(counterData.pmc[3], slots),
                        FormatPercentage(counterData.pmc[4], slots),
                        FormatPercentage(counterData.pmc[5], slots),
                        FormatPercentage(counterData.pmc[6], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[7], counterData.activeCycles),
                };
            }
        }

        public class ECoreMemBound : MonitoringConfig
        {
            private ArrowLake cpu;
            private CoreType coreType;
            public string GetConfigName() { return "E Cores: Mem Bound"; }

            public ECoreMemBound(ArrowLake intelCpu)
            {
                cpu = intelCpu;
                foreach (CoreType type in cpu.coreTypes)
                {
                    if (type.Type == ADL_E_CORE_TYPE)
                    {
                        coreType = type;
                        break;
                    }
                }
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.DisablePerformanceCounters();

                ulong[] pmc = new ulong[8];
                pmc[0] = GetPerfEvtSelRegisterValue(0x5, 0x90); // retire stalled DTLB miss
                pmc[1] = GetPerfEvtSelRegisterValue(0x5, 0x81); // L1 miss retire stalled
                pmc[2] = GetPerfEvtSelRegisterValue(0x5, 0x84); // store address match retire blocked
                pmc[3] = GetPerfEvtSelRegisterValue(0x5, 0xA0); // page walk retire blocked
                pmc[4] = GetPerfEvtSelRegisterValue(0x5, 0x82); // wcb full retire blocked
                pmc[5] = GetPerfEvtSelRegisterValue(0x31, 0); // l2q reject
                pmc[6] = GetPerfEvtSelRegisterValue(0x30, 0); // xq reject
                pmc[7] = GetPerfEvtSelRegisterValue(0x5, 0x81, edge: true); // L1 miss retire stalled
                cpu.ProgramPerfCounters(pmc, coreType.Type);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[coreType.CoreCount][];
                cpu.InitializeCoreTotals();
                int pCoreIdx = 0;
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    if (((coreType.CoreMask >> threadIdx) & 0x1) != 0x1)
                        continue;
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[pCoreIdx] = computeMetrics(coreType.Name + " " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                    pCoreIdx++;
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues(new String[] {
                   "DTLB Miss", "L1 Miss", "Store Address Match", "Page Walk", "WCB Full", "L2Q Reject", "XQ Reject", "L1 Miss Edge"});
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "PkgPower",
                "DTLB Miss", "L1 Miss", "Store Address Match", "Page Walk", "WCB Full", "L2Q Reject", "XQ Reject", "L1 Miss Stall Duration"};

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float slots = counterData.activeCycles * 8;
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatPercentage(counterData.pmc[0], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[1], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[2], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[3], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[4], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[5], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[6], counterData.activeCycles),
                        string.Format("{0:F2} clks", counterData.pmc[0] / counterData.pmc[7])
                };
            }
        }

        public class ECoreMemBound1 : MonitoringConfig
        {
            private ArrowLake cpu;
            private CoreType coreType;
            public string GetConfigName() { return "E Cores: Mem Bound 1"; }

            public ECoreMemBound1(ArrowLake intelCpu)
            {
                cpu = intelCpu;
                foreach (CoreType type in cpu.coreTypes)
                {
                    if (type.Type == ADL_E_CORE_TYPE)
                    {
                        coreType = type;
                        break;
                    }
                }
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.DisablePerformanceCounters();

                ulong[] pmc = new ulong[8];
                pmc[0] = GetPerfEvtSelRegisterValue(0x34, 0x7F); // L1 demand load miss (all)
                pmc[1] = GetPerfEvtSelRegisterValue(0x34, 0x1); // L2 hit
                pmc[2] = GetPerfEvtSelRegisterValue(0x34, 0x6); // LLC Hit
                pmc[3] = GetPerfEvtSelRegisterValue(0x34, 0x78); // LLC Miss
                pmc[4] = GetPerfEvtSelRegisterValue(0x34, 0x80); // store buffer full
                pmc[5] = GetPerfEvtSelRegisterValue(0x34, 0x1, edge: true); // l2 hit bound edge
                pmc[6] = GetPerfEvtSelRegisterValue(0x34, 0x6, edge: true); // llc hit bound edge
                pmc[7] = GetPerfEvtSelRegisterValue(0x34, 0x78, edge: true); // llc miss bound edge
                cpu.ProgramPerfCounters(pmc, coreType.Type);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[coreType.CoreCount][];
                cpu.InitializeCoreTotals();
                int pCoreIdx = 0;
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    if (((coreType.CoreMask >> threadIdx) & 0x1) != 0x1)
                        continue;
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[pCoreIdx] = computeMetrics(coreType.Name + " " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                    pCoreIdx++;
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues(new String[] {
                   "All L1D Demand Miss Bound Clks", "L2 Hit Bound", "LLC Hit Bound", "LLC Miss Bound", "Store Buffer Full", "L2 Hit Bound Edge", "LLC Hit Bound Edge", "LLC Miss Bound Edge"});
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "PkgPower",
                "L1D Miss", "L2", "L3", "L3 Miss", "L2 Bound Duration", "L3 Bound Duration", "L3 Miss Bound Duration" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float slots = counterData.activeCycles * 8;
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatPercentage(counterData.pmc[0], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[1], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[2], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[3], counterData.activeCycles),
                        string.Format("{0:F2} clk", counterData.pmc[1] / counterData.pmc[5]),
                        string.Format("{0:F2} clk", counterData.pmc[2] / counterData.pmc[6]),
                        string.Format("{0:F2} clk", counterData.pmc[3] / counterData.pmc[7]),
                };
            }
        }

        public class ECoreLoadData : MonitoringConfig
        {
            private ArrowLake cpu;
            private CoreType coreType;
            public string GetConfigName() { return "E Cores: Load Data Source"; }

            public ECoreLoadData(ArrowLake intelCpu)
            {
                cpu = intelCpu;
                foreach (CoreType type in cpu.coreTypes)
                {
                    if (type.Type == ADL_E_CORE_TYPE)
                    {
                        coreType = type;
                        break;
                    }
                }
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.DisablePerformanceCounters();

                ulong[] pmc = new ulong[8];
                pmc[0] = GetPerfEvtSelRegisterValue(0xD1, 1); // l1 hit
                pmc[1] = GetPerfEvtSelRegisterValue(0xD1, 2); // l2 hit
                pmc[2] = GetPerfEvtSelRegisterValue(0xD1, 0x1C); // l3 hit
                pmc[3] = GetPerfEvtSelRegisterValue(0xD0, 0x81); // load uops
                pmc[4] = GetPerfEvtSelRegisterValue(0xD1, 0x80); // l2 miss
                pmc[5] = GetPerfEvtSelRegisterValue(0xD1, 0x40); // l1 miss
                pmc[6] = GetPerfEvtSelRegisterValue(0xD0, 0x11); // loads that missed the STLB
                pmc[7] = GetPerfEvtSelRegisterValue(0xD1, 1 << 5); // load hit WCB
                cpu.ProgramPerfCounters(pmc, coreType.Type);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[coreType.CoreCount][];
                cpu.InitializeCoreTotals();
                int pCoreIdx = 0;
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    if (((coreType.CoreMask >> threadIdx) & 0x1) != 0x1)
                        continue;
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[pCoreIdx] = computeMetrics(coreType.Name + " " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                    pCoreIdx++;
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues(new String[] {
                   "L1 hit", "L2 hit", "L3 hit", "Load Uops Retired", "L2 Miss", "L1 Miss", "STLB miss", "WCB hit" });
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "PkgPower",
                "L1 Hitrate", "L2 Hitrate", "L3 Hitrate", "L1 MPKI", "L2 MPKI", "L3 MPKI", "STLB MPKI",};

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float slots = counterData.activeCycles * 8;
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatPercentage(counterData.pmc[0], counterData.pmc[3]),
                        FormatPercentage(counterData.pmc[1], counterData.pmc[3] - counterData.pmc[0]),
                        FormatPercentage(counterData.pmc[2], counterData.pmc[3] - counterData.pmc[0] - counterData.pmc[1]),
                        string.Format("{0:F2}", 1000 * (counterData.pmc[3] - counterData.pmc[0]) / counterData.instr),
                        string.Format("{0:F2}", 1000 * (counterData.pmc[3] - counterData.pmc[0] - counterData.pmc[1]) / counterData.instr),
                        string.Format("{0:F2}", 1000 * (counterData.pmc[3] - counterData.pmc[0] - counterData.pmc[1] - counterData.pmc[2]) / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.pmc[6] / counterData.instr)
                };
            }
        }

        public class ECoreMachineClear : MonitoringConfig
        {
            private ArrowLake cpu;
            private CoreType coreType;
            public string GetConfigName() { return "E Cores: Machine Clear"; }

            public ECoreMachineClear(ArrowLake intelCpu)
            {
                cpu = intelCpu;
                foreach (CoreType type in cpu.coreTypes)
                {
                    if (type.Type == ADL_E_CORE_TYPE)
                    {
                        coreType = type;
                        break;
                    }
                }
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.DisablePerformanceCounters();

                ulong[] pmc = new ulong[8];
                pmc[0] = GetPerfEvtSelRegisterValue(0xC3, 1); // SMC
                pmc[1] = GetPerfEvtSelRegisterValue(0xC3, 2); // memory ordering (snoop)
                pmc[2] = GetPerfEvtSelRegisterValue(0xC3, 4); // fp assist
                pmc[3] = GetPerfEvtSelRegisterValue(0xC3, 8); // mem disambiguation
                pmc[4] = GetPerfEvtSelRegisterValue(0xC3, 0x10); // mrn nuke (memory renaming)
                pmc[5] = GetPerfEvtSelRegisterValue(0xC3, 0x20); // page fault
                pmc[6] = GetPerfEvtSelRegisterValue(0xC3, 0); // any
                pmc[7] = 0;
                cpu.ProgramPerfCounters(pmc, coreType.Type);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[coreType.CoreCount][];
                cpu.InitializeCoreTotals();
                int pCoreIdx = 0;
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    if (((coreType.CoreMask >> threadIdx) & 0x1) != 0x1)
                        continue;
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[pCoreIdx] = computeMetrics(coreType.Name + " " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                    pCoreIdx++;
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues(new String[] {
                   "SMC", "Memory Ordering (Snoop)", "FP Assist", "Mem Disambiguation", "MRN Nuke", "Page Fault", "Any" });
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "PkgPower",
                 "SMC", "Memory Ordering (Snoop)", "FP Assist", "Mem Disambiguation", "MRN Nuke", "Page Fault", "Any"};

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float slots = counterData.activeCycles * 8;
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        string.Format("{0:F2}", counterData.pmc[0] * 1000 / counterData.instr),
                        string.Format("{0:F2}", counterData.pmc[1] * 1000 / counterData.instr),
                        string.Format("{0:F2}", counterData.pmc[2] * 1000 / counterData.instr),
                        string.Format("{0:F2}", counterData.pmc[3] * 1000 / counterData.instr),
                        string.Format("{0:F2}", counterData.pmc[4] * 1000 / counterData.instr),
                        string.Format("{0:F2}", counterData.pmc[5] * 1000 / counterData.instr),
                        string.Format("{0:F2}", counterData.pmc[6] * 1000 / counterData.instr),
                };
            }
        }

        public class ECoreSerialization : MonitoringConfig
        {
            private ArrowLake cpu;
            private CoreType coreType;
            public string GetConfigName() { return "E Cores: Serialization"; }

            public ECoreSerialization(ArrowLake intelCpu)
            {
                cpu = intelCpu;
                foreach (CoreType type in cpu.coreTypes)
                {
                    if (type.Type == ADL_E_CORE_TYPE)
                    {
                        coreType = type;
                        break;
                    }
                }
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.DisablePerformanceCounters();

                ulong[] pmc = new ulong[8];
                pmc[0] = GetPerfEvtSelRegisterValue(0x75, 1); // IQ_JEU (jump scoreboard). LFENCE and MFENCE
                pmc[1] = GetPerfEvtSelRegisterValue(0x75, 2); // NON_C01_MS_SCB Non-C01 (umwait/tpause) microcode sequencer scoreboard
                pmc[2] = GetPerfEvtSelRegisterValue(0x75, 4); // C01_MS_SCB: UMWAIT/TPAUSE
                pmc[3] = GetPerfEvtSelRegisterValue(0xE7, 4); // MS Busy
                pmc[4] = GetPerfEvtSelRegisterValue(0xC3, 0x10); // mrn nuke (memory renaming)
                pmc[5] = GetPerfEvtSelRegisterValue(0xC3, 0x20); // page fault
                pmc[6] = GetPerfEvtSelRegisterValue(0xC3, 0); // any
                pmc[7] = 0;
                cpu.ProgramPerfCounters(pmc, coreType.Type);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[coreType.CoreCount][];
                cpu.InitializeCoreTotals();
                int pCoreIdx = 0;
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    if (((coreType.CoreMask >> threadIdx) & 0x1) != 0x1)
                        continue;
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[pCoreIdx] = computeMetrics(coreType.Name + " " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                    pCoreIdx++;
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues(new String[] {
                   "IQ_JEU", "NON_C01_MS_SCB", "C01_MS_SCB", "MS Busy", "Unused", "Unused", "Unused", "Unused" });
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "PkgPower",
                 "IQ_JEU", "NON_C01_MS_SCB", "C01_MS_SCB", "MS Busy" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float slots = counterData.activeCycles * 8;
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatPercentage(counterData.pmc[0], slots),
                        FormatPercentage(counterData.pmc[1], slots),
                        FormatPercentage(counterData.pmc[2], slots),
                        FormatPercentage(counterData.pmc[3], counterData.activeCycles)
                };
            }
        }
    }
}

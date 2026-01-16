using System;
using System.Collections.Generic;
using PmcReader.Interop;

namespace PmcReader.Intel
{
    public class AlderLake : ModernIntelCpu
    {
        public static byte ADL_P_CORE_TYPE = 0x40;
        public static byte ADL_E_CORE_TYPE = 0x20;

        public AlderLake()
        {
            List<MonitoringConfig> configs = new List<MonitoringConfig>();
            architectureName = "Alder Lake";
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
                    coreTypes[coreIdx].AllocWidth = 5;
                    coreTypes[coreIdx].Name = "E-Core";
                }
            }

            // Create supported configs
            configs.Add(new ArchitecturalCounters(this));
            configs.Add(new RetireHistogram(this));
            configs.Add(new LoadDataSources(this));
            for (byte coreIdx = 0; coreIdx < coreTypes.Length; coreIdx++)
            {
                if (coreTypes[coreIdx].Type == ADL_P_CORE_TYPE)
                {
                    coreTypes[coreIdx].Name = "P-Core";
                    configs.Add(new PCoreVector(this));
                    configs.Add(new PCorePowerLicense(this));
                }
                if (coreTypes[coreIdx].Type == ADL_E_CORE_TYPE)
                {
                    configs.Add(new ECoresTopDown(this));
                    configs.Add(new ECoresMemExec(this));
                    configs.Add(new ECoresFEBound(this));
                    configs.Add(new ECoresBadSpec(this));
                    configs.Add(new ECoresBackendBound(this));
                    configs.Add(new ECoresLdHead(this));
                }
            }
            monitoringConfigs = configs.ToArray();
        }

        public class PCoreVector : MonitoringConfig
        {
            private AlderLake cpu;
            private CoreType coreType;
            public string GetConfigName() { return "P Cores: Vector Instrs"; }

            public PCoreVector(AlderLake intelCpu)
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
                pmc[0] = GetPerfEvtSelRegisterValue(0xE7, 0x13); // vec128retired
                pmc[1] = GetPerfEvtSelRegisterValue(0xE7, 0xAC); // vec256retired
                pmc[2] = GetPerfEvtSelRegisterValue(0xC7, 0x08); // fp128ps_retired
                pmc[3] = GetPerfEvtSelRegisterValue(0xC7, 0x04); // fp128pd_retired
                pmc[4] = GetPerfEvtSelRegisterValue(0xC7, 0x20); // fp256ps_retired
                pmc[5] = GetPerfEvtSelRegisterValue(0xC7, 0x10); // fp256pd_retired
                pmc[6] = GetPerfEvtSelRegisterValue(0xC7, 0x02); // ss_retired
                pmc[7] = GetPerfEvtSelRegisterValue(0xC7, 0x01); // sd_retired
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
                results.overallCounterValues = cpu.GetOverallCounterValues( new String[] {
                    "int 128-bit vec", "int 256-bit vec", "128-bit fp32", "128-bit fp64", "256-bit FP32", "256-bit FP64", "Scalar FP32", "Scalar FP64" });
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "PkgPower", "Instr/Watt", 
                "128-bit Vec Int", "256-bit Vec Int", "128-bit FP32", "128-bit FP64", "256-bit FP32", "256-bit FP64", "Scalar FP32", "Scalar FP64" };

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
                        FormatPercentage(counterData.pmc[0], counterData.instr),
                        FormatPercentage(counterData.pmc[1], counterData.instr),
                        FormatPercentage(counterData.pmc[2], counterData.instr),
                        FormatPercentage(counterData.pmc[3], counterData.instr),
                        FormatPercentage(counterData.pmc[4], counterData.instr),
                        FormatPercentage(counterData.pmc[5], counterData.instr),
                        FormatPercentage(counterData.pmc[6], counterData.instr),
                        FormatPercentage(counterData.pmc[7], counterData.instr),
                };
            }
        }

        public class PCorePowerLicense : MonitoringConfig
        {
            private AlderLake cpu;
            private CoreType coreType;
            public string GetConfigName() { return "P Cores: Power State/License"; }

            public PCorePowerLicense(AlderLake intelCpu)
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

                ulong[] pmc = new ulong[7];
                pmc[0] = GetPerfEvtSelRegisterValue(0x28, 0x02); // license1
                pmc[1] = GetPerfEvtSelRegisterValue(0x28, 0x04); // license2
                pmc[2] = GetPerfEvtSelRegisterValue(0x28, 0x08); // license3
                pmc[3] = GetPerfEvtSelRegisterValue(0xEC, 0x10); // c01State
                pmc[4] = GetPerfEvtSelRegisterValue(0xEC, 0x20); // c02State
                pmc[5] = GetPerfEvtSelRegisterValue(0x3C, 0x02); // oneThread
                pmc[6] = GetPerfEvtSelRegisterValue(0xEC, 0x40); // pause
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
                results.overallCounterValues = cpu.GetOverallCounterValues(new String[] { "License 1", "License 2", "License 3", "C0.1 State", "C0.2 State", "1T Active", "Paused", "Unused" });
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "PkgPower", "Instr/Watt",
                "License 0", "License 1", "License 2", "C0.1 Pwr Save State", "C0.2 Pwr Save State", "Single Thread Active", "Pause" };

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
                        FormatPercentage(counterData.pmc[6], counterData.activeCycles)
                };
            }
        }

        public class ECoresMemExec : MonitoringConfig
        {
            private AlderLake cpu;
            private CoreType coreType;
            public string GetConfigName() { return "E Cores: Memory Execution"; }

            public ECoresMemExec(AlderLake intelCpu)
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

                ulong[] pmc = new ulong[5];
                pmc[0] = GetPerfEvtSelRegisterValue(0x4, 0x2, true, true, false, false, false, false, true, false, 0); // loadBufferFUll
                pmc[1] = GetPerfEvtSelRegisterValue(0x4, 0x4, true, true, false, false, false, false, true, false, 0); // memRsFull
                pmc[2] = GetPerfEvtSelRegisterValue(0x4, 0x1, true, true, false, false, false, false, true, false, 0); // storeBufferFull

                // 4K alias check
                pmc[3] = GetPerfEvtSelRegisterValue(0x3, 0x4, true, true, false, false, false, false, true, false, 0); // addr alias
                pmc[4] = GetPerfEvtSelRegisterValue(0x3, 0x1, true, true, false, false, false, false, true, false, 0); // data unknown
                cpu.ProgramPerfCounters(pmc, coreType.Type);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[coreType.CoreCount][];
                cpu.InitializeCoreTotals();
                int eCoreIdx = 0;
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    if (((coreType.CoreMask >> threadIdx) & 0x1) != 0x1)
                        continue;
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[eCoreIdx] = computeMetrics(coreType.Name + " " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                    eCoreIdx++;
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues(new String[] { "Load Buffer Full", "Mem RS Full", "Store Buffer Full", "LD Block 4K Alias", "LD Block Data Unknown", "Unused" });
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "PkgPower", "Instr/Watt",
                "Load Buffer Full", "Store Buffer Full", "Mem Scheduler Full", "Load blocked, 4K alias/Ki", "Load blocked, dependent store data unavailable/Ki" };

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
                        FormatPercentage(counterData.pmc[0], counterData.pmc[0] + counterData.pmc[1]),
                        string.Format("{0:F2}", 1000 * counterData.pmc[1] / counterData.instr),
                        FormatPercentage(counterData.pmc[0], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[1], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[2], counterData.activeCycles),
                        string.Format("{0:F2}", 1000 * counterData.pmc[3] / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.pmc[4] / counterData.instr),
                };
            }
        }

        public class ECoresBackendBound : MonitoringConfig
        {
            private AlderLake cpu;
            private CoreType coreType;

            public string GetConfigName() { return "E Cores: Backend Bound"; }

            public ECoresBackendBound(AlderLake intelCpu)
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

                ulong[] pmc = new ulong[6];
                pmc[0] = GetPerfEvtSelRegisterValue(0x74, 0x01); // tdAllocRestriction
                pmc[1] = GetPerfEvtSelRegisterValue(0x74, 0x02); // tdMemSched
                pmc[2] = GetPerfEvtSelRegisterValue(0x74, 0x08); // tdNonMemSched
                pmc[3] = GetPerfEvtSelRegisterValue(0x74, 0x20); // tdReg
                pmc[4] = GetPerfEvtSelRegisterValue(0x74, 0x40); // tdRob
                pmc[5] = GetPerfEvtSelRegisterValue(0x74, 0x10); // tdSerialization
                cpu.ProgramPerfCounters(pmc, coreType.Type);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[coreType.CoreCount][];
                cpu.InitializeCoreTotals();
                int eCoreIdx = 0;
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    if(((coreType.CoreMask >> threadIdx) & 0x1) != 0x1)
                            continue;
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[eCoreIdx] = computeMetrics(coreType.Name + " " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                    eCoreIdx++;
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues( new String[] {
                    "Allocation Restriction", "Mem Scheduler Full", "Non-mem Scheduler Full", "Registers Full", "ROB Full", "Serialization", "BE Bound" });
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "PkgPower", "Instr/Watt",
                "Alloc Restriction", "Mem Scheduler Full", "Non-mem Scheduler Full", "Registers Full", "ROB Full", "Serialization", "BE Bound" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float slots = counterData.activeCycles * coreType.AllocWidth;
                float be_bound = counterData.pmc[0] + counterData.pmc[1] + counterData.pmc[2] + counterData.pmc[3] + counterData.pmc[4] + counterData.pmc[5];
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatLargeNumber(counterData.instr / counterData.packagePower),
                        FormatPercentage(counterData.pmc[0], slots),
                        FormatPercentage(counterData.pmc[1], slots),
                        FormatPercentage(counterData.pmc[2], slots),
                        FormatPercentage(counterData.pmc[3], slots),
                        FormatPercentage(counterData.pmc[4], slots),
                        FormatPercentage(counterData.pmc[5], slots),
                        FormatPercentage(be_bound, slots),
                };
            }
        }
        
        public class LoadDataSources : MonitoringConfig
        {
            private AlderLake cpu;
            public string GetConfigName() { return "Retired Data Loads"; }

            public LoadDataSources(AlderLake intelCpu)
            {
                cpu = intelCpu;
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.DisablePerformanceCounters();

                // Pick a set of four events for both the P-Cores and E-Cores
                // Intel annoyingly doesn't match them up. I suspect some umasks are just undocumented, but without a chip to test
                // I'm gonna try to stick with documented unit masks
                // Also super sketchy because Gracemont counts retired uops for these events, while Golden Cove counts instructions
                ulong[] pmc = new ulong[6];
                pmc[0] = GetPerfEvtSelRegisterValue(0xD0, 0x81); // retiredLoads
                pmc[1] = GetPerfEvtSelRegisterValue(0xD1, 0x02); // l2Hit
                pmc[2] = GetPerfEvtSelRegisterValue(0xD1, 0x04); // l3Hit
                pmc[3] = GetPerfEvtSelRegisterValue(0xD1, 0x20); // l3Miss

                foreach (CoreType coreType in cpu.coreTypes)
                {
                    if (coreType.Type == ADL_P_CORE_TYPE)
                    {
                        pmc[4] = GetPerfEvtSelRegisterValue(0, 0); // unused
                        cpu.ProgramPerfCounters(pmc, ADL_P_CORE_TYPE);
                    }
                    if (coreType.Type == ADL_E_CORE_TYPE)
                    {
                        // DRAM hit and L3 miss should be close enough
                        pmc[4] = GetPerfEvtSelRegisterValue(0xD1, 0x08); // dramHit
                        cpu.ProgramPerfCounters(pmc, ADL_E_CORE_TYPE);
                    }
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
                results.overallCounterValues = cpu.GetOverallCounterValues("Retired Loads", "L2 Hit", "L3 Hit", "L3 Miss or DRAM Hit");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "PkgPower", "Instr/Watt",
                "L1/FB Hitrate", "L1/LFB MPKI", "L2 Hitrate", "L2 MPKI", "L3 Hitrate", "L3 MPKI" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                // L1 and load fill buffer hits = loads that were not served from L2, L3, or DRAM
                float l1FbHits = counterData.pmc[0] - counterData.pmc[1] - counterData.pmc[2] - counterData.pmc[3];
                float l2Reqs = counterData.pmc[0] - l1FbHits; // L2 requests = l1 and fill buffer misses
                float l3Reqs = l2Reqs - counterData.pmc[1];
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatLargeNumber(counterData.instr / counterData.packagePower),
                        FormatPercentage(l1FbHits, counterData.pmc[0]),                            // L1 and fill buffer hitrate
                        string.Format("{0:F2}", 1000 * l2Reqs / counterData.instr),              // L1 MPKI
                        FormatPercentage(counterData.pmc[1], l2Reqs),                              // L2 hitrate
                        string.Format("{0:F2}", 1000 * l3Reqs / counterData.instr),              // L2 MPKI
                        FormatPercentage(counterData.pmc[2], l3Reqs),                              // L3 hitrate
                        string.Format("{0:F2}", 1000 * counterData.pmc[3] / counterData.instr)     // L3 MPKI
                };
            }
        }
        public class ECoresTopDown : MonitoringConfig
        {
            private AlderLake cpu;
            private CoreType coreType;

            public string GetConfigName() { return "E Cores: Top Down"; }

            public ECoresTopDown(AlderLake intelCpu)
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

                ulong[] pmc = new ulong[6];
                pmc[0] = GetPerfEvtSelRegisterValue(0x71, 0x8D); // FE Bound / Bandwidth
                pmc[1] = GetPerfEvtSelRegisterValue(0x71, 0x72); // FE Bound / Latency
                pmc[2] = GetPerfEvtSelRegisterValue(0x73, 0x04); // Bad Speculation (Branch)
                pmc[3] = GetPerfEvtSelRegisterValue(0x73, 0xFB); // Bad Speculation (Nuke)
                pmc[4] = GetPerfEvtSelRegisterValue(0x74, 0x00); // BE Bound / All
                pmc[5] = GetPerfEvtSelRegisterValue(0x74, 0x02); // BE Bound / Mem
                cpu.ProgramPerfCounters(pmc, coreType.Type);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[coreType.CoreCount][];
                cpu.InitializeCoreTotals();
                int eCoreIdx = 0;
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    if (((coreType.CoreMask >> threadIdx) & 0x1) != 0x1)
                        continue;
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[eCoreIdx] = computeMetrics(coreType.Name + " " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                    eCoreIdx++;
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues(new String[] {
                    "FE (BW)", "FE (Latency)", "Bad Speculation (BR)", "Bad Speculation (Nuke)", "BE (Non-Mem)", "BE (Mem)", "Retiring" });
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "PkgPower", "Instr/Watt",
                "FE (BW)", "FE (Latency)", "Bad Speculation (BR)", "Bad Speculation (Nuke)", "BE (Non-Mem)", "BE (Mem)", "Retiring" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float slots = counterData.activeCycles * coreType.AllocWidth;
                float nonRetiring = (counterData.pmc[0] + counterData.pmc[1] + counterData.pmc[2] + counterData.pmc[3] + counterData.pmc[4]);
                float retiring = slots - nonRetiring;
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatLargeNumber(counterData.instr / counterData.packagePower),
                        FormatPercentage(counterData.pmc[0], slots),
                        FormatPercentage(counterData.pmc[1], slots),
                        FormatPercentage(counterData.pmc[2], slots),
                        FormatPercentage(counterData.pmc[3], slots),
                        FormatPercentage(counterData.pmc[4] - counterData.pmc[5], slots),
                        FormatPercentage(counterData.pmc[5], slots),
                        FormatPercentage(retiring, slots),
                };
            }
        }

        public class ECoresFEBound: MonitoringConfig
        {
            private AlderLake cpu;
            private CoreType coreType;

            public string GetConfigName() { return "E Cores: Front End Bound"; }

            public ECoresFEBound(AlderLake intelCpu)
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

                ulong[] pmc = new ulong[6];
                pmc[0] = GetPerfEvtSelRegisterValue(0x71, 0x02); // Branch Detect
                pmc[1] = GetPerfEvtSelRegisterValue(0x71, 0x40); // Branch Resteer
                pmc[2] = GetPerfEvtSelRegisterValue(0x71, 0x01); // CISC
                pmc[3] = GetPerfEvtSelRegisterValue(0x71, 0x08); // Decode
                pmc[4] = GetPerfEvtSelRegisterValue(0xC1, 0x20); // Icache
                pmc[5] = GetPerfEvtSelRegisterValue(0xC1, 0x10); // ITLB
                cpu.ProgramPerfCounters(pmc, coreType.Type);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[coreType.CoreCount][];
                cpu.InitializeCoreTotals();
                int eCoreIdx = 0;
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    if (((coreType.CoreMask >> threadIdx) & 0x1) != 0x1)
                        continue;
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[eCoreIdx] = computeMetrics(coreType.Name + " " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                    eCoreIdx++;
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues(new String[] {
                    "Branch Detect", "Branch Resteer", "CISC", "Decode", "Icache Miss", "ITLB Miss" });
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "PkgPower", "Instr/Watt",
                "Branch Detect", "Branch Resteer", "CISC", "Decode", "Icache Miss", "ITLB Miss"};

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float slots = counterData.activeCycles * coreType.AllocWidth;
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatLargeNumber(counterData.instr / counterData.packagePower),
                        FormatPercentage(counterData.pmc[0], slots),
                        FormatPercentage(counterData.pmc[1], slots),
                        FormatPercentage(counterData.pmc[2], slots),
                        FormatPercentage(counterData.pmc[3], slots),
                        FormatPercentage(counterData.pmc[4], slots),
                        FormatPercentage(counterData.pmc[5], slots),
                };
            }
        }

        public class ECoresBadSpec : MonitoringConfig
        {
            private AlderLake cpu;
            private CoreType coreType;

            public string GetConfigName() { return "E Cores: Bad Speculation"; }

            public ECoresBadSpec(AlderLake intelCpu)
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

                ulong[] pmc = new ulong[5];
                pmc[0] = GetPerfEvtSelRegisterValue(0x73, 0x00); // All
                pmc[1] = GetPerfEvtSelRegisterValue(0x73, 0x02); // Fast Nuke
                pmc[2] = GetPerfEvtSelRegisterValue(0x73, 0x03); // Machine Clears (memory ordering/memory disambiguation)
                pmc[3] = GetPerfEvtSelRegisterValue(0x73, 0x04); // Mispredict
                pmc[4] = GetPerfEvtSelRegisterValue(0x73, 0x01); // Nuke
                cpu.ProgramPerfCounters(pmc, coreType.Type);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[coreType.CoreCount][];
                cpu.InitializeCoreTotals();
                int eCoreIdx = 0;
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    if (((coreType.CoreMask >> threadIdx) & 0x1) != 0x1)
                        continue;
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[eCoreIdx] = computeMetrics(coreType.Name + " " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                    eCoreIdx++;
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues(new String[] {
                    "Bad Spec (All)", "Fast Nuke", "Machine Clears", "Mispredict", "Nuke" });
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "PkgPower", "Instr/Watt",
                "Bad Spec (All)", "Fast Nuke", "Machine Clears", "Mispredict", "Nuke" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float slots = counterData.activeCycles * coreType.AllocWidth;
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatLargeNumber(counterData.instr / counterData.packagePower),
                        FormatPercentage(counterData.pmc[0], slots),
                        FormatPercentage(counterData.pmc[1], slots),
                        FormatPercentage(counterData.pmc[2], slots),
                        FormatPercentage(counterData.pmc[3], slots),
                        FormatPercentage(counterData.pmc[4], slots),
                        FormatPercentage(counterData.pmc[5], slots),
                };
            }
        }

        public class ECoresLdHead : MonitoringConfig
        {
            private AlderLake cpu;
            private CoreType coreType;

            public string GetConfigName() { return "E Cores: Load Head"; }

            public ECoresLdHead(AlderLake intelCpu)
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

                ulong[] pmc = new ulong[6];
                pmc[0] = GetPerfEvtSelRegisterValue(0x05, 0xFF); // any
                pmc[1] = GetPerfEvtSelRegisterValue(0x05, 0x83); // Miss
                pmc[2] = GetPerfEvtSelRegisterValue(0x05, 0x84); // stAddr
                pmc[3] = GetPerfEvtSelRegisterValue(0x05, 0x90); // dtlb
                pmc[4] = GetPerfEvtSelRegisterValue(0x05, 0xA0); // pgwalk
                pmc[5] = GetPerfEvtSelRegisterValue(0x05, 0xC0); // Other
                cpu.ProgramPerfCounters(pmc, coreType.Type);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[coreType.CoreCount][];
                cpu.InitializeCoreTotals();
                int eCoreIdx = 0;
                for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                {
                    if (((coreType.CoreMask >> threadIdx) & 0x1) != 0x1)
                        continue;
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[eCoreIdx] = computeMetrics(coreType.Name + " " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                    eCoreIdx++;
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues(new String[] {
                    "Load Stall", "L1 Miss", "Store Address", "DTLB Miss", "Pagewalk", "Other" });
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "PkgPower", "Instr/Watt",
                "Load Stall", "L1 Miss", "Store Address", "DTLB Miss", "Pagewalk", "Other" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float slots = counterData.activeCycles * coreType.AllocWidth;
                float all = counterData.pmc[0] + counterData.pmc[1] + counterData.pmc[2] + counterData.pmc[3];
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
                };
            }
        }
    }
}

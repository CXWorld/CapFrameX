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
            configs.Add(new PCoreGaming(this));  // Combined gaming metrics for all cores
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

        /// <summary>
        /// Gaming performance profile for Alder Lake / Raptor Lake.
        /// Monitors IPC, L3 hitrate, L3/Mem bound stalls, and memory bandwidth
        /// for both P-cores (Golden Cove) and E-cores (Gracemont).
        /// Shows separate overall metrics for P-cores and E-cores.
        /// </summary>
        public class PCoreGaming : MonitoringConfig
        {
            private AlderLake cpu;
            private CoreType pCoreType;
            private CoreType eCoreType;
            private bool hasPCores = false;
            private bool hasECores = false;

            public string GetConfigName() { return "All Cores: Gaming Performance"; }

            public PCoreGaming(AlderLake intelCpu)
            {
                cpu = intelCpu;
                PmcDiagnostics.Log("PCoreGaming: coreTypes.Length={0}", cpu.coreTypes.Length);
                foreach (CoreType type in cpu.coreTypes)
                {
                    PmcDiagnostics.Log("  CoreType: Type=0x{0:X2} CoreCount={1} CoreMask=0x{2:X16} PmcCounters={3}",
                        type.Type, type.CoreCount, type.CoreMask, type.PmcCounters);
                    if (type.Type == ADL_P_CORE_TYPE)
                    {
                        pCoreType = type;
                        hasPCores = true;
                    }
                    if (type.Type == ADL_E_CORE_TYPE)
                    {
                        eCoreType = type;
                        hasECores = true;
                    }
                }
                PmcDiagnostics.Log("PCoreGaming: hasPCores={0} hasECores={1}", hasPCores, hasECores);
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                PmcDiagnostics.Log("PCoreGaming.Initialize: hasPCores={0} hasECores={1}", hasPCores, hasECores);
                cpu.DisablePerformanceCounters();

                // P-core events (Golden Cove)
                if (hasPCores)
                {
                    PmcDiagnostics.Log("  Programming P-core events (PmcCounters={0})", pCoreType.PmcCounters);
                    ulong[] pCorePmc = new ulong[8];
                    pCorePmc[0] = GetPerfEvtSelRegisterValue(0xD1, 0x04); // MEM_LOAD_RETIRED.L3_HIT
                    pCorePmc[1] = GetPerfEvtSelRegisterValue(0xD1, 0x20); // MEM_LOAD_RETIRED.L3_MISS
                    pCorePmc[2] = GetPerfEvtSelRegisterValue(0x47, 0x05, true, true, false, false, false, false, true, false, 5); // MEMORY_ACTIVITY.STALLS_L2_MISS (CMask=5 mandatory)
                    pCorePmc[3] = GetPerfEvtSelRegisterValue(0x47, 0x09, true, true, false, false, false, false, true, false, 9); // MEMORY_ACTIVITY.STALLS_L3_MISS (CMask=9 mandatory)
                    // Note: all 4 events above only support counters 0-3. No room for OFFCORE_REQUESTS.
                    // DRAM BW is derived from MEM_LOAD_RETIRED.L3_MISS (pmc[1]) * 64B instead.
                    PmcDiagnostics.Log("  P-core events: [0]=0x{0:X16} [1]=0x{1:X16} [2]=0x{2:X16} [3]=0x{3:X16}",
                        pCorePmc[0], pCorePmc[1], pCorePmc[2], pCorePmc[3]);
                    cpu.ProgramPerfCounters(pCorePmc, ADL_P_CORE_TYPE);
                }

                // E-core events (Gracemont) - different event encodings than Crestmont/Skymont
                // Gracemont uses different UMask values for the same logical events
                if (hasECores)
                {
                    PmcDiagnostics.Log("  Programming E-core events (PmcCounters={0})", eCoreType.PmcCounters);
                    ulong[] eCorePmc = new ulong[6];
                    eCorePmc[0] = GetPerfEvtSelRegisterValue(0xD1, 0x04); // MEM_LOAD_UOPS_RETIRED.L3_HIT
                    eCorePmc[1] = GetPerfEvtSelRegisterValue(0xD1, 0x10); // MEM_LOAD_UOPS_RETIRED.L2_MISS (L3_HIT + L3_MISS)
                    eCorePmc[2] = GetPerfEvtSelRegisterValue(0x34, 0x02); // MEM_BOUND_STALLS.LOAD_LLC_HIT (L3 bound stalls)
                    eCorePmc[3] = GetPerfEvtSelRegisterValue(0x34, 0x04); // MEM_BOUND_STALLS.LOAD_DRAM_HIT (DRAM bound)
                    // DRAM BW derived from L2_MISS - L3_HIT (pmc[1] - pmc[0]) * 64B
                    cpu.ProgramPerfCounters(eCorePmc, ADL_E_CORE_TYPE);
                }
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                cpu.InitializeCoreTotals();

                // Count cores for each type
                int pCoreCount = hasPCores ? pCoreType.CoreCount : 0;
                int eCoreCount = hasECores ? eCoreType.CoreCount : 0;

                // Allocate: P-cores + P-overall + E-cores + E-overall
                int totalRows = pCoreCount + (pCoreCount > 0 ? 1 : 0) + eCoreCount + (eCoreCount > 0 ? 1 : 0);
                results.unitMetrics = new string[totalRows][];

                // Accumulators for P-core and E-core totals
                NormalizedCoreCounterData pCoreTotals = new NormalizedCoreCounterData();
                NormalizedCoreCounterData eCoreTotals = new NormalizedCoreCounterData();

                int rowIdx = 0;

                // Process P-cores first
                if (hasPCores && pCoreCount > 0)
                {
                    for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                    {
                        if (((pCoreType.CoreMask >> threadIdx) & 0x1) != 0x1)
                            continue;

                        cpu.UpdateThreadCoreCounterData(threadIdx);
                        var threadData = cpu.NormalizedThreadCounts[threadIdx];
                        results.unitMetrics[rowIdx] = computeMetrics("P-Core " + threadIdx, threadData, true);
                        AccumulateCounters(pCoreTotals, threadData);
                        rowIdx++;
                    }
                    // P-core overall
                    results.unitMetrics[rowIdx] = computeMetrics(">> P-Cores Overall", pCoreTotals, true);
                    rowIdx++;
                }

                // Process E-cores
                if (hasECores && eCoreCount > 0)
                {
                    for (int threadIdx = 0; threadIdx < cpu.GetThreadCount(); threadIdx++)
                    {
                        if (((eCoreType.CoreMask >> threadIdx) & 0x1) != 0x1)
                            continue;

                        cpu.UpdateThreadCoreCounterData(threadIdx);
                        var threadData = cpu.NormalizedThreadCounts[threadIdx];
                        results.unitMetrics[rowIdx] = computeMetrics("E-Core " + threadIdx, threadData, false);
                        AccumulateCounters(eCoreTotals, threadData);
                        rowIdx++;
                    }
                    // E-core overall
                    results.unitMetrics[rowIdx] = computeMetrics(">> E-Cores Overall", eCoreTotals, false);
                    rowIdx++;
                }

                // Combined overall
                results.overallMetrics = computeCombinedMetrics("Overall", pCoreTotals, eCoreTotals);
                results.overallCounterValues = cpu.GetOverallCounterValues(new string[] {
                    "L3 Hit", "L3 Miss", "L3 Stall Cycles", "Mem Stall Cycles" });
                return results;
            }

            private void AccumulateCounters(NormalizedCoreCounterData totals, NormalizedCoreCounterData source)
            {
                totals.pmc[0] += source.pmc[0];
                totals.pmc[1] += source.pmc[1];
                totals.pmc[2] += source.pmc[2];
                totals.pmc[3] += source.pmc[3];
                totals.activeCycles += source.activeCycles;
                totals.instr += source.instr;
            }

            public string[] columns = new string[] {
                "Item", "IPC", "L3 Hitrate", "L3 Bound %", "Mem Bound %",
                "L3 Miss BW"
            };

            public string GetHelpText()
            {
                return "Gaming metrics for P and E cores.\n" +
                       "L3 Bound = (STALLS_L2_MISS - STALLS_L3_MISS) / active cycles.\n" +
                       "Mem Bound = STALLS_L3_MISS / active cycles.\n" +
                       "L3 Miss BW = L3_MISS * 64B (demand load L3 miss traffic)";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData, bool isPCore)
            {
                float l3Hit = counterData.pmc[0];

                float l3Miss, l3Stalls, memStalls;
                if (isPCore)
                {
                    // P-cores (Golden Cove):
                    // pmc[1] = MEM_LOAD_RETIRED.L3_MISS
                    // pmc[2] = MEMORY_ACTIVITY.STALLS_L2_MISS (includes L3 and DRAM stalls)
                    // pmc[3] = MEMORY_ACTIVITY.STALLS_L3_MISS (DRAM stalls only)
                    l3Miss = counterData.pmc[1];
                    l3Stalls = counterData.pmc[2] - counterData.pmc[3]; // L3 bound = L2_MISS stalls - L3_MISS stalls
                    if (l3Stalls < 0) l3Stalls = 0;
                    memStalls = counterData.pmc[3];
                }
                else
                {
                    // E-cores (Gracemont):
                    // pmc[1] = MEM_LOAD_UOPS_RETIRED.L2_MISS (L3_HIT + L3_MISS)
                    // pmc[2] = MEM_BOUND_STALLS.LOAD_LLC_HIT (L3 bound stalls)
                    // pmc[3] = MEM_BOUND_STALLS.LOAD_DRAM_HIT (DRAM bound stalls)
                    float l2Miss = counterData.pmc[1];
                    l3Miss = l2Miss - l3Hit;
                    if (l3Miss < 0) l3Miss = 0;
                    l3Stalls = counterData.pmc[2];
                    memStalls = counterData.pmc[3];
                }

                // IPC
                float ipc = counterData.activeCycles > 0 ? counterData.instr / counterData.activeCycles : 0;
                // L3 hit rate
                float l3HitRate = (l3Hit + l3Miss) > 0 ? 100 * l3Hit / (l3Hit + l3Miss) : 0;
                // L3 bound % (cycles stalled waiting for L3)
                float l3BoundPct = counterData.activeCycles > 0 ? 100 * l3Stalls / counterData.activeCycles : 0;
                // Memory bound % (cycles waiting for DRAM)
                float memBoundPct = counterData.activeCycles > 0 ? 100 * memStalls / counterData.activeCycles : 0;
                // DRAM BW = L3_MISS * 64 bytes per cacheline
                float dramBw = l3Miss * 64;

                return new string[] {
                    label,
                    string.Format("{0:F2}", ipc),
                    string.Format("{0:F2}%", l3HitRate),
                    string.Format("{0:F2}%", l3BoundPct),
                    string.Format("{0:F2}%", memBoundPct),
                    FormatLargeNumber(dramBw) + "B/s"
                };
            }

            private string[] computeCombinedMetrics(string label, NormalizedCoreCounterData pCoreTotals, NormalizedCoreCounterData eCoreTotals)
            {
                // P-cores: pmc[1] = L3_MISS directly
                // E-cores: pmc[1] = L2_MISS, so L3_MISS = L2_MISS - L3_HIT
                float pCoreL3Hit = pCoreTotals.pmc[0];
                float pCoreL3Miss = pCoreTotals.pmc[1];
                float eCoreL3Hit = eCoreTotals.pmc[0];
                float eCoreL2Miss = eCoreTotals.pmc[1];
                float eCoreL3Miss = eCoreL2Miss - eCoreL3Hit;
                if (eCoreL3Miss < 0) eCoreL3Miss = 0;

                float l3Hit = pCoreL3Hit + eCoreL3Hit;
                float l3Miss = pCoreL3Miss + eCoreL3Miss;

                // P-cores: L3 stalls = STALLS_L2_MISS - STALLS_L3_MISS
                float pCoreL3Stalls = pCoreTotals.pmc[2] - pCoreTotals.pmc[3];
                if (pCoreL3Stalls < 0) pCoreL3Stalls = 0;
                float l3Stalls = pCoreL3Stalls + eCoreTotals.pmc[2];

                // P-cores: Mem stalls = STALLS_L3_MISS, E-cores: LOAD_DRAM_HIT
                float memStalls = pCoreTotals.pmc[3] + eCoreTotals.pmc[3];

                float activeCycles = pCoreTotals.activeCycles + eCoreTotals.activeCycles;
                float instr = pCoreTotals.instr + eCoreTotals.instr;

                // Combined DRAM BW = total L3_MISS * 64B
                float dramBw = (pCoreL3Miss + eCoreL3Miss) * 64;

                // IPC
                float ipc = activeCycles > 0 ? instr / activeCycles : 0;
                // L3 hit rate
                float l3HitRate = (l3Hit + l3Miss) > 0 ? 100 * l3Hit / (l3Hit + l3Miss) : 0;
                // L3 bound %
                float l3BoundPct = activeCycles > 0 ? 100 * l3Stalls / activeCycles : 0;
                // Memory bound %
                float memBoundPct = activeCycles > 0 ? 100 * memStalls / activeCycles : 0;

                return new string[] {
                    label,
                    string.Format("{0:F2}", ipc),
                    string.Format("{0:F2}%", l3HitRate),
                    string.Format("{0:F2}%", l3BoundPct),
                    string.Format("{0:F2}%", memBoundPct),
                    FormatLargeNumber(dramBw) + "B/s"
                };
            }
        }
    }
}

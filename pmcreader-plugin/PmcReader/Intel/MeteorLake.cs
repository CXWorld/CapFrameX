using System;
using System.Collections.Generic;
using PmcReader.Interop;

namespace PmcReader.Intel
{
    public class MeteorLake : ModernIntelCpu
    {
        public const uint IA32_PERF_METRICS = 0x329;
        public static byte P_CORE_TYPE = 0x40;
        public static byte E_CORE_TYPE = 0x20;
        private static int PCoreTypeIndex, ECoreTypeIndex;

        public MeteorLake()
        {
            List<MonitoringConfig> configs = new List<MonitoringConfig>();
            architectureName = "Meteor Lake";
            if (coreTypes.Length > 1)
            {
                architectureName += " (Hybrid)";
            }

            // Use a list of cores of each type
            // MTL will always be hybrid so non-hybrid cases won't be handled
            for (byte coreIdx = 0; coreIdx < coreTypes.Length; coreIdx++)
            {
                if (coreTypes[coreIdx].Type == P_CORE_TYPE) PCoreTypeIndex = coreIdx;
                else if (coreTypes[coreIdx].Type == E_CORE_TYPE) ECoreTypeIndex = coreIdx;
            }

            // Create supported configs
            configs.Add(new ArchitecturalCounters(this));
            configs.Add(new PCoreFE(this));
            configs.Add(new L2(this));
            configs.Add(new Topdown(this));
            configs.Add(new ECoreBackendStall(this));
            configs.Add(new ECoreMemBound(this));
            monitoringConfigs = configs.ToArray();
        }

        public class PCoreFE : MonitoringConfig
        {
            private MeteorLake cpu;
            private List<int> targetCores;

            public string GetConfigName() { return "P: Frontend"; }

            public PCoreFE(MeteorLake intelCpu)
            {
                cpu = intelCpu;
                targetCores = intelCpu.coreTypeNumbers[PCoreTypeIndex];
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.DisablePerformanceCounters();

                ulong[] pmc = new ulong[8];
                pmc[0] = GetPerfEvtSelRegisterValue(0x79, 0x8); // DSB ops
                pmc[1] = GetPerfEvtSelRegisterValue(0x79, 0x4); // MITE ops
                pmc[2] = GetPerfEvtSelRegisterValue(0x79, 0x20); // MS ops
                pmc[3] = GetPerfEvtSelRegisterValue(0x75, 0x1); // Instrs decoded
                cpu.ProgramPerfCounters(pmc, targetCores, P_CORE_TYPE);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[targetCores.Count][];
                cpu.InitializeCoreTotals();
                for (int i = 0; i < targetCores.Count; i++)
                {
                    int threadIdx = targetCores[i];
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[i] = computeMetrics("P: Thread " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues(new string[] {
                    "DSB Ops", "MITE Ops", "MS Ops", "Instrs Decoded" });
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "PkgPower", "Instr/Watt",
                "OC %", "MITE %", "MS %", "DSB Ops", "MITE Ops", "MS Ops", "Decoded Instrs" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float totalFeOps = counterData.pmc[0] + counterData.pmc[1] + counterData.pmc[2];
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatLargeNumber(counterData.instr / counterData.packagePower),
                        FormatPercentage(counterData.pmc[0], totalFeOps),
                        FormatPercentage(counterData.pmc[1], totalFeOps),
                        FormatPercentage(counterData.pmc[2], totalFeOps),
                        FormatLargeNumber(counterData.pmc[0]),
                        FormatLargeNumber(counterData.pmc[1]),
                        FormatLargeNumber(counterData.pmc[2]),
                        FormatLargeNumber(counterData.pmc[3])
                };
            }
        }

        public class Topdown : MonitoringConfig
        {
            private MeteorLake cpu;
            private List<int> PCores;
            private List<int> ECores;

            public string GetConfigName() { return "Topdown"; }

            public Topdown(MeteorLake intelCpu)
            {
                cpu = intelCpu;
                PCores = intelCpu.coreTypeNumbers[PCoreTypeIndex];
                ECores = intelCpu.coreTypeNumbers[ECoreTypeIndex];
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.DisablePerformanceCounters();

                // clear MSR_PERF_METRICS and fixed counter 3 for P cores
                for (int i = 0; i < PCores.Count; i++)
                {
                    ThreadAffinity.Set(1UL << PCores[i]);
                    Ring0.WriteMsr(IA32_FIXED_CTR3, 0);
                    Ring0.WriteMsr(IA32_PERF_METRICS, 0);
                }

                    ulong[] pCorePmc = new ulong[8];
                pCorePmc[0] = GetPerfEvtSelRegisterValue(0xA4, 1); // slots
                pCorePmc[1] = GetPerfEvtSelRegisterValue(0xA4, 4); // bad speculation
                cpu.ProgramPerfCounters(pCorePmc, PCores, P_CORE_TYPE);

                ulong[] eCorePmc = new ulong[8];
                eCorePmc[0] = GetPerfEvtSelRegisterValue(0x73, 0); // bad speculation
                eCorePmc[1] = GetPerfEvtSelRegisterValue(0x71, 0); // frontend bound
                eCorePmc[2] = GetPerfEvtSelRegisterValue(0x74, 0); // backend bound
                eCorePmc[3] = GetPerfEvtSelRegisterValue(0x72, 0); // retiring
                eCorePmc[4] = GetPerfEvtSelRegisterValue(0xA4, 1); // slots
                eCorePmc[5] = GetPerfEvtSelRegisterValue(0x71, 0x72); // frontend latency bound
                eCorePmc[6] = GetPerfEvtSelRegisterValue(0x71, 0x8D); // frontend bw bound

                cpu.ProgramPerfCounters(eCorePmc, ECores, E_CORE_TYPE);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[PCores.Count + ECores.Count][];
                cpu.InitializeCoreTotals();
                cpu.ReadPackagePowerCounter();
                float totalSlots = 0, totalBadSpecSlots = 0, totalRetiringSlots = 0, totalFeBoundSlots = 0, totalFeLatencyBoundSlots = 0, totalBeBoundSlots = 0;
                for (int i = 0; i < PCores.Count; i++)
                {
                    int threadIdx = PCores[i];

                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    Ring0.ReadMsr(IA32_PERF_METRICS, out ulong topdownMetrics);
                    Ring0.ReadMsr(IA32_FIXED_CTR3, out ulong fixed_topdown_slots);
                    Ring0.ReadMsr(IA32_PERF_GLOBAL_CTRL, out ulong global_ctrl);
                    Ring0.ReadMsr(IA32_PERF_GLOBAL_STATUS, out ulong global_status);
                    // ulong topdownMetrics = ReadAndClearMsr(IA32_PERF_METRICS);
                    // ulong topdown_slots = ReadAndClearMsr(IA32_FIXED_CTR3);

                    float retiringPercent = (topdownMetrics & 0xFF) / 255f;
                    float badSpecPercent = ((topdownMetrics >> 8) & 0xFF) / 255f;
                    float feBoundPercent = ((topdownMetrics >> 16) & 0xFF) / 255f;
                    float beBoundPercent = ((topdownMetrics >> 24) & 0xFF) / 255f;
                    float feLatencyBoundPercent = ((topdownMetrics >> 48) & 0xFF) / 255f;
                    results.unitMetrics[i] = new string[] { "P: Thread " + threadIdx,
                        FormatLargeNumber(cpu.NormalizedThreadCounts[threadIdx].activeCycles),
                        FormatLargeNumber(cpu.NormalizedThreadCounts[threadIdx].instr),
                        string.Format("{0:F2}", cpu.NormalizedThreadCounts[threadIdx].instr / cpu.NormalizedThreadCounts[threadIdx].activeCycles),
                        "--",
                        "--",
                        string.Format("{0:F2}%", badSpecPercent * 100),
                        string.Format("{0:F2}%", feBoundPercent * 100),
                        string.Format("{0:F2}%", feLatencyBoundPercent * 100),
                        string.Format("{0:F2}%", beBoundPercent * 100),
                        string.Format("{0:F2}%", retiringPercent * 100) };

                    float slots = cpu.NormalizedThreadCounts[threadIdx].pmc[0];
                    totalSlots += slots;
                    totalBadSpecSlots += slots * badSpecPercent;
                    totalRetiringSlots += slots * retiringPercent;
                    totalFeBoundSlots += slots * feBoundPercent;
                    totalFeLatencyBoundSlots += slots * feLatencyBoundPercent;
                    totalBeBoundSlots += slots * beBoundPercent;
                }

                for (int i = 0; i < ECores.Count; i++)
                {
                    int threadIdx = ECores[i];
                    cpu.UpdateThreadCoreCounterData(threadIdx);

                    float slots = cpu.NormalizedThreadCounts[threadIdx].activeCycles * 6;
                    float retiringSlots = cpu.NormalizedThreadCounts[threadIdx].pmc[3];
                    float badSpecSlots = cpu.NormalizedThreadCounts[threadIdx].pmc[0];
                    float feBoundSlots = cpu.NormalizedThreadCounts[threadIdx].pmc[1];
                    float beBoundSlots = cpu.NormalizedThreadCounts[threadIdx].pmc[2];
                    float feLatencyBoundSlots = cpu.NormalizedThreadCounts[threadIdx].pmc[5];

                    results.unitMetrics[PCores.Count + i] = new string[] { "E: Thread " + threadIdx,
                        FormatLargeNumber(cpu.NormalizedThreadCounts[threadIdx].activeCycles),
                        FormatLargeNumber(cpu.NormalizedThreadCounts[threadIdx].instr),
                        string.Format("{0:F2}", cpu.NormalizedThreadCounts[threadIdx].instr / cpu.NormalizedThreadCounts[threadIdx].activeCycles),
                        string.Format("{0:F2} W", cpu.NormalizedThreadCounts[threadIdx].packagePower),
                        FormatLargeNumber(cpu.NormalizedThreadCounts[threadIdx].instr / cpu.NormalizedThreadCounts[threadIdx].packagePower),
                        FormatPercentage(badSpecSlots, slots),
                        FormatPercentage(feBoundSlots, slots),
                        FormatPercentage(feLatencyBoundSlots, slots),
                        FormatPercentage(beBoundSlots, slots),
                        FormatPercentage(retiringSlots, slots)
                    };

                    totalSlots += slots;
                    totalBadSpecSlots += badSpecSlots;
                    totalFeBoundSlots += feBoundSlots;
                    totalFeLatencyBoundSlots += feLatencyBoundSlots;
                    totalBeBoundSlots += beBoundSlots;
                    totalRetiringSlots += retiringSlots;
                }

                results.overallMetrics = new string[] {"Overall",
                    FormatLargeNumber(cpu.NormalizedTotalCounts.activeCycles),
                        FormatLargeNumber(cpu.NormalizedTotalCounts.instr),
                        string.Format("{0:F2}", cpu.NormalizedTotalCounts.instr / cpu.NormalizedTotalCounts.activeCycles),
                        string.Format("{0:F2} W", cpu.NormalizedTotalCounts.packagePower),
                        FormatLargeNumber(cpu.NormalizedTotalCounts.instr / cpu.NormalizedTotalCounts.packagePower),
                        FormatPercentage(totalBadSpecSlots, totalSlots),
                        FormatPercentage(totalFeBoundSlots, totalSlots),
                        FormatPercentage(totalFeLatencyBoundSlots, totalSlots),
                        FormatPercentage(totalBeBoundSlots, totalSlots),
                        FormatPercentage(totalRetiringSlots, totalSlots)
                };

                List<Tuple<string, float>> overallCounterValues = new List<Tuple<string, float>>();
                overallCounterValues.Add(new Tuple<string, float>("Active Cycles", cpu.NormalizedTotalCounts.activeCycles));
                overallCounterValues.Add(new Tuple<string, float>("REF_TSC", cpu.NormalizedTotalCounts.refTsc));
                overallCounterValues.Add(new Tuple<string, float>("Instructions", cpu.NormalizedTotalCounts.instr));
                overallCounterValues.Add(new Tuple<string, float>("Package Power", cpu.NormalizedTotalCounts.packagePower));
                overallCounterValues.Add(new Tuple<string, float>("PP0 Power", cpu.NormalizedTotalCounts.pp0Power));
                overallCounterValues.Add(new Tuple<string, float>("Slots", totalSlots));
                overallCounterValues.Add(new Tuple<string, float>("Bad Speculation", totalBadSpecSlots));
                overallCounterValues.Add(new Tuple<string, float>("Frontend Bound", totalFeBoundSlots));
                overallCounterValues.Add(new Tuple<string, float>("Frontend Latency Bound", totalFeLatencyBoundSlots));
                overallCounterValues.Add(new Tuple<string, float>("Backend Bound", totalBeBoundSlots));
                overallCounterValues.Add(new Tuple<string, float>("Retiring", totalRetiringSlots));

                results.overallCounterValues = overallCounterValues.ToArray();
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "PkgPower", "Instr/Watt",
                "Bad Speculation", "FE Bound", "FE Latency", "BE Bound", "Retiring" };

            public string GetHelpText()
            {
                return "";
            }
        }

        public class L2 : MonitoringConfig
        {
            private MeteorLake cpu;
            private List<int> PCores;
            private List<int> ECores;

            public string GetConfigName() { return "L2 Cache"; }

            public L2(MeteorLake intelCpu)
            {
                cpu = intelCpu;
                PCores = intelCpu.coreTypeNumbers[PCoreTypeIndex];
                ECores = intelCpu.coreTypeNumbers[ECoreTypeIndex];
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.DisablePerformanceCounters();

                for (int i = 0; i < PCores.Count; i++)
                {
                    ThreadAffinity.Set(1UL << PCores[i]);
                    Ring0.WriteMsr(IA32_PERFEVTSEL0, GetPerfEvtSelRegisterValue(0x24, 0x3F)); // L2 miss
                    Ring0.WriteMsr(IA32_PERFEVTSEL1, GetPerfEvtSelRegisterValue(0x24, 0xFF)); // L2 references
                }

                for (int i = 0; i < ECores.Count; i++)
                {
                    ThreadAffinity.Set(1UL << ECores[i]);
                    Ring0.WriteMsr(IA32_PERFEVTSEL0, GetPerfEvtSelRegisterValue(0x24, 1));
                    Ring0.WriteMsr(IA32_PERFEVTSEL1, GetPerfEvtSelRegisterValue(0x24, 0));
                    Ring0.WriteMsr(IA32_PERFEVTSEL2, GetPerfEvtSelRegisterValue(OFFCORE_RESPONSE_EVENT, 1)); // L2 miss
                    Ring0.WriteMsr(IA32_PERFEVTSEL3, GetPerfEvtSelRegisterValue(OFFCORE_RESPONSE_EVENT, 2)); // L2 references

                    ulong offcoreResponseSelection = 0x1FFF; // demand/code + L1D prefetcher
                    ulong l2MissSelection = offcoreResponseSelection | (1UL << 26); // miss
                    ulong l2ReqSelection = offcoreResponseSelection | (1UL << 16); // any response

                    Ring0.WriteMsr(MSR_OFFCORE_RSP0, l2MissSelection);
                    Ring0.WriteMsr(MSR_OFFCORE_RSP1, l2ReqSelection);
                }

                ulong[] eCorePmc = new ulong[8];
                eCorePmc[0] = GetPerfEvtSelRegisterValue(OFFCORE_RESPONSE_EVENT, 1); // offcore response 0
                eCorePmc[1] = GetPerfEvtSelRegisterValue(OFFCORE_RESPONSE_EVENT, 2); // offcore response 1

                cpu.EnablePerformanceCounters();
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[PCores.Count + ECores.Count][];
                cpu.InitializeCoreTotals();
                cpu.ReadPackagePowerCounter();

                int metricsIdx = 0;
                for (int i =  0; i < PCores.Count;i++)
                {
                    int threadIdx = PCores[i];
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[metricsIdx] = computeMetrics("P: Thread " + threadIdx, cpu.NormalizedThreadCounts[threadIdx], null);
                    metricsIdx++;
                }

                for (int i = 0;i < ECores.Count; i++)
                {
                    int threadIdx = ECores[i];
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[metricsIdx] = computeMetrics("E: Thread " + threadIdx, cpu.NormalizedThreadCounts[threadIdx], null);
                    metricsIdx++;
                }

                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts, cpu.RawTotalCounts);

                results.overallCounterValues = cpu.GetOverallCounterValues("L2 Hits", "L2 References", "unused", "unused");
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "PkgPower", "Instr/Watt",
                "L2 Hitrate", "L2 Hit BW", "L2 MPKI", "L2 Req/Ki", "L2 Hit/Ki", "Total Instructions"};

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData, RawTotalCoreCounterData rawTotals)
            {
                float l2Hits = counterData.pmc[1] - counterData.pmc[0];
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatLargeNumber(counterData.instr / counterData.packagePower),
                        FormatPercentage(l2Hits, counterData.pmc[1]),
                        FormatLargeNumber(64 * l2Hits) + "B/s",
                        string.Format("{0:F2}", 1000 * counterData.pmc[0] / counterData.instr),
                        string.Format("{0:F2}", 1000 * counterData.pmc[1] / counterData.instr),
                        string.Format("{0:F2}", 1000 * l2Hits / counterData.instr),
                        rawTotals == null ? "-" : FormatLargeNumber(rawTotals.instr)
                };
            }

            public string GetHelpText()
            {
                return "";
            }
        }

        public class ECoreBackendStall : MonitoringConfig
        {
            private MeteorLake cpu;
            private List<int> targetCores;

            public string GetConfigName() { return "E: Backend Stall"; }

            public ECoreBackendStall(MeteorLake intelCpu)
            {
                cpu = intelCpu;
                targetCores = intelCpu.coreTypeNumbers[ECoreTypeIndex];
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.DisablePerformanceCounters();

                ulong[] pmc = new ulong[8];
                pmc[0] = GetPerfEvtSelRegisterValue(0x74, 0x1); // Allocation restriction (slots)
                pmc[1] = GetPerfEvtSelRegisterValue(0x04, 0x4); // Memory scheduler (cycles)
                pmc[2] = GetPerfEvtSelRegisterValue(0x74, 0x8); // Non-memory scheduler (slots)
                pmc[3] = GetPerfEvtSelRegisterValue(0x74, 0x20); // Registers (slots)
                pmc[4] = GetPerfEvtSelRegisterValue(0x74, 0x40); // Reorder buffer (slots)
                pmc[5] = GetPerfEvtSelRegisterValue(0x74, 0x10); // Serialization (slots)
                pmc[6] = GetPerfEvtSelRegisterValue(0x04, 0x2); // Load buffer (cycles)
                pmc[7] = GetPerfEvtSelRegisterValue(0x04, 0x1); // Store buffer (cycles)
                cpu.ProgramPerfCounters(pmc, targetCores, E_CORE_TYPE);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[targetCores.Count][];
                cpu.InitializeCoreTotals();
                for (int i = 0; i < targetCores.Count; i++)
                {
                    int threadIdx = targetCores[i];
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[i] = computeMetrics("E: Thread " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues(new string[] {
                    "Alloc Restriction (Slots)", "Memory RSV (Cycles)", "Non-Memory Scheduler (Slots)", "Registers (Slots)", "ROB (Slots)", "Serialization (Slots)", 
                    "Load Buffer (Cycles)", "Store Buffer (Cycles)" });
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "PkgPower", "Instr/Watt",
                "ROB", "Registers", "Non-Mem RSV", "Mem RSV", "Load Buffer", "Store Buffer", "Alloc Restriction", "Serialization" };

            public string GetHelpText()
            {
                return "";
            }

            private string[] computeMetrics(string label, NormalizedCoreCounterData counterData)
            {
                float slots = counterData.activeCycles * 6;
                float allocRestriction = counterData.pmc[0];
                float memSched = counterData.pmc[1];
                float nonMemSched = counterData.pmc[2];
                float registers = counterData.pmc[3];
                float rob = counterData.pmc[4];
                float serialization = counterData.pmc[5];
                float ldb = counterData.pmc[6];
                float stb = counterData.pmc[7];
                return new string[] { label,
                        FormatLargeNumber(counterData.activeCycles),
                        FormatLargeNumber(counterData.instr),
                        string.Format("{0:F2}", counterData.instr / counterData.activeCycles),
                        string.Format("{0:F2} W", counterData.packagePower),
                        FormatLargeNumber(counterData.instr / counterData.packagePower),
                        FormatPercentage(rob, slots),
                        FormatPercentage(registers, slots),
                        FormatPercentage(nonMemSched, slots),
                        FormatPercentage(memSched, counterData.activeCycles),
                        FormatPercentage(ldb, counterData.activeCycles),
                        FormatPercentage(stb, counterData.activeCycles),
                        FormatPercentage(allocRestriction, slots),
                        FormatPercentage(serialization, slots)
                };
            }
        }

        public class ECoreMemBound : MonitoringConfig
        {
            private MeteorLake cpu;
            private List<int> targetCores;

            public string GetConfigName() { return "E: Memory Bound"; }

            public ECoreMemBound(MeteorLake intelCpu)
            {
                cpu = intelCpu;
                targetCores = intelCpu.coreTypeNumbers[ECoreTypeIndex];
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.DisablePerformanceCounters();

                ulong[] pmc = new ulong[8];
                pmc[0] = GetPerfEvtSelRegisterValue(0x34, 0x1); // Stall with demand load that hits in L2
                pmc[1] = GetPerfEvtSelRegisterValue(0x34, 0x6); // Stall with demand load L3 hit
                pmc[2] = GetPerfEvtSelRegisterValue(0x34, 0x78); // Stall with demand load LLC miss
                pmc[3] = GetPerfEvtSelRegisterValue(0x34, 0x7F); // Stall with demand load L1D miss
                pmc[4] = GetPerfEvtSelRegisterValue(0x35, 0x7F); // Instr fetch stall, L1i/TLB miss
                pmc[5] = GetPerfEvtSelRegisterValue(0x35, 0x1); // Instr fetch stall, L2 hit
                pmc[6] = GetPerfEvtSelRegisterValue(0x35, 0x10); // Instr fetch stall, LLC hit
                pmc[7] = GetPerfEvtSelRegisterValue(0x78, 0x2); // Instr fetch stall, LLC miss
                cpu.ProgramPerfCounters(pmc, targetCores, E_CORE_TYPE);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[targetCores.Count][];
                cpu.InitializeCoreTotals();
                for (int i = 0; i < targetCores.Count; i++)
                {
                    int threadIdx = targetCores[i];
                    cpu.UpdateThreadCoreCounterData(threadIdx);
                    results.unitMetrics[i] = computeMetrics("E: Thread " + threadIdx, cpu.NormalizedThreadCounts[threadIdx]);
                }

                cpu.ReadPackagePowerCounter();
                results.overallMetrics = computeMetrics("Overall", cpu.NormalizedTotalCounts);
                results.overallCounterValues = cpu.GetOverallCounterValues(new string[] {
                    "Stall L2 Hit", "Stall L3 Hit", "Stall LLC Miss", "Stall L1D Miss", "IF Stall IC/iTLB Miss", "IF Stall L2 Hit", "IF Stall L3 Hit", "IF Stall LLC Miss" });
                return results;
            }

            public string[] columns = new string[] { "Item", "Active Cycles", "Instructions", "IPC", "PkgPower", "Instr/Watt",
                "L1D Miss", "L2 Data Hit", "L3 Data Hit", "LLC Miss", "IFetch IC/iTLB Miss", "IFetch L2 Hit", "IFetch L3 Hit", "IFetch LLC Miss" };

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
                        FormatPercentage(counterData.pmc[3], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[0], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[1], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[2], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[4], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[5], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[6], counterData.activeCycles),
                        FormatPercentage(counterData.pmc[7], counterData.activeCycles)
                };
            }
        }

    }
}

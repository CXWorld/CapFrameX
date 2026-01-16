using System;
using System.Collections.Generic;
using PmcReader.Interop;

namespace PmcReader.AMD
{
    public class PiledriverNorthbridge : Amd15hCpu
    {
        private const int monitoringThread = 1;

        public PiledriverNorthbridge()
        {
            architectureName = "Piledriver Northbridge";
            List<MonitoringConfig> cfgs = new List<MonitoringConfig>();
            cfgs.Add(new MemBwConfig(this));
            cfgs.Add(new L3Config(this));
            cfgs.Add(new MemSubtimings(this));
            cfgs.Add(new CrossbarRequests(this));
            cfgs.Add(new SriCommands(this));
            cfgs.Add(new L3Commands(this));
            cfgs.Add(new Probe(this));
            cfgs.Add(new UpstreamRequests(this));
            cfgs.Add(new MemSource(this));
            //cfgs.Add(new GartConfig(this));
            monitoringConfigs = cfgs.ToArray();
        }

        private void ProgramPerfCounters(ulong ctr0, ulong ctr1, ulong ctr2, ulong ctr3)
        {
            ThreadAffinity.Set(1UL << monitoringThread);
            Ring0.WriteMsr(MSR_NB_PERF_CTL_0, ctr0);
            Ring0.WriteMsr(MSR_NB_PERF_CTL_1, ctr1);
            Ring0.WriteMsr(MSR_NB_PERF_CTL_2, ctr2);
            Ring0.WriteMsr(MSR_NB_PERF_CTL_3, ctr3);
            Ring0.WriteMsr(MSR_NB_PERF_CTR_0, 0);
            Ring0.WriteMsr(MSR_NB_PERF_CTR_1, 0);
            Ring0.WriteMsr(MSR_NB_PERF_CTR_2, 0);
            Ring0.WriteMsr(MSR_NB_PERF_CTR_3, 0);
            this.nbCounterData = new NormalizedNbCounterData();
        }

        private NormalizedNbCounterData nbCounterData = null;

        private NormalizedNbCounterData UpdateNbPerfCounterData()
        {
            float normalizationFactor = GetNormalizationFactor(monitoringThread);
            ulong ctr0 = ReadAndClearMsr(MSR_NB_PERF_CTR_0);
            ulong ctr1 = ReadAndClearMsr(MSR_NB_PERF_CTR_1);
            ulong ctr2 = ReadAndClearMsr(MSR_NB_PERF_CTR_2);
            ulong ctr3 = ReadAndClearMsr(MSR_NB_PERF_CTR_3);

            if (nbCounterData == null) nbCounterData = new NormalizedNbCounterData();
            NormalizedNbCounterData counterData = nbCounterData;
            counterData.ctr0 = ctr0 * normalizationFactor;
            counterData.ctr1 = ctr1 * normalizationFactor;
            counterData.ctr2 = ctr2 * normalizationFactor;
            counterData.ctr3 = ctr3 * normalizationFactor;
            counterData.ctr0total += ctr0;
            counterData.ctr1total += ctr1;
            counterData.ctr2total += ctr2;
            counterData.ctr3total += ctr3;
            return counterData;
        }

        private Tuple<string, float>[] GetOverallCounterValues(NormalizedNbCounterData counterData, string ctr0, string ctr1, string ctr2, string ctr3)
        {
            Tuple<string, float>[] retval = new Tuple<string, float>[4];
            retval[0] = new Tuple<string, float>(ctr0, counterData.ctr0);
            retval[1] = new Tuple<string, float>(ctr1, counterData.ctr1);
            retval[2] = new Tuple<string, float>(ctr2, counterData.ctr2);
            retval[3] = new Tuple<string, float>(ctr3, counterData.ctr3);
            return retval;
        }

        public class NormalizedNbCounterData
        {
            /// <summary>
            /// Programmable performance counter values
            /// </summary>
            public float ctr0;
            public float ctr1;
            public float ctr2;
            public float ctr3;

            public ulong ctr0total;
            public ulong ctr1total;
            public ulong ctr2total;
            public ulong ctr3total;

            public float NormalizationFactor;
        }

        public class MemBwConfig : MonitoringConfig
        {
            private PiledriverNorthbridge dataFabric;

            public string[] columns = new string[] { "Item", "Count * 64B", "Count", "Total Data" };
            public string GetHelpText() { return ""; }
            public MemBwConfig(PiledriverNorthbridge dataFabric)
            {
                this.dataFabric = dataFabric;
            }

            public string GetConfigName() { return "Memory Bandwidth"; }
            public string[] GetColumns() { return columns; }
            public void Initialize()
            {
                dataFabric.ProgramPerfCounters(
                    GetNBPerfCtlValue(0xE0, 1, true, 0), // DCT0 page hit
                    GetNBPerfCtlValue(0xE0, 0b110, true, 0), // DCT0 page miss
                    GetNBPerfCtlValue(0xE0, 0b1000, true, 0), // DCT1 page hit
                    GetNBPerfCtlValue(0xE0, 0b110000, true, 0)); // DCT1 page miss
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                NormalizedNbCounterData counterData = dataFabric.UpdateNbPerfCounterData();

                results.unitMetrics = new string[4][];
                results.unitMetrics[0] = new string[] { "DCT0 Page Hit", 
                    FormatLargeNumber(counterData.ctr0 * 64) + "B/s",
                    FormatLargeNumber(counterData.ctr0),
                    FormatLargeNumber(counterData.ctr0total * 64) + "B",
                };
                results.unitMetrics[1] = new string[] { "DCT0 Page Miss", 
                    FormatLargeNumber(counterData.ctr1 * 64) + "B/s", 
                    FormatLargeNumber(counterData.ctr1),
                    FormatLargeNumber(counterData.ctr1total * 64) + "B",
                };
                results.unitMetrics[2] = new string[] { "DCT1 Page Hit", 
                    FormatLargeNumber(counterData.ctr2 * 64) + "B/s", 
                    FormatLargeNumber(counterData.ctr2),
                    FormatLargeNumber(counterData.ctr2total * 64) + "B",
                };
                results.unitMetrics[3] = new string[] { "DCT1 Page Miss", 
                    FormatLargeNumber(counterData.ctr3 * 64) + "B/s", 
                    FormatLargeNumber(counterData.ctr3),
                    FormatLargeNumber(counterData.ctr3total * 64) + "B",
                };

                float totalReqs = counterData.ctr0 + counterData.ctr1 + counterData.ctr2 + counterData.ctr3;
                float totalCumulativeReqs = counterData.ctr0total + counterData.ctr1total + counterData.ctr2total + counterData.ctr3total;
                results.overallMetrics = new string[] { "Overall", FormatLargeNumber(totalReqs * 64) + "B/s", FormatLargeNumber(totalReqs), FormatLargeNumber(64 * totalCumulativeReqs) + "B" };

                results.overallCounterValues = dataFabric.GetOverallCounterValues(counterData, "DCT0 Page Hit", "DCT0 Page Miss", "DCT1 Page Hit", "DCT1 Page Miss");
                return results;
            }
        }
        public class MemSubtimings : MonitoringConfig
        {
            private PiledriverNorthbridge dataFabric;

            public string[] columns = new string[] { "Item", "Count * 64B", "Count" };
            public string GetHelpText() { return ""; }
            public MemSubtimings(PiledriverNorthbridge dataFabric)
            {
                this.dataFabric = dataFabric;
            }

            public string GetConfigName() { return "Memory Subtimings"; }
            public string[] GetColumns() { return columns; }
            public void Initialize()
            {
                dataFabric.ProgramPerfCounters(
                    GetNBPerfCtlValue(0xE0, 0b1001, true, 0), // page hit
                    GetNBPerfCtlValue(0xE0, 0b10010, true, 0), // page miss
                    GetNBPerfCtlValue(0xE0, 0b100100, true, 0), // page conflict
                    GetNBPerfCtlValue(0xE1, 0b11, true, 0)); // page table overflow
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                NormalizedNbCounterData counterData = dataFabric.UpdateNbPerfCounterData();

                results.unitMetrics = new string[4][];
                results.unitMetrics[0] = new string[] { "Page Hit", FormatLargeNumber(counterData.ctr0 * 64) + "B/s", FormatLargeNumber(counterData.ctr0) };
                results.unitMetrics[1] = new string[] { "Page Miss", FormatLargeNumber(counterData.ctr1 * 64) + "B/s", FormatLargeNumber(counterData.ctr1) };
                results.unitMetrics[2] = new string[] { "Page Conflict", FormatLargeNumber(counterData.ctr2 * 64) + "B/s", FormatLargeNumber(counterData.ctr2) };
                results.unitMetrics[3] = new string[] { "MC Page Table Overflow", FormatLargeNumber(counterData.ctr3 * 64) + "B/s", FormatLargeNumber(counterData.ctr3) };

                float totalReqs = counterData.ctr0 + counterData.ctr1 + counterData.ctr2;
                results.overallMetrics = new string[] { "Overall", FormatLargeNumber(totalReqs * 64) + "B/s", FormatLargeNumber(totalReqs) };

                results.overallCounterValues = dataFabric.GetOverallCounterValues(counterData, "Page Hit", "Page Miss", "Page Conflict", "Page Table Overflow");
                return results;
            }
        }

        public class MemSource : MonitoringConfig
        {
            private PiledriverNorthbridge dataFabric;

            public string[] columns = new string[] { "Item", "Count * 64B", "Count", "Total Data" };
            public string GetHelpText() { return ""; }
            public MemSource(PiledriverNorthbridge dataFabric)
            {
                this.dataFabric = dataFabric;
            }

            public string GetConfigName() { return "Crossbar/DRAM"; }
            public string[] GetColumns() { return columns; }
            public void Initialize()
            {
                dataFabric.ProgramPerfCounters(
                    GetNBPerfCtlValue(0xE0, 0x3F, true, 0), // DRAM access
                    GetNBPerfCtlValue(0xE9, 0xA8, true, 0), // CPU to Mem
                    GetNBPerfCtlValue(0xE9, 0xA4, true, 0), // CPU to IO
                    GetNBPerfCtlValue(0xE9, 0xA2, true, 0)); // IO to Mem
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                NormalizedNbCounterData counterData = dataFabric.UpdateNbPerfCounterData();

                results.unitMetrics = new string[4][];
                results.unitMetrics[0] = new string[] { 
                    "DRAM Access", FormatLargeNumber(counterData.ctr0 * 64) + "B/s", 
                    FormatLargeNumber(counterData.ctr0),
                    FormatLargeNumber(counterData.ctr0total * 64) + "B" };
                results.unitMetrics[1] = new string[] { "CPU to Mem", FormatLargeNumber(counterData.ctr1 * 64) + "B/s", FormatLargeNumber(counterData.ctr1), "<" + FormatLargeNumber(counterData.ctr1total * 64) + "B" };
                results.unitMetrics[2] = new string[] { "CPU to IO", FormatLargeNumber(counterData.ctr2 * 64) + "B/s", FormatLargeNumber(counterData.ctr2), "<" + FormatLargeNumber(counterData.ctr2total * 64) + "B" };
                results.unitMetrics[3] = new string[] { "IO to Mem", FormatLargeNumber(counterData.ctr3 * 64) + "B/s", FormatLargeNumber(counterData.ctr3), "<" + FormatLargeNumber(counterData.ctr3total * 64) + "B" };

                float totalReqs = counterData.ctr0 + counterData.ctr1 + counterData.ctr2;
                results.overallMetrics = new string[] { "Overall", FormatLargeNumber(totalReqs * 64) + "B/s", FormatLargeNumber(totalReqs) };

                results.overallCounterValues = dataFabric.GetOverallCounterValues(counterData, "DRAM Access", "CPU to Mem", "CPU to IO", "IO to Mem");
                return results;
            }
        }

        public class L3Config : MonitoringConfig
        {
            private PiledriverNorthbridge dataFabric;

            public string[] columns = new string[] { "Item", "Hitrate", "Hit BW", "L3 Fill BW", "L3 Evictions, Writeback", "Total L3 BW", "Total L3 Data" };
            public string GetHelpText() { return ""; }
            public L3Config(PiledriverNorthbridge dataFabric)
            {
                this.dataFabric = dataFabric;
            }

            public string GetConfigName() { return "L3 Cache"; }
            public string[] GetColumns() { return columns; }
            public void Initialize()
            {
                dataFabric.ProgramPerfCounters(
                    GetNBPerfCtlValue(0xE0, 0xF7, true, 4), // L3 read request, all cores, all requests
                    GetNBPerfCtlValue(0xE1, 0xF7, true, 4), // L3 miss, as above
                    GetNBPerfCtlValue(0xE2, 0xFF, true, 4), // L3 fills
                    GetNBPerfCtlValue(0xE3, 0x8, true, 4)); // L3 modified evictions
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                NormalizedNbCounterData counterData = dataFabric.UpdateNbPerfCounterData();

                results.unitMetrics = null;

                float l3Hits = counterData.ctr0 - counterData.ctr1;
                float totalL3Hits = counterData.ctr0total - counterData.ctr1total;
                results.overallMetrics = new string[] { "Overall", 
                    FormatPercentage(l3Hits, counterData.ctr0), 
                    FormatLargeNumber(64 * l3Hits) + "B/s",
                    FormatLargeNumber(64 * counterData.ctr2) + "B/s",
                    FormatLargeNumber(64 * counterData.ctr3) + "B/s",
                    FormatLargeNumber(64 * (l3Hits + counterData.ctr2 + counterData.ctr3)) + "B/s",
                    FormatLargeNumber(64 * (l3Hits + counterData.ctr2total + counterData.ctr3total)) + "B",
                };

                results.overallCounterValues = dataFabric.GetOverallCounterValues(counterData, "L3 Read Request", "L3 Miss", "L3 Fill", "L3 Eviction, Modified");
                return results;
            }
        }

        public class L3Commands : MonitoringConfig
        {
            private PiledriverNorthbridge dataFabric;

            public string[] columns = new string[] { "Item", "Bandwidth", "Count" };
            public string GetHelpText() { return ""; }
            public L3Commands(PiledriverNorthbridge dataFabric)
            {
                this.dataFabric = dataFabric;
            }

            public string GetConfigName() { return "Cache Block Commands"; }
            public string[] GetColumns() { return columns; }
            public void Initialize()
            {
                dataFabric.ProgramPerfCounters(
                    GetNBPerfCtlValue(0xEA, 0x20, true, 0), // Change-To-Dirty
                    GetNBPerfCtlValue(0xEA, 0x14, true, 0), // Read Block/Read Block Modified
                    GetNBPerfCtlValue(0xEA, 0x8, true, 0), // Read Block Shared
                    GetNBPerfCtlValue(0xEA, 0x1, true, 0)); // Victim/Writeback
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                NormalizedNbCounterData counterData = dataFabric.UpdateNbPerfCounterData();

                results.unitMetrics = new string[4][];
                results.unitMetrics[0] = new string[] { "Read Block/Read Block Modified", FormatLargeNumber(counterData.ctr1 * 64) + "B/s", FormatLargeNumber(counterData.ctr1) };
                results.unitMetrics[1] = new string[] { "Read Block Shared", FormatLargeNumber(counterData.ctr2 * 64) + "B/s", FormatLargeNumber(counterData.ctr2) };
                results.unitMetrics[2] = new string[] { "Victim Block (Writeback)", FormatLargeNumber(counterData.ctr3 * 64) + "B/s", FormatLargeNumber(counterData.ctr3) };
                results.unitMetrics[3] = new string[] { "Change-to-Dirty", FormatLargeNumber(counterData.ctr0 * 64) + "B/s", FormatLargeNumber(counterData.ctr0) };

                float totalReqs = counterData.ctr1 + counterData.ctr2 + counterData.ctr3;
                results.overallMetrics = new string[] { "Overall", FormatLargeNumber(totalReqs * 64) + "B/s", FormatLargeNumber(totalReqs) };

                results.overallCounterValues = dataFabric.GetOverallCounterValues(counterData, "Change-To-Dirty", "Read Block/Modified", "Read Block Shared", "Victim Block");
                return results;
            }
        }

        public class CrossbarRequests : MonitoringConfig
        {
            private PiledriverNorthbridge dataFabric;

            public string[] columns = new string[] { "Item", "Count" };
            public string GetHelpText() { return ""; }
            public CrossbarRequests(PiledriverNorthbridge dataFabric)
            {
                this.dataFabric = dataFabric;
            }

            public string GetConfigName() { return "Crossbar Requests"; }
            public string[] GetColumns() { return columns; }
            public void Initialize()
            {
                dataFabric.ProgramPerfCounters(
                    GetNBPerfCtlValue(0xE9, 0xA8, true, 0), // CPU to mem
                    GetNBPerfCtlValue(0xE9, 0xA4, true, 0), // CPU to IO
                    GetNBPerfCtlValue(0xE9, 0xA2, true, 0), // IO to mem
                    GetNBPerfCtlValue(0xE9, 0xA1, true, 0)); // IO to IO
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                NormalizedNbCounterData counterData = dataFabric.UpdateNbPerfCounterData();

                results.unitMetrics = new string[4][];
                results.unitMetrics[0] = new string[] { "CPU to Mem", FormatLargeNumber(counterData.ctr0) };
                results.unitMetrics[1] = new string[] { "CPU to IO", FormatLargeNumber(counterData.ctr1) };
                results.unitMetrics[2] = new string[] { "IO to Mem", FormatLargeNumber(counterData.ctr2) };
                results.unitMetrics[3] = new string[] { "IO to IO", FormatLargeNumber(counterData.ctr3) };

                float totalReqs = counterData.ctr0 + counterData.ctr1 + counterData.ctr2;
                results.overallMetrics = new string[] { "Overall", FormatLargeNumber(totalReqs) };

                results.overallCounterValues = dataFabric.GetOverallCounterValues(counterData, "CPU to Mem", "CPU to IO", "IO to Mem", "IO to IO");
                return results;
            }
        }

        public class SriCommands : MonitoringConfig
        {
            private PiledriverNorthbridge dataFabric;

            public string[] columns = new string[] { "Item", "Count" };
            public string GetHelpText() { return ""; }
            public SriCommands(PiledriverNorthbridge dataFabric)
            {
                this.dataFabric = dataFabric;
            }

            public string GetConfigName() { return "System Request Interface"; }
            public string[] GetColumns() { return columns; }
            public void Initialize()
            {
                dataFabric.ProgramPerfCounters(
                    GetNBPerfCtlValue(0xEB, 0b110000, true, 0), // SzRd
                    GetNBPerfCtlValue(0xEB, 0b1100, true, 0), // Posted SzWr
                    GetNBPerfCtlValue(0xEB, 0b11, true, 0), // Non-Posted SzWr
                    GetNBPerfCtlValue(0xEB, 0xA1, true, 0)); // IO to IO, unused
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                NormalizedNbCounterData counterData = dataFabric.UpdateNbPerfCounterData();

                results.unitMetrics = new string[3][];
                results.unitMetrics[0] = new string[] { "Sized Read", FormatLargeNumber(counterData.ctr0) };
                results.unitMetrics[1] = new string[] { "Posted Sized Write", FormatLargeNumber(counterData.ctr1) };
                results.unitMetrics[2] = new string[] { "Non-Posted Sized Write", FormatLargeNumber(counterData.ctr2) };

                float totalReqs = counterData.ctr0 + counterData.ctr1 + counterData.ctr2;
                results.overallMetrics = new string[] { "Overall", FormatLargeNumber(totalReqs) };

                results.overallCounterValues = dataFabric.GetOverallCounterValues(counterData, "SzRd", "Posted SzWr", "Non-Posted SzWr", "Unused");
                return results;
            }
        }

        public class Probe : MonitoringConfig
        {
            private PiledriverNorthbridge dataFabric;

            public string[] columns = new string[] { "Item", "Count * 64B", "Count" };
            public string GetHelpText() { return ""; }
            public Probe(PiledriverNorthbridge dataFabric)
            {
                this.dataFabric = dataFabric;
            }

            public string GetConfigName() { return "Probe Response"; }
            public string[] GetColumns() { return columns; }
            public void Initialize()
            {
                dataFabric.ProgramPerfCounters(
                    GetNBPerfCtlValue(0xEC, 1, true, 0), // probe miss
                    GetNBPerfCtlValue(0xEC, 2, true, 0), // probe hit clean
                    GetNBPerfCtlValue(0xEC, 4, true, 0), // probe hit dirty without mem cancel
                    GetNBPerfCtlValue(0xEC, 8, true, 0)); // probe hit dirty with mem cancel
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                NormalizedNbCounterData counterData = dataFabric.UpdateNbPerfCounterData();

                results.unitMetrics = new string[4][];
                results.unitMetrics[0] = new string[] { "Probe Miss", FormatLargeNumber(counterData.ctr0 * 64) + "B/s", FormatLargeNumber(counterData.ctr0) };
                results.unitMetrics[1] = new string[] { "Probe Hit Clean", FormatLargeNumber(counterData.ctr1 * 64) + "B/s", FormatLargeNumber(counterData.ctr1) };
                results.unitMetrics[2] = new string[] { "Probe Hit Dirty w/o Mem Cancel", FormatLargeNumber(counterData.ctr2 * 64) + "B/s", FormatLargeNumber(counterData.ctr2) };
                results.unitMetrics[3] = new string[] { "Probe Hit Dirty w/Mem Cancel", FormatLargeNumber(counterData.ctr3 * 64) + "B/s", FormatLargeNumber(counterData.ctr3) };

                float totalReqs = counterData.ctr0 + counterData.ctr1 + counterData.ctr2 + counterData.ctr3;
                results.overallMetrics = new string[] { "Total", FormatLargeNumber(totalReqs * 64) + "B/s", FormatLargeNumber(totalReqs) };

                results.overallCounterValues = dataFabric.GetOverallCounterValues(counterData, "Probe Miss", "Probe Hit Clean", "Probe Hit Dirty w/o Mem Cancel", "Probe Hit Dirty w/Mem Cancel");
                return results;
            }
        }

        public class UpstreamRequests : MonitoringConfig
        {
            private PiledriverNorthbridge dataFabric;

            public string[] columns = new string[] { "Item", "Count * 64B", "Count" };
            public string GetHelpText() { return ""; }
            public UpstreamRequests(PiledriverNorthbridge dataFabric)
            {
                this.dataFabric = dataFabric;
            }

            public string GetConfigName() { return "Upstream Requsets"; }
            public string[] GetColumns() { return columns; }
            public void Initialize()
            {
                dataFabric.ProgramPerfCounters(
                    GetNBPerfCtlValue(0xEC, 0x10, true, 0), // Upstream display refresh/ISOC reads
                    GetNBPerfCtlValue(0xEC, 0x20, true, 0), // Upstream non-ISOC reads
                    GetNBPerfCtlValue(0xEC, 0x40, true, 0), // Upstream ISOC writes
                    GetNBPerfCtlValue(0xEC, 0x80, true, 0)); // Upstream non-ISOC writes
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                NormalizedNbCounterData counterData = dataFabric.UpdateNbPerfCounterData();

                results.unitMetrics = new string[4][];
                results.unitMetrics[0] = new string[] { "ISOC Read (Display)", FormatLargeNumber(counterData.ctr0 * 64) + "B/s", FormatLargeNumber(counterData.ctr0) };
                results.unitMetrics[1] = new string[] { "Non-ISOC Read", FormatLargeNumber(counterData.ctr1 * 64) + "B/s", FormatLargeNumber(counterData.ctr1) };
                results.unitMetrics[2] = new string[] { "ISOC Write", FormatLargeNumber(counterData.ctr2 * 64) + "B/s", FormatLargeNumber(counterData.ctr2) };
                results.unitMetrics[3] = new string[] { "Non-ISOC Write", FormatLargeNumber(counterData.ctr3 * 64) + "B/s", FormatLargeNumber(counterData.ctr3) };

                float totalReqs = counterData.ctr0 + counterData.ctr1 + counterData.ctr2 + counterData.ctr3;
                results.overallMetrics = new string[] { "Total", FormatLargeNumber(totalReqs * 64) + "B/s", FormatLargeNumber(totalReqs) };

                results.overallCounterValues = dataFabric.GetOverallCounterValues(counterData, "ISOC Read", "Non-ISOC Read", "ISOC Write", "Non-ISOC Write");
                return results;
            }
        }

        public class GartConfig : MonitoringConfig
        {
            private PiledriverNorthbridge dataFabric;

            public string GetHelpText() { return ""; }
            public GartConfig(PiledriverNorthbridge dataFabric)
            {
                this.dataFabric = dataFabric;
            }

            public string GetConfigName() { return "Graphics Addr Remap Table"; }
            public string[] GetColumns() { return columns; }
            public void Initialize()
            {
                dataFabric.ProgramPerfCounters(
                    GetNBPerfCtlValue(0xEE, 0x1, true, 0), // Aperture hit from CPU access
                    GetNBPerfCtlValue(0xEE, 0x2, true, 0), // Aperture hit from IO access
                    GetNBPerfCtlValue(0xEE, 0x4, true, 0), // Aperture miss
                    GetNBPerfCtlValue(0xEE, 0b10001000, true, 0)); // GART table walk in progress
            }

            public string[] columns = new string[] { "Item", "Value" };

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                NormalizedNbCounterData counterData = dataFabric.UpdateNbPerfCounterData();
                List<string[]> unitMetricsList = new List<string[]>();
                unitMetricsList.Add(new string[] { "CPU Req, Aperture Hit", FormatLargeNumber(counterData.ctr0)});
                unitMetricsList.Add(new string[] { "IO Req, Aperture Hit", FormatLargeNumber(counterData.ctr1) });
                unitMetricsList.Add(new string[] { "GART Table Walk In Progress", FormatLargeNumber(counterData.ctr3) });

                results.unitMetrics = unitMetricsList.ToArray();
                results.overallMetrics = new string[] { "Aperture Hitrate", 
                    FormatPercentage(counterData.ctr0 + counterData.ctr1, counterData.ctr0 + counterData.ctr1 + counterData.ctr2) };


                results.overallCounterValues = dataFabric.GetOverallCounterValues(counterData, "CPU access GART aperture hit", "IO access GART aperture hit", "GART miss", "GART table walk in progress");
                return results;
            }
        }
    }
}

using PmcReader.Interop;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;

namespace PmcReader.Intel
{
    public class SkylakeClientL3 : SkylakeClientUncore
    {
        /// <summary>
        /// Number of L3 cache coherency boxes
        /// </summary>
        public int CboCount;
        public NormalizedCboCounterData[] cboData;
        public NormalizedCboCounterData cboTotals;

        public SkylakeClientL3()
        {
            ulong cboConfig;
            architectureName = "Skylake Client L3";

            // intel developer manual table 2-30 says bits 0-3 encode number of C-Box
            // "subtract one to determine number of CBo units"
            Ring0.ReadMsr(MSR_UNC_CBO_CONFIG, out cboConfig);
            if ((cboConfig & 0xF) == 10) CboCount = 10;
            else CboCount = (int)((cboConfig & 0xF) - 1); // but not for the 109000k?
            cboData = new NormalizedCboCounterData[CboCount];

            List<MonitoringConfig> monitoringConfigList = new List<MonitoringConfig>();
            monitoringConfigList.Add(new HitrateConfig(this));
            monitoringConfigList.Add(new SnoopHitConfig(this));
            monitoringConfigList.Add(new HitsCategoryConfig(this, "Data?", 0x80 | 0b10));
            monitoringConfigList.Add(new HitsCategoryConfig(this, "Code?", 0x80 | 0b100));
            monitoringConfigList.Add(new HitsCategoryConfig(this, "Modified", 0x80 | 0b1));
            monitoringConfigs = monitoringConfigList.ToArray();
        }

        public class NormalizedCboCounterData
        {
            public float ctr0;
            public float ctr1;
            public ulong ctr0Total;
            public ulong ctr1Total;
        }

        public void InitializeCboTotals()
        {
            if (cboTotals == null)
            {
                cboTotals = new NormalizedCboCounterData();
            }

            cboTotals.ctr0 = 0;
            cboTotals.ctr1 = 0;
        }

        public void UpdateCboCounterData(uint cboIdx)
        {
            float normalizationFactor = GetNormalizationFactor((int)cboIdx);
            ulong ctr0 = ReadAndClearMsr(MSR_UNC_CBO_PERFCTR0_base + MSR_UNC_CBO_increment * cboIdx);
            ulong ctr1 = ReadAndClearMsr(MSR_UNC_CBO_PERFCTR1_base + MSR_UNC_CBO_increment * cboIdx);

            if (cboData[cboIdx] == null)
            {
                cboData[cboIdx] = new NormalizedCboCounterData();
            }

            cboData[cboIdx].ctr0 = ctr0 * normalizationFactor;
            cboData[cboIdx].ctr1 = ctr1 * normalizationFactor;
            cboData[cboIdx].ctr0Total += ctr0;
            cboData[cboIdx].ctr1Total += ctr1;
            cboTotals.ctr0 += cboData[cboIdx].ctr0;
            cboTotals.ctr1 += cboData[cboIdx].ctr1;
            cboTotals.ctr0Total += ctr0;
            cboTotals.ctr1Total += ctr1;
        }

        public Tuple<string, float>[] GetOverallCounterValues(string ctr0, string ctr1)
        {
            Tuple<string, float>[] retval = new Tuple<string, float>[2];
            retval[0] = new Tuple<string, float>(ctr0, cboTotals.ctr0);
            retval[1] = new Tuple<string, float>(ctr1, cboTotals.ctr1);
            return retval;
        }

        public class HitrateConfig : MonitoringConfig
        {
            private SkylakeClientL3 cpu;
            public string GetConfigName() { return "L3 Hitrate"; }

            public HitrateConfig(SkylakeClientL3 intelCpu)
            {
                cpu = intelCpu;
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.EnableUncoreCounters();
                for (uint cboIdx = 0; cboIdx < cpu.CboCount; cboIdx++)
                {
                    // Event 0x34 = uncore cbo cache lookup
                    // Bit 0 = Modified state
                    // Bit 1, 2 = Exclusive, Shared states
                    // Bit 3 = Invalid state (miss)
                    // Bit 4 = Read
                    // Bit 5 = Write
                    // Bit 6 = ???
                    // Bit 7 = Any
                    // 0x34 = L3 lookups, 0xFF = all lookups
                    Ring0.WriteMsr(MSR_UNC_CBO_PERFEVTSEL0_base + MSR_UNC_CBO_increment * cboIdx,
                        GetUncorePerfEvtSelRegisterValue(0x34, 0x8F, false, false, true, false, 0));

                    // 0x34 = L3 lookups, high 4 bits = cacheable read | cacheable write | external snoop | irq/ipq
                    // low 4 bits = M | ES | I, so select I to count misses
                    Ring0.WriteMsr(MSR_UNC_CBO_PERFEVTSEL1_base + MSR_UNC_CBO_increment * cboIdx,
                        GetUncorePerfEvtSelRegisterValue(0x34, 0x88, false, false, true, false, 0));
                }
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[cpu.CboCount][];
                cpu.InitializeCboTotals();
                for (uint cboIdx = 0; cboIdx < cpu.CboCount; cboIdx++)
                {
                    cpu.UpdateCboCounterData(cboIdx);
                    results.unitMetrics[cboIdx] = computeMetrics("CBo " + cboIdx, cpu.cboData[cboIdx]);
                }

                results.overallMetrics = computeMetrics("Overall", cpu.cboTotals);
                results.overallCounterValues = cpu.GetOverallCounterValues("L3 Lookups", "L3 Misses");
                return results;
            }

            public string[] columns = new string[] { "Item", "Hitrate", "Hit BW", "All Lookups", "I state", "Total Hit Data" };

            public string GetHelpText() { return ""; }

            private string[] computeMetrics(string label, NormalizedCboCounterData counterData)
            {
                return new string[] { label,
                    string.Format("{0:F2}%", 100 * (1 - counterData.ctr1 / counterData.ctr0)),
                    FormatLargeNumber((counterData.ctr0 - counterData.ctr1) * 64) + "B/s",
                    FormatLargeNumber(counterData.ctr0),
                    FormatLargeNumber(counterData.ctr1),
                    FormatLargeNumber((counterData.ctr0Total - counterData.ctr1Total) * 64) + "B"};
            }
        }

        public class HitsCategoryConfig : MonitoringConfig
        {
            private SkylakeClientL3 cpu;
            private string category;
            private byte umask;
            public string GetConfigName() { return "L3 Hits, " + category; }

            public HitsCategoryConfig(SkylakeClientL3 intelCpu, string category, byte umask)
            {
                this.cpu = intelCpu;
                this.category = category;
                this.umask = umask;
                this.columns = new string[] { "Item", "Hit BW", category + " Hit BW", "% " + category + " hits" };
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.EnableUncoreCounters();
                for (uint cboIdx = 0; cboIdx < cpu.CboCount; cboIdx++)
                {
                    // Event 0x34 = uncore cbo cache lookup
                    // Bit 0 = Modified state
                    // Bit 1, 2 = Exclusive, Shared states
                    // Bit 3 = Invalid state (miss)
                    // Bit 4 = Read
                    // Bit 5 = Write
                    // Bit 6 = ???
                    // Bit 7 = Any
                    // 0x34 = L3 lookups, 0xFF = all lookups

                    // L3 hits
                    Ring0.WriteMsr(MSR_UNC_CBO_PERFEVTSEL0_base + MSR_UNC_CBO_increment * cboIdx,
                        GetUncorePerfEvtSelRegisterValue(0x34, 0x80 | 0b111, false, false, true, false, 0));

                    // Bit one (E or S?)
                    Ring0.WriteMsr(MSR_UNC_CBO_PERFEVTSEL1_base + MSR_UNC_CBO_increment * cboIdx,
                        GetUncorePerfEvtSelRegisterValue(0x34, umask, false, false, true, false, 0));
                }
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[cpu.CboCount][];
                cpu.InitializeCboTotals();
                for (uint cboIdx = 0; cboIdx < cpu.CboCount; cboIdx++)
                {
                    cpu.UpdateCboCounterData(cboIdx);
                    results.unitMetrics[cboIdx] = computeMetrics("CBo " + cboIdx, cpu.cboData[cboIdx]);
                }

                results.overallMetrics = computeMetrics("Overall", cpu.cboTotals);
                results.overallCounterValues = cpu.GetOverallCounterValues("L3 Hits", this.category + " L3 Hits");
                return results;
            }

            public string[] columns;

            public string GetHelpText() { return ""; }

            private string[] computeMetrics(string label, NormalizedCboCounterData counterData)
            {
                return new string[] { label,
                    FormatLargeNumber(counterData.ctr0 * 64) + "B/s",
                    FormatLargeNumber(counterData.ctr1 * 64) + "B/s",
                    FormatPercentage(counterData.ctr1, counterData.ctr0)};
            }
        }

        public class SnoopHitConfig : MonitoringConfig
        {
            private SkylakeClientL3 cpu;
            public string GetConfigName() { return "Snoop Hits"; }

            public SnoopHitConfig(SkylakeClientL3 intelCpu)
            {
                cpu = intelCpu;
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                ThreadAffinity.Set(0x1);
                cpu.EnableUncoreCounters();
                for (uint cboIdx = 0; cboIdx < cpu.CboCount; cboIdx++)
                {
                    // CBo sent a snoop that hit a non-modified line
                    Ring0.WriteMsr(MSR_UNC_CBO_PERFEVTSEL0_base + MSR_UNC_CBO_increment * cboIdx,
                        GetUncorePerfEvtSelRegisterValue(0x22, 0x44, false, false, true, false, 0));

                    // CBo sent a snoop that hit a modified line
                    Ring0.WriteMsr(MSR_UNC_CBO_PERFEVTSEL1_base + MSR_UNC_CBO_increment * cboIdx,
                        GetUncorePerfEvtSelRegisterValue(0x22, 0x48, false, false, true, false, 0));
                }
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[cpu.CboCount][];
                cpu.InitializeCboTotals();
                ThreadAffinity.Set(0x1);
                for (uint cboIdx = 0; cboIdx < cpu.CboCount; cboIdx++)
                {
                    cpu.UpdateCboCounterData(cboIdx);
                    results.unitMetrics[cboIdx] = computeMetrics("CBo " + cboIdx, cpu.cboData[cboIdx]);
                }

                results.overallMetrics = computeMetrics("Overall", cpu.cboTotals);
                return results;
            }

            public string[] columns = new string[] { "Item", "Snoop Hit BW", "Snoop Hit(M) BW", "Snoop Hit(non-M) BW", "Snoop Hits" };

            public string GetHelpText() { return ""; }

            private string[] computeMetrics(string label, NormalizedCboCounterData counterData)
            {
                return new string[] { label,
                    FormatLargeNumber((counterData.ctr0 + counterData.ctr1) * 64) + "B/s",
                    FormatLargeNumber(counterData.ctr0 * 64) + "B/s",
                    FormatLargeNumber(counterData.ctr1 * 64) + "B/s",
                    FormatLargeNumber(counterData.ctr0 + counterData.ctr1)};
            }
        }
    }
}

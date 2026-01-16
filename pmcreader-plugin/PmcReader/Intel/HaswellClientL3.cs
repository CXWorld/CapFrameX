using PmcReader.Interop;
using System;

namespace PmcReader.Intel
{
    public class HaswellClientL3 : HaswellClientUncore
    {
        /// <summary>
        /// Number of L3 cache coherency boxes
        /// </summary>
        public int CboCount;
        public NormalizedCboCounterData[] cboData;
        public NormalizedCboCounterData cboTotals;

        public HaswellClientL3()
        {
            ulong cboConfig;
            architectureName = "Haswell Client L3";

            // intel developer manual table 2-30 syas bits 0-3 encode number of C-Box
            // "derive value by -1"
            Ring0.ReadMsr(MSR_UNC_CBO_CONFIG, out cboConfig);
            CboCount = (int)((cboConfig & 0x7) - 1);
            cboData = new NormalizedCboCounterData[CboCount];

            monitoringConfigs = new MonitoringConfig[3];
            monitoringConfigs[0] = new HitrateConfig(this);
            monitoringConfigs[1] = new SnoopHitConfig(this);
            monitoringConfigs[2] = new SnoopInvalidateConfig(this);
        }

        public class NormalizedCboCounterData
        {
            public float ctr0;
            public float ctr1;
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
            cboTotals.ctr0 += cboData[cboIdx].ctr0;
            cboTotals.ctr1 += cboData[cboIdx].ctr1;
        }

        public Tuple<string, float>[] GetCboOverallCounterValues(string ctr0, string ctr1)
        {
            Tuple<string, float>[] retval = new Tuple<string, float>[2];
            retval[0] = new Tuple<string, float>(ctr0, cboTotals.ctr0);
            retval[1] = new Tuple<string, float>(ctr1, cboTotals.ctr1);
            return retval;
        }

        public class HitrateConfig : MonitoringConfig
        {
            private HaswellClientL3 cpu;
            public string GetConfigName() { return "L3 Hitrate"; }

            public HitrateConfig(HaswellClientL3 intelCpu)
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
                    // 0x34 = L3 lookups, 0xFF = all lookups
                    Ring0.WriteMsr(MSR_UNC_CBO_PERFEVTSEL0_base + MSR_UNC_CBO_increment * cboIdx,
                        GetUncorePerfEvtSelRegisterValue(0x34, 0xFF, false, false, true, false, 0));

                    // 0x34 = L3 lookups, high 4 bits = cacheable read | cacheable write | external snoop | irq/ipq
                    // low 4 bits = M | ES | I, so select I to count misses
                    Ring0.WriteMsr(MSR_UNC_CBO_PERFEVTSEL1_base + MSR_UNC_CBO_increment * cboIdx,
                        GetUncorePerfEvtSelRegisterValue(0x34, 0xF8, false, false, true, false, 0));
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
                results.overallCounterValues = cpu.GetCboOverallCounterValues("L3 Lookups", "L3 Miss");
                return results;
            }

            public string[] columns = new string[] { "Item", "Hitrate", "Hit BW", "All Lookups", "L3 Miss" };

            public string GetHelpText() { return ""; }

            private string[] computeMetrics(string label, NormalizedCboCounterData counterData)
            {
                return new string[] { label,
                    string.Format("{0:F2}%", 100 * (1 - counterData.ctr1 / counterData.ctr0)),
                    FormatLargeNumber((counterData.ctr0 - counterData.ctr1) * 64) + "B/s",
                    FormatLargeNumber(counterData.ctr0),
                    FormatLargeNumber(counterData.ctr1)};
            }
        }

        public class SnoopInvalidateConfig : MonitoringConfig
        {
            private HaswellClientL3 cpu;
            public string GetConfigName() { return "Snoop Invalidations"; }

            public SnoopInvalidateConfig(HaswellClientL3 intelCpu)
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
                    // 0x22 = Snoop response, 0xFF = all responses
                    Ring0.WriteMsr(MSR_UNC_CBO_PERFEVTSEL0_base + MSR_UNC_CBO_increment * cboIdx,
                        GetUncorePerfEvtSelRegisterValue(0x22, 0xFF, false, false, true, false, 0));

                    // 0x22 = Snoop response, umask 0x2 = non-modified line invalidated, umask 0x10 = modified line invalidated
                    // high 3 bits of umask = filter. 0x20 = external snoop, 0x40 = core memory request, 0x80 = L3 eviction
                    Ring0.WriteMsr(MSR_UNC_CBO_PERFEVTSEL1_base + MSR_UNC_CBO_increment * cboIdx,
                        GetUncorePerfEvtSelRegisterValue(0x22, 0x12 | 0x20 | 0x40 | 0x80, false, false, true, false, 0));
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

            public string[] columns = new string[] { "Item", "Invalidate Resp %", "Invalidate BW", "All Snoop Responses", "Core Cache Lines Invalidated" };

            public string GetHelpText() { return ""; }

            private string[] computeMetrics(string label, NormalizedCboCounterData counterData)
            {
                return new string[] { label,
                    string.Format("{0:F2}%", 100 * (counterData.ctr1 / counterData.ctr0)),
                    FormatLargeNumber(counterData.ctr1 * 64) + "B/s",
                    FormatLargeNumber(counterData.ctr0),
                    FormatLargeNumber(counterData.ctr1)};
            }
        }

        public class SnoopHitConfig : MonitoringConfig
        {
            private HaswellClientL3 cpu;
            public string GetConfigName() { return "Snoop Hits"; }

            public SnoopHitConfig(HaswellClientL3 intelCpu)
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
                    // 0x22 = Snoop response, 0xFF = all responses
                    Ring0.WriteMsr(MSR_UNC_CBO_PERFEVTSEL0_base + MSR_UNC_CBO_increment * cboIdx,
                        GetUncorePerfEvtSelRegisterValue(0x22, 0xFF, false, false, true, false, 0));

                    // 0x22 = Snoop response, umask 0x4 = non-modified line hit, umask 0x8 = modified line hit
                    // high 3 bits of umask = filter. 0x20 = external snoop, 0x40 = core memory request, 0x80 = L3 eviction
                    Ring0.WriteMsr(MSR_UNC_CBO_PERFEVTSEL1_base + MSR_UNC_CBO_increment * cboIdx,
                        GetUncorePerfEvtSelRegisterValue(0x22, 0x4 | 0x8 | 0x20 | 0x40 | 0x80, false, false, true, false, 0));
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

            public string[] columns = new string[] { "Item", "Snoop Hitrate", "Snoop Hit BW", "All Snoop Responses", "Snoop Hits" };

            public string GetHelpText() { return ""; }

            private string[] computeMetrics(string label, NormalizedCboCounterData counterData)
            {
                return new string[] { label,
                    string.Format("{0:F2}%", 100 * (counterData.ctr1 / counterData.ctr0)),
                    FormatLargeNumber(counterData.ctr1 * 64) + "B/s",
                    FormatLargeNumber(counterData.ctr0),
                    FormatLargeNumber(counterData.ctr1)};
            }
        }
    }
}

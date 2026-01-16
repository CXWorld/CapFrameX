using PmcReader.Interop;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;

namespace PmcReader.Intel
{
    public class AlderLakeL3 : AlderLakeUncore
    {
        /// <summary>
        /// Number of L3 cache coherency boxes
        /// </summary>
        public int CboCount;
        public NormalizedCboCounterData[] cboData;
        public NormalizedCboCounterData cboTotals;

        public AlderLakeL3()
        {
            ulong cboConfig;
            architectureName = "Alder Lake Client L3";

            // intel developer manual table 2-30 says bits 0-3 encode number of C-Box
            // ADL no longer requires subtracting one from the reported C-Box count, unlike Haswell and Skylake
            Ring0.ReadMsr(MSR_UNC_CBO_CONFIG, out cboConfig);
            CboCount = (int)(cboConfig & 0xF);
            cboData = new NormalizedCboCounterData[CboCount];

            List<MonitoringConfig> monitoringConfigList = new List<MonitoringConfig>();
            monitoringConfigList.Add(new HitrateConfig(this));
            monitoringConfigs = monitoringConfigList.ToArray();
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

        public Tuple<string, float>[] GetOverallCounterValues(string ctr0, string ctr1)
        {
            Tuple<string, float>[] retval = new Tuple<string, float>[2];
            retval[0] = new Tuple<string, float>(ctr0, cboTotals.ctr0);
            retval[1] = new Tuple<string, float>(ctr1, cboTotals.ctr1);
            return retval;
        }

        public class HitrateConfig : MonitoringConfig
        {
            private AlderLakeL3 cpu;
            public string GetConfigName() { return "L3 Hitrate"; }

            public HitrateConfig(AlderLakeL3 intelCpu)
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
                    // Reusing Skylake events since Intel has not documented uncore events for arches after that
                    Ring0.WriteMsr(MSR_UNC_CBO_PERFEVTSEL0_base + MSR_UNC_CBO_increment * cboIdx,
                        GetUncorePerfEvtSelRegisterValue(0x34, 0x8F, false, false, true, false, 0));
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

            public string[] columns = new string[] { "Item", "Hitrate", "Hit BW", "All Lookups", "I state" };

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
    }
}

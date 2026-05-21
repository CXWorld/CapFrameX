using PmcReader.Interop;
using System;
using System.Collections.Generic;

namespace PmcReader.Intel
{
    public class MeteorLakeL3 : MeteorLakeUncore
    {
        /// <summary>
        /// Number of L3 cache coherency boxes
        /// </summary>
        public int CboCount;
        public NormalizedCboCounterData[] cboData;
        public NormalizedCboCounterData cboTotals;

        public MeteorLakeL3()
        {
            ulong cboConfig;
            architectureName = "Meteor Lake Client L3";

            // Verbatim from Linux perf code
            Ring0.ReadMsr(MTL_UNC_CBO_CONFIG, out cboConfig);
            CboCount = (int)(cboConfig & MTL_UNC_NUM_CBO_MASK);
            cboData = new NormalizedCboCounterData[CboCount];

            List<MonitoringConfig> monitoringConfigList = new List<MonitoringConfig>();
            monitoringConfigList.Add(new HitrateConfig(this));
            monitoringConfigs = monitoringConfigList.ToArray();
        }

        public class NormalizedCboCounterData
        {
            public float ctr0;
            public float ctr1;
            public ulong totalCtr0;
            public ulong totalCtr1;
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
            ulong ctr0 = ReadAndClearMsr(MTL_UNC_CBO_CTR + MTL_UNC_INCREMENT * cboIdx);
            ulong ctr1 = ReadAndClearMsr(MTL_UNC_CBO_CTR + MTL_UNC_INCREMENT * cboIdx + 1);

            if (cboData[cboIdx] == null)
            {
                cboData[cboIdx] = new NormalizedCboCounterData();
            }

            cboData[cboIdx].ctr0 = ctr0 * normalizationFactor;
            cboData[cboIdx].ctr1 = ctr1 * normalizationFactor;
            cboData[cboIdx].totalCtr0 += ctr0;
            cboData[cboIdx].totalCtr1 += ctr1;
            cboTotals.ctr0 += cboData[cboIdx].ctr0;
            cboTotals.ctr1 += cboData[cboIdx].ctr1;
            cboTotals.totalCtr0 += ctr0;
            cboTotals.totalCtr1 += ctr1;
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
            private MeteorLakeL3 cpu;
            public string GetConfigName() { return "L3 Hitrate"; }

            public HitrateConfig(MeteorLakeL3 intelCpu)
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
                    Ring0.WriteMsr(MTL_UNC_CBO_CTRL + MTL_UNC_INCREMENT * cboIdx,
                        GetUncorePerfEvtSelRegisterValue(0x34, 0x8F, false, false, true, false, 0));
                    Ring0.WriteMsr(MTL_UNC_CBO_CTRL + MTL_UNC_INCREMENT * cboIdx + 1,
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

            public string[] columns = new string[] { "Item", "Hitrate", "Hit BW", "All Lookups", "I state", "Hit Data" };

            public string GetHelpText() { return ""; }

            private string[] computeMetrics(string label, NormalizedCboCounterData counterData)
            {
                return new string[] { label,
                    string.Format("{0:F2}%", 100 * (1 - counterData.ctr1 / counterData.ctr0)),
                    FormatLargeNumber((counterData.ctr0 - counterData.ctr1) * 64) + "B/s",
                    FormatLargeNumber(counterData.ctr0),
                    FormatLargeNumber(counterData.ctr1),
                    FormatLargeNumber(64 *(counterData.totalCtr0 - counterData.totalCtr1)) + "B"};
            }
        }
    }
}

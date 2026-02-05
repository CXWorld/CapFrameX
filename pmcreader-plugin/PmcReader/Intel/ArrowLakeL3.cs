using PmcReader.Interop;
using System;
using System.Collections.Generic;

namespace PmcReader.Intel
{
    /// <summary>
    /// Arrow Lake L3 monitoring using HAC_CBO (Home Agent CBox)
    /// Arrow Lake does not have traditional CBO with L3 lookup events (0x34).
    /// Instead, it uses HAC_CBO with TOR allocation events (0x35).
    /// </summary>
    public class ArrowLakeL3 : MeteorLakeUncore
    {
        /// <summary>
        /// Number of HAC_CBO units (typically 2 on client parts)
        /// </summary>
        public int HacCboCount = 2;
        public NormalizedHacCboCounterData[] hacCboData;
        public NormalizedHacCboCounterData hacCboTotals;

        public ArrowLakeL3()
        {
            architectureName = "Arrow Lake L3";
            hacCboData = new NormalizedHacCboCounterData[HacCboCount];

            List<MonitoringConfig> monitoringConfigList = new List<MonitoringConfig>();
            monitoringConfigList.Add(new TorAllocationConfig(this));
            monitoringConfigs = monitoringConfigList.ToArray();
        }

        public class NormalizedHacCboCounterData
        {
            public float ctr0;  // TOR allocations ALL
            public float ctr1;  // TOR allocations DRD
            public ulong totalCtr0;
            public ulong totalCtr1;
        }

        public void InitializeHacCboTotals()
        {
            if (hacCboTotals == null)
            {
                hacCboTotals = new NormalizedHacCboCounterData();
            }

            hacCboTotals.ctr0 = 0;
            hacCboTotals.ctr1 = 0;
        }

        public void UpdateHacCboCounterData(uint boxIdx)
        {
            float normalizationFactor = GetNormalizationFactor((int)boxIdx);
            ulong ctr0 = ReadAndClearMsr(MTL_UNC_HAC_CBO_CTR + MTL_UNC_INCREMENT * boxIdx);
            ulong ctr1 = ReadAndClearMsr(MTL_UNC_HAC_CBO_CTR + MTL_UNC_INCREMENT * boxIdx + 1);

            if (hacCboData[boxIdx] == null)
            {
                hacCboData[boxIdx] = new NormalizedHacCboCounterData();
            }

            hacCboData[boxIdx].ctr0 = ctr0 * normalizationFactor;
            hacCboData[boxIdx].ctr1 = ctr1 * normalizationFactor;
            hacCboData[boxIdx].totalCtr0 += ctr0;
            hacCboData[boxIdx].totalCtr1 += ctr1;
            hacCboTotals.ctr0 += hacCboData[boxIdx].ctr0;
            hacCboTotals.ctr1 += hacCboData[boxIdx].ctr1;
            hacCboTotals.totalCtr0 += ctr0;
            hacCboTotals.totalCtr1 += ctr1;
        }

        public Tuple<string, float>[] GetOverallCounterValues(string ctr0, string ctr1)
        {
            Tuple<string, float>[] retval = new Tuple<string, float>[2];
            retval[0] = new Tuple<string, float>(ctr0, hacCboTotals.ctr0);
            retval[1] = new Tuple<string, float>(ctr1, hacCboTotals.ctr1);
            return retval;
        }

        /// <summary>
        /// Monitor L3 TOR allocations using HAC_CBO events.
        /// Event 0x35 = UNC_HAC_CBO_TOR_ALLOCATION
        /// Umask 0x08 = ALL (all entries allocated including retries)
        /// Umask 0x01 = DRD (coherent data reads + prefetches, cacheable)
        /// </summary>
        public class TorAllocationConfig : MonitoringConfig
        {
            private ArrowLakeL3 cpu;
            public string GetConfigName() { return "L3 TOR Allocations"; }

            public TorAllocationConfig(ArrowLakeL3 intelCpu)
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

                // Enable box control for sNCU
                ulong boxEnable = 1UL << 29;
                Ring0.WriteMsr(MTL_UNC_SNCU_BOX_CTRL, boxEnable);

                for (uint boxIdx = 0; boxIdx < cpu.HacCboCount; boxIdx++)
                {
                    // Counter 0: TOR_ALLOCATION.ALL (event 0x35, umask 0x08)
                    // All entries allocated including retries
                    Ring0.WriteMsr(MTL_UNC_HAC_CBO_CTRL + MTL_UNC_INCREMENT * boxIdx,
                        GetUncorePerfEvtSelRegisterValue(0x35, 0x08, false, false, true, false, 0));

                    // Counter 1: TOR_ALLOCATION.DRD (event 0x35, umask 0x01)
                    // Coherent data reads + prefetches (cacheable)
                    Ring0.WriteMsr(MTL_UNC_HAC_CBO_CTRL + MTL_UNC_INCREMENT * boxIdx + 1,
                        GetUncorePerfEvtSelRegisterValue(0x35, 0x01, false, false, true, false, 0));
                }
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[cpu.HacCboCount][];
                cpu.InitializeHacCboTotals();

                for (uint boxIdx = 0; boxIdx < cpu.HacCboCount; boxIdx++)
                {
                    cpu.UpdateHacCboCounterData(boxIdx);
                    results.unitMetrics[boxIdx] = computeMetrics("HAC_CBO " + boxIdx, cpu.hacCboData[boxIdx]);
                }

                results.overallMetrics = computeMetrics("Overall", cpu.hacCboTotals);
                results.overallCounterValues = cpu.GetOverallCounterValues("TOR All", "TOR DRD");
                return results;
            }

            public string[] columns = new string[] { "Item", "DRD %", "DRD BW", "All Alloc", "DRD Alloc", "Total DRD Data" };

            public string GetHelpText()
            {
                return "HAC_CBO TOR allocations. DRD = coherent data reads + prefetches.";
            }

            private string[] computeMetrics(string label, NormalizedHacCboCounterData counterData)
            {
                // DRD % = percentage of allocations that are data reads
                // DRD BW = estimated bandwidth from data read allocations (64 bytes per cacheline)
                float drdPercent = counterData.ctr0 > 0 ? 100 * counterData.ctr1 / counterData.ctr0 : 0;
                return new string[] { label,
                    string.Format("{0:F2}%", drdPercent),
                    FormatLargeNumber(counterData.ctr1 * 64) + "B/s",
                    FormatLargeNumber(counterData.ctr0),
                    FormatLargeNumber(counterData.ctr1),
                    FormatLargeNumber(counterData.totalCtr1 * 64) + "B"};
            }
        }
    }
}

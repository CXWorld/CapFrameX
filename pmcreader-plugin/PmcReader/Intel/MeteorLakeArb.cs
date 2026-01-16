using PmcReader.Interop;
using System;
using System.Collections.Generic;

namespace PmcReader.Intel
{
    public class MeteorLakeArb : MeteorLakeUncore
    {
        private ulong lastSncuClk, lastCncuClk;
        public ArbCounterTotals arbCounterTotals;

        public MeteorLakeArb()
        {
            architectureName = "Meteor Lake ARB";
            lastSncuClk = 0;
            List<MonitoringConfig> arbMonitoringConfigs = new List<MonitoringConfig>();
            arbMonitoringConfigs.Add(new FixedCounters(this));
            arbMonitoringConfigs.Add(new ArbCounters(this));
            monitoringConfigs = arbMonitoringConfigs.ToArray();
            arbCounterTotals = new ArbCounterTotals();
        }

        public class NormalizedArbCounterData
        {
            public float sncuUncoreClk;

            /// <summary>
            /// Documented as UCLK (UNC_CLOCK.SOCKET) cycles
            /// </summary>
            public float cncuUncoreClk;
            public float[] arbCtr0;
            public float[] arbCtr1;
            public float[] hacArbCtr0;
            public float[] hacArbCtr1;
            public float[] hacCboCtr0;
            public float[] hacCboCtr1;
        }

        public class ArbCounterTotals
        {
            public ulong totalSncuUncoreClk;
            public ulong totalCncuUncoreClock;
            public ulong totalArbCtr0;
            public ulong totalArbCtr1;
            public ulong totalHacArbCtr0;
            public ulong totalHacArbCtr1;
            public ulong totalHacCboCtr0;
            public ulong totalHacCboCtr1;
        }

        public void InitializeFixedCounters()
        {
            ulong boxEnable = 1UL << 29;
            Ring0.WriteMsr(MTL_UNC_SNCU_BOX_CTRL, boxEnable);
            Ring0.WriteMsr(MTL_UNC_CNCU_BOX_CTRL, boxEnable);

            // 0xFF = clockticks, bit 22 = enable
            // cNCU = socket uncore clocks from Intel's description
            // reaches 3.3 GHz and likely corresponds to uncore clk on the CPU tile
            // sNCU could be socket uncore clock for the IO die. 
            // reaches 2.4 GHz
            Ring0.WriteMsr(MTL_UNC_SNCU_FIXED_CTRL, 0xFF | (1UL << 22));
            Ring0.WriteMsr(MTL_UNC_CNCU_FIXED_CTRL, 0xFF | (1UL << 22));
            Ring0.WriteMsr(MTL_UNC_SNCU_FIXED_CTR, 0);
            Ring0.WriteMsr(MTL_UNC_CNCU_FIXED_CTR, 0);
        }

        public NormalizedArbCounterData UpdateArbCounterData()
        {
            NormalizedArbCounterData rc = new NormalizedArbCounterData();
            float normalizationFactor = GetNormalizationFactor(0);
            ulong sncuClk, cncuClk, elapsedSncuClk, elapsedCncuClk;
            rc.arbCtr0 = new float[2];
            rc.arbCtr1 = new float[2];
            rc.hacCboCtr0 = new float[2];
            rc.hacCboCtr1 = new float[2];
            rc.hacArbCtr0 = new float[2];
            rc.hacArbCtr1 = new float[2];

            // Fixed counters
            Ring0.ReadMsr(MTL_UNC_SNCU_FIXED_CTR, out sncuClk);
            Ring0.ReadMsr(MTL_UNC_CNCU_FIXED_CTR, out cncuClk);

            // MSR_UNC_PERF_FIXED_CTR is 48 bits wide, upper bits are reserved
            sncuClk &= 0xFFFFFFFFFFFF;
            elapsedSncuClk = sncuClk;
            if (sncuClk > lastSncuClk)
                elapsedSncuClk = sncuClk - lastSncuClk;
            lastSncuClk = sncuClk;

            cncuClk &= 0xFFFFFFFFFFFF;
            elapsedCncuClk = cncuClk;
            if (cncuClk > lastCncuClk)
                elapsedCncuClk = cncuClk - lastCncuClk;
            lastCncuClk = cncuClk;

            rc.sncuUncoreClk = elapsedSncuClk * normalizationFactor;
            rc.cncuUncoreClk = elapsedCncuClk * normalizationFactor;

            this.arbCounterTotals.totalCncuUncoreClock += elapsedSncuClk;
            this.arbCounterTotals.totalSncuUncoreClk += elapsedSncuClk;

            for (uint boxIdx = 0; boxIdx < 2; boxIdx++)
            {
                ulong arbCtr0 = ReadAndClearMsr(MTL_UNC_ARB_CTR + (boxIdx * MTL_UNC_INCREMENT));
                ulong arbCtr1 = ReadAndClearMsr(MTL_UNC_ARB_CTR + (boxIdx * MTL_UNC_INCREMENT) + 1);
                ulong hacArbCtr0 = ReadAndClearMsr(MTL_UNC_HAC_ARB_CTR + (boxIdx * MTL_UNC_INCREMENT));
                ulong hacArbCtr1 = ReadAndClearMsr(MTL_UNC_HAC_ARB_CTR + (boxIdx * MTL_UNC_INCREMENT) + 1);
                ulong hacCboCtr0 = ReadAndClearMsr(MTL_UNC_HAC_CBO_CTR + (boxIdx * MTL_UNC_INCREMENT));
                ulong hacCboCtr1 = ReadAndClearMsr(MTL_UNC_HAC_CBO_CTR + (boxIdx * MTL_UNC_INCREMENT) + 1);

                rc.arbCtr0[boxIdx] = arbCtr0 * normalizationFactor;
                rc.arbCtr1[boxIdx] = arbCtr1 * normalizationFactor;
                rc.hacArbCtr0[boxIdx] = hacArbCtr0 * normalizationFactor;
                rc.hacArbCtr1[boxIdx] = hacArbCtr1 * normalizationFactor;
                rc.hacCboCtr0[boxIdx] = hacCboCtr0 * normalizationFactor;
                rc.hacCboCtr1[boxIdx] = hacCboCtr1 * normalizationFactor;

                this.arbCounterTotals.totalArbCtr0 += arbCtr0;
                this.arbCounterTotals.totalArbCtr1 += arbCtr1;
                this.arbCounterTotals.totalHacCboCtr0 += hacCboCtr0;
                this.arbCounterTotals.totalHacCboCtr1 += hacCboCtr1;
                this.arbCounterTotals.totalHacArbCtr0 += hacArbCtr0;
                this.arbCounterTotals.totalHacArbCtr1 += hacArbCtr1;
            }

            return rc;
        }

        // Two arbs.
        public void SetBothBoxCounters(uint msrAddr, ulong value)
        {
            Ring0.WriteMsr(msrAddr, value);
            Ring0.WriteMsr(msrAddr + MTL_UNC_INCREMENT, value);
        }

        public Tuple<string, float>[] GetOverallCounterValues(NormalizedArbCounterData data, string arbCtr0, string arbCtr1, string hacArbCtr0, string hacArbCtr1, string hacCboCtr0, string hacCboCtr1)
        {
            Tuple<string, float>[] retval = new Tuple<string, float>[8];
            retval[0] = new Tuple<string, float>("sNCU Clk", data.sncuUncoreClk);
            retval[1] = new Tuple<string, float>("cNCU Clk", data.cncuUncoreClk);
            retval[2] = new Tuple<string, float>(arbCtr0, data.arbCtr0[0] + data.arbCtr0[1]);
            retval[3] = new Tuple<string, float>(arbCtr1, data.arbCtr1[0] + data.arbCtr1[1]);
            retval[4] = new Tuple<string, float>(hacArbCtr0, data.hacArbCtr0[0] + data.hacArbCtr0[1]);
            retval[5] = new Tuple<string, float>(hacArbCtr1, data.hacArbCtr1[0] + data.hacArbCtr1[1]);
            retval[6] = new Tuple<string, float>(hacCboCtr0, data.hacCboCtr0[0] + data.hacCboCtr0[1]);
            retval[7] = new Tuple<string, float>(hacCboCtr1, data.hacCboCtr1[0] + data.hacCboCtr1[1]);
            return retval;
        }

        public class FixedCounters : MonitoringConfig
        {
            private MeteorLakeArb arb;
            public FixedCounters(MeteorLakeArb arb)
            {
                this.arb = arb;
            }

            public string[] columns = new string[] { "Item", "GHz" };
            public string[] GetColumns() { return columns; }
            public string GetConfigName() { return "Fixed Counters"; }
            public string GetHelpText() { return ""; }

            public void Initialize()
            {
                arb.InitializeFixedCounters();

                // HAC CBo ToR allocation, all requests
                Ring0.WriteMsr(MTL_UNC_HAC_CBO_CTRL, GetUncorePerfEvtSelRegisterValue(0x35, 8, false, false, true, false, 0));
                Ring0.WriteMsr(MTL_UNC_HAC_CBO_CTR, 0);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.overallMetrics = new string[] { "N/A", "N/A" };
                NormalizedArbCounterData normalizedArbCounterData = arb.UpdateArbCounterData();
                results.unitMetrics = new string[2][];
                results.unitMetrics[0] = new string[] { "sNCU", FormatLargeNumber(normalizedArbCounterData.sncuUncoreClk) + "Hz" };
                results.unitMetrics[1] = new string[] { "cNCU", FormatLargeNumber(normalizedArbCounterData.cncuUncoreClk) + "Hz" };
                return results;
            }
        }

        public class ArbCounters : MonitoringConfig
        {
            private MeteorLakeArb arb;
            public ArbCounters(MeteorLakeArb arb)
            {
                this.arb = arb;
            }

            public string[] columns = new string[] { "Item", "Metric", "Total" };
            public string[] GetColumns() { return columns; }
            public string GetConfigName() { return "Arb"; }
            public string GetHelpText() { return ""; }

            public void Initialize()
            {
                arb.InitializeFixedCounters();

                // HAC CBo ToR allocation, all requests
                arb.SetBothBoxCounters(MTL_UNC_HAC_CBO_CTRL, GetUncorePerfEvtSelRegisterValue(0x35, 8, false, false, true, false, 0));
                arb.SetBothBoxCounters(MTL_UNC_HAC_CBO_CTR, 0);

                // HAC ARB, all requests
                arb.SetBothBoxCounters(MTL_UNC_HAC_ARB_CTRL, GetUncorePerfEvtSelRegisterValue(0x81, 1, false, false, true, false, 0));
                arb.SetBothBoxCounters(MTL_UNC_HAC_ARB_CTR, 0);

                // HAC ARB, CMI transactions
                arb.SetBothBoxCounters(MTL_UNC_HAC_ARB_CTRL + 1, GetUncorePerfEvtSelRegisterValue(0x8A, 1, false, false, true, false, 0));
                arb.SetBothBoxCounters(MTL_UNC_HAC_ARB_CTR, 0);
                arb.SetBothBoxCounters(MTL_UNC_HAC_ARB_CTR + 1, 0);

                // ARB Occupancy. 2 = data read, 0 = all (in the past, not documented)
                // 0x85 = occupancy. Uses cNCU clock
                // ok 0x81 doesn't work, how about 0x8A
                // 0x86 is almost right? seems to count in 32B increments and doesn't count GPU BW
                //Ring0.WriteMsr(MTL_UNC_ARB_CTRL, GetUncorePerfEvtSelRegisterValue(0x85, 0, false, false, true, false, 0));
                arb.SetBothBoxCounters(MTL_UNC_ARB_CTRL, GetUncorePerfEvtSelRegisterValue(0x85, 0, false, false, true, false, cmask: 0xFF));
                arb.SetBothBoxCounters(MTL_UNC_ARB_CTR, 0);
                //Ring0.WriteMsr(MTL_UNC_ARB_CTR + 1, 0);

                arb.arbCounterTotals = new ArbCounterTotals();
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                NormalizedArbCounterData normalizedArbCounterData = arb.UpdateArbCounterData();
                float arbReqs = normalizedArbCounterData.arbCtr0[0] + normalizedArbCounterData.arbCtr0[1];
                float hacCboTorAlloc = normalizedArbCounterData.hacCboCtr0[0] + normalizedArbCounterData.hacCboCtr0[1];
                float hacArbRequests = normalizedArbCounterData.hacArbCtr0[0] + normalizedArbCounterData.hacArbCtr0[1];
                float hacArbCmi = normalizedArbCounterData.hacArbCtr1[0] + normalizedArbCounterData.hacArbCtr1[1];
                // float arbOcc = normalizedArbCounterData.arbCtr0;
                results.unitMetrics = new string[][] {
                    new string[] { "HAC CBo", FormatLargeNumber(hacCboTorAlloc * 64) + "B/s", FormatLargeNumber(arb.arbCounterTotals.totalHacCboCtr0 * 64) + "B" },
                    new string[] { "HAC ARB (All Reqs)", FormatLargeNumber(hacArbRequests * 64) + "B/s", FormatLargeNumber(arb.arbCounterTotals.totalHacArbCtr0 * 64) + "B" },
                    new string[] { "HAC ARB (CMI Transactions)", FormatLargeNumber(hacArbCmi * 64) + "B/s", FormatLargeNumber(arb.arbCounterTotals.totalHacArbCtr1 * 64) + "B" },

                    // which clock?
                    // new string[] { "ARB", string.Format("{0:F2}", normalizedArbCounterData.arbCtr0 / normalizedArbCounterData.cncuUncoreClk), "-" },
                    new string[] { "ARB Cmask FF", FormatLargeNumber(normalizedArbCounterData.arbCtr0[0])},
                    new string[] { "sNCU", FormatLargeNumber(normalizedArbCounterData.sncuUncoreClk) + "Hz", "-" },
                    new string[] { "cNCU", FormatLargeNumber(normalizedArbCounterData.cncuUncoreClk) + "Hz", "-" },
                };

                results.overallMetrics = new string[] { "N/A", "N/A", "N/A" };
                results.overallCounterValues = arb.GetOverallCounterValues(normalizedArbCounterData, "ARB Occ", "Unused", "HAC ARB Reqs", "HAC ARB CMI Transactions", "HAC CBo Alloc", "Unused");
                return results;
            }
        }
    }
}

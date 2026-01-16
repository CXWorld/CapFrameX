using PmcReader.Interop;
using System;

namespace PmcReader.Intel
{
    public class HaswellClientArb : HaswellClientUncore
    {
        private ulong lastUncoreClockCount;

        public HaswellClientArb()
        {
            architectureName = "Haswell Client System Agent";
            lastUncoreClockCount = 0;
            monitoringConfigs = new MonitoringConfig[2];
            monitoringConfigs[0] = new MCRequests(this);
            monitoringConfigs[1] = new CoherencyRequests(this);
        }

        public class NormalizedArbCounterData
        {
            public float uncoreClock;
            public float ctr0;
            public float ctr1;
        }

        public NormalizedArbCounterData UpdateArbCounterData(out ulong ctr0, out ulong ctr1)
        {
            NormalizedArbCounterData rc = new NormalizedArbCounterData();
            float normalizationFactor = GetNormalizationFactor(0);
            ulong uncoreClock, elapsedUncoreClocks;
            ctr0 = ReadAndClearMsr(MSR_UNC_ARB_PERFCTR0);
            ctr1 = ReadAndClearMsr(MSR_UNC_ARB_PERFCTR1);
            Ring0.ReadMsr(MSR_UNC_PERF_FIXED_CTR, out uncoreClock);

            // MSR_UNC_PERF_FIXED_CTR is 48 bits wide, upper bits are reserved
            uncoreClock &= 0xFFFFFFFFFFFF;
            elapsedUncoreClocks = uncoreClock;
            if (uncoreClock > lastUncoreClockCount)
                elapsedUncoreClocks = uncoreClock - lastUncoreClockCount;
            lastUncoreClockCount = uncoreClock;

            rc.ctr0 = ctr0 * normalizationFactor;
            rc.ctr1 = ctr1 * normalizationFactor;
            rc.uncoreClock = elapsedUncoreClocks * normalizationFactor;
            return rc;
        }

        public Tuple<string, float>[] GetOverallCounterValues(NormalizedArbCounterData data, string ctr0, string ctr1)
        {
            Tuple<string, float>[] retval = new Tuple<string, float>[3];
            retval[0] = new Tuple<string, float>("Uncore Clk", data.uncoreClock);
            retval[1] = new Tuple<string, float>(ctr0, data.ctr0);
            retval[2] = new Tuple<string, float>(ctr1, data.ctr1);
            return retval;
        }

        public class MCRequests : MonitoringConfig
        {
            private HaswellClientArb cpu;
            private ulong totalReqs;
            public string GetConfigName() { return "All MC Requests"; }

            public MCRequests(HaswellClientArb intelCpu)
            {
                cpu = intelCpu;
                this.totalReqs = 0;
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                cpu.EnableUncoreCounters();
                // 0x80 = increments by number of outstanding requests every cycle
                // counts for coherent and non-coherent requests initiated by cores, igpu, or L3
                // only works in counter 0
                Ring0.WriteMsr(MSR_UNC_ARB_PERFEVTSEL0,
                    GetUncorePerfEvtSelRegisterValue(0x80, 1, false, false, true, false, 0));

                // 0x81 = number of requests
                Ring0.WriteMsr(MSR_UNC_ARB_PERFEVTSEL1,
                    GetUncorePerfEvtSelRegisterValue(0x81, 1, false, false, true, false, 0));

                Ring0.WriteMsr(MSR_UNC_ARB_PERFCTR0, 0);
                Ring0.WriteMsr(MSR_UNC_ARB_PERFCTR1, 0);
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = null;
                NormalizedArbCounterData counterData = cpu.UpdateArbCounterData(out ulong _, out ulong reqs);
                this.totalReqs += reqs;

                results.overallMetrics = new string[] { FormatLargeNumber(counterData.uncoreClock),
                    FormatLargeNumber(counterData.ctr1),
                    FormatLargeNumber(counterData.ctr1 * 64) + "B/s",
                    string.Format("{0:F2}", counterData.ctr0 / counterData.uncoreClock),
                    string.Format("{0:F2} clk", counterData.ctr0 / counterData.ctr1),
                    string.Format("{0:F2} ns", (1000000000 / counterData.uncoreClock) * (counterData.ctr0 / counterData.ctr1)),
                    FormatLargeNumber(totalReqs * 64) + "B"
                };
                results.overallCounterValues = cpu.GetOverallCounterValues(counterData, "Pending Requests Per Cycle", "Requests");
                return results;
            }

            public string GetHelpText() { return ""; }
            public string[] columns = new string[] { "Clk", "Requests", "Req*64B", "Q Occupancy", "Req Latency", "Req Latency", "Total Req Data" };
        }

        public class CoherencyRequests : MonitoringConfig
        {
            private HaswellClientArb cpu;
            public string GetConfigName() { return "Coherency Tracker Requests"; }

            public CoherencyRequests(HaswellClientArb intelCpu)
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
                // 0x83 = increments by number of outstanding requests every cycle in coherency tracker
                Ring0.WriteMsr(MSR_UNC_ARB_PERFEVTSEL0,
                    GetUncorePerfEvtSelRegisterValue(0x83, 1, false, false, true, false, 0));

                // 0x84 = number of requests allocated in coherency tracker
                Ring0.WriteMsr(MSR_UNC_ARB_PERFEVTSEL1,
                    GetUncorePerfEvtSelRegisterValue(0x84, 1, false, false, true, false, 0));
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = null;
                NormalizedArbCounterData counterData = cpu.UpdateArbCounterData(out _, out _);

                results.overallMetrics = new string[] { FormatLargeNumber(counterData.uncoreClock),
                    FormatLargeNumber(counterData.ctr1),
                    FormatLargeNumber(counterData.ctr1 * 64) + "B/s",
                    string.Format("{0:F2}", counterData.ctr0 / counterData.uncoreClock),
                    string.Format("{0:F2} clk", counterData.ctr0 / counterData.ctr1),
                    string.Format("{0:F2} ns", (1000000000 / counterData.uncoreClock) * (counterData.ctr0 / counterData.ctr1))
                };
                return results;
            }

            public string GetHelpText() { return ""; }
            public string[] columns = new string[] { "Clk", "Requests", "Req*64B", "Q Occupancy", "Req Latency", "Req Latency" };
        }
    }
}

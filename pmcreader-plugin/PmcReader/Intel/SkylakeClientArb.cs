using PmcReader.Interop;
using System;

namespace PmcReader.Intel
{
    public class SkylakeClientArb : SkylakeClientUncore
    {
        private ulong lastUncoreClockCount;

        public SkylakeClientArb()
        {
            architectureName = "Skylake Client System Agent";
            lastUncoreClockCount = 0;
            monitoringConfigs = new MonitoringConfig[3];
            monitoringConfigs[0] = new MCRequests(this, "All MC Requests", 0x1);
            monitoringConfigs[1] = new MCRequests(this, "MC: Core Data Read", 0x2);
            monitoringConfigs[2] = new MCRequests(this, "MC: Write", 0x20);
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

        public Tuple<string, float>[] GetOverallCounterValuesFromArbData(NormalizedArbCounterData data, string ctr0, string ctr1)
        {
            Tuple<string, float>[] retval = new Tuple<string, float>[3];
            retval[0] = new Tuple<string, float>("Uncore Clk", data.uncoreClock);
            retval[1] = new Tuple<string, float>(ctr0, data.ctr0);
            retval[2] = new Tuple<string, float>(ctr1, data.ctr1);
            return retval;
        }

        public class MCRequests : MonitoringConfig
        {
            private SkylakeClientArb cpu;
            private byte umask;
            private string configName;
            private ulong totalReqs;
            public string GetConfigName() { return configName; }

            public MCRequests(SkylakeClientArb intelCpu, string configName, byte umask)
            {
                cpu = intelCpu;
                this.configName = configName;
                this.umask = umask;
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
                    GetUncorePerfEvtSelRegisterValue(0x80, umask, false, false, true, false, 0));

                // 0x81 = number of requests
                Ring0.WriteMsr(MSR_UNC_ARB_PERFEVTSEL1,
                    GetUncorePerfEvtSelRegisterValue(0x81, umask, false, false, true, false, 0));
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = null;
                NormalizedArbCounterData counterData = cpu.UpdateArbCounterData(out _, out ulong reqs);
                this.totalReqs += reqs;

                results.overallCounterValues = cpu.GetOverallCounterValuesFromArbData(counterData, "Arb Queue Occupancy", "Reqs");
                results.overallMetrics = new string[] { FormatLargeNumber(counterData.uncoreClock),
                    FormatLargeNumber(counterData.ctr1),
                    FormatLargeNumber(counterData.ctr1 * 64) + "B/s",
                    string.Format("{0:F2}", counterData.ctr0 / counterData.uncoreClock),
                    string.Format("{0:F2} clk", counterData.ctr0 / counterData.ctr1),
                    string.Format("{0:F2} ns", (1000000000 / counterData.uncoreClock) * (counterData.ctr0 / counterData.ctr1)),
                    FormatLargeNumber(totalReqs * 64) + "B"
                };
                return results;
            }

            public string GetHelpText() { return ""; }
            public string[] columns = new string[] { "Clk", "Requests", "Req BW", "Q Len", "Req Latency", "Req Latency", "Total Req Data" };
        }

        public ulong GetImcCounterDelta(ulong addressOffset, ref ulong lastValue)
        {
            uint value = 0;
            ulong rc = 0;
            if (!Ring0.ReadMemory<uint>(barAddress, ref value))
            {
                return rc;
            }

            if (value < lastValue)
            {
                rc = value + (0xFFFFFFFF - lastValue);
            }
            else
            {
                rc = value - lastValue;
            }

            lastValue = value;
            return rc;
        }

        public class MemoryBandwidth : MonitoringConfig
        {
            private SkylakeClientArb cpu;
            public string GetConfigName() { return "Memory Bandwidth"; }

            private ulong lastDataReads;
            private ulong lastDataWrites;

            public MemoryBandwidth(SkylakeClientArb intelCpu)
            {
                cpu = intelCpu;
            }

            public string[] GetColumns()
            {
                return columns;
            }

            public void Initialize()
            {
                lastDataReads = 0;
                lastDataWrites = 0;
            }

            public MonitoringUpdateResults Update()
            {
                ulong reads = cpu.GetImcCounterDelta(0x5050, ref lastDataReads);
                ulong writes = cpu.GetImcCounterDelta(0x5054, ref lastDataWrites);
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = null;


                results.overallMetrics = new string[] { "Total", FormatLargeNumber(64*(reads + writes)) + "B/s"  };
                results.unitMetrics = new string[2][];
                results.unitMetrics[0] = new string[] { "Read", FormatLargeNumber(64 * reads) + "B/s" };
                results.unitMetrics[1] = new string[] { "Write", FormatLargeNumber(64 * writes) + "B/s" };
                return results;
            }

            public string GetHelpText() { return ""; }
            public string[] columns = new string[] { "Clk", "Requests", "Requests * 64B", "Q Occupancy", "Req Latency", "Req Latency" };
        }
    }
}

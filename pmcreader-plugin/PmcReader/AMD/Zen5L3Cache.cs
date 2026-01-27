using System;
using System.Collections.Generic;
using System.Linq;
using PmcReader.Interop;

namespace PmcReader.AMD
{
    public class Zen5L3Cache : Amd19hCpu
    {
        // ccx -> thread id mapping. Just need one thread per ccx - we'll always sample using that thread
        protected Dictionary<int, int> ccxSampleThreads;
        protected Dictionary<int, List<int>> allCcxThreads;
        public L3CounterData[] ccxCounterData;
        public L3CounterData ccxTotals;

        public Zen5L3Cache()
        {
            architectureName = "Zen 5 L3";

#if DEBUG
            Zen5Diagnostics.Enabled = true;
#endif

            // Initialize diagnostics
            Zen5Diagnostics.Initialize();
            Zen5Diagnostics.Log($"Zen5L3Cache constructor starting - coreCount={coreCount}, threadCount={GetThreadCount()}");

            ccxSampleThreads = new Dictionary<int, int>();
            allCcxThreads = new Dictionary<int, List<int>>();
            Dictionary<int, int> ccxIndexMap = new Dictionary<int, int>();

            // Log APIC ID information for all threads
            Zen5Diagnostics.LogApicIds(GetThreadCount(), (threadIdx) =>
            {
                if (TryGetExtendedApicIdEx(threadIdx, out uint apicId, out uint coreId, out uint threadsPerCore))
                {
                    int ccxId = Get1AhCcxId(threadIdx);
                    return (apicId, coreId, threadsPerCore, ccxId, true);
                }
                return (0, 0, 0, Get1AhCcxId(threadIdx), false);
            });

            // Build CCX to thread mapping
            Zen5Diagnostics.LogSection("CCX MAPPING CONSTRUCTION");
            for (int threadIdx = 0; threadIdx < GetThreadCount(); threadIdx++)
            {
                int rawCcxIdx = Get1AhCcxId(threadIdx);
                Zen5Diagnostics.Log($"  Thread {threadIdx}: raw CCX ID = {rawCcxIdx}");

                if (!ccxIndexMap.TryGetValue(rawCcxIdx, out int ccxIdx))
                {
                    ccxIdx = ccxIndexMap.Count;
                    ccxIndexMap.Add(rawCcxIdx, ccxIdx);
                    Zen5Diagnostics.Log($"    -> New CCX discovered: raw {rawCcxIdx} mapped to sequential index {ccxIdx}");
                }
                ccxSampleThreads[ccxIdx] = threadIdx;
                List<int> ccxThreads;
                if (! allCcxThreads.TryGetValue(ccxIdx, out ccxThreads))
                {
                    ccxThreads = new List<int>();
                    allCcxThreads.Add(ccxIdx, ccxThreads);
                }

                ccxThreads.Add(threadIdx);
            }

            // Log final topology
            Zen5Diagnostics.LogTopology(coreCount, GetThreadCount(), ccxSampleThreads.Count, ccxSampleThreads, allCcxThreads);

            // Validate CCX count
            if (coreCount > 8 && ccxSampleThreads.Count < 2)
            {
                Zen5Diagnostics.LogWarning($"Expected multiple CCXs for {coreCount} cores but only detected {ccxSampleThreads.Count} CCX(s)!");
            }

            monitoringConfigs = new MonitoringConfig[2];
            monitoringConfigs[0] = new HitRateLatencyConfig(this);
            monitoringConfigs[1] = new TopologyConfig(this);

            ccxCounterData = new L3CounterData[ccxSampleThreads.Count()];
            ccxTotals = new L3CounterData();

            // Reset thread affinity after constructor's CPUID operations
            // On multi-CCX systems, Get1AhCcxId calls OpCode.CpuidTx which changes
            // thread affinity. Reset to thread 0 to ensure clean state.
            ThreadAffinity.Set(1UL << 0);

            Zen5Diagnostics.Log($"Zen5L3Cache constructor complete. CCX count: {ccxSampleThreads.Count}");
        }

        /// <summary>
        /// Try to get extended APIC ID with additional info
        /// </summary>
        private static bool TryGetExtendedApicIdEx(int threadId, out uint extendedApicId, out uint coreId, out uint threadsPerCore)
        {
            extendedApicId = 0;
            coreId = 0;
            threadsPerCore = 1;

            if (!OpCode.CpuidTx(0x8000001E, 0, out extendedApicId, out uint ebx, out _, out _, 1UL << threadId))
                return false;

            coreId = ebx & 0xFF;
            threadsPerCore = ((ebx >> 8) & 0xFF) + 1;
            return true;
        }

        public class L3CounterData
        {
            public float ctr0;
            public float ctr1;
            public float ctr2;
            public float ctr3;
            public float ctr4;
            public float ctr5;
        }

        public void ClearTotals()
        {
            ccxTotals.ctr0 = 0;
            ccxTotals.ctr1 = 0;
            ccxTotals.ctr2 = 0;
            ccxTotals.ctr3 = 0;
            ccxTotals.ctr4 = 0;
            ccxTotals.ctr5 = 0;
        }

        public void UpdateCcxL3CounterData(int ccxIdx, int threadIdx)
        {
            ThreadAffinity.Set(1UL << threadIdx);
            Zen5Diagnostics.LogAffinitySet(threadIdx);

            float normalizationFactor = GetNormalizationFactor(threadIdx);

            // Read raw counter values
            ulong ctr0 = ReadAndClearMsr(MSR_L3_PERF_CTR_0);
            ulong ctr1 = ReadAndClearMsr(MSR_L3_PERF_CTR_1);
            ulong ctr2 = ReadAndClearMsr(MSR_L3_PERF_CTR_2);
            ulong ctr3 = ReadAndClearMsr(MSR_L3_PERF_CTR_3);
            ulong ctr4 = ReadAndClearMsr(MSR_L3_PERF_CTR_4);
            ulong ctr5 = ReadAndClearMsr(MSR_L3_PERF_CTR_5);

            // Log raw values
            Zen5Diagnostics.LogL3CounterRead(ccxIdx, threadIdx, ctr0, ctr1, ctr2, ctr3, ctr4, ctr5, normalizationFactor);

            if (ccxCounterData[ccxIdx] == null) ccxCounterData[ccxIdx] = new L3CounterData();
            ccxCounterData[ccxIdx].ctr0 = ctr0 * normalizationFactor;
            ccxCounterData[ccxIdx].ctr1 = ctr1 * normalizationFactor;
            ccxCounterData[ccxIdx].ctr2 = ctr2 * normalizationFactor;
            ccxCounterData[ccxIdx].ctr3 = ctr3 * normalizationFactor;
            ccxCounterData[ccxIdx].ctr4 = ctr4 * normalizationFactor;
            ccxCounterData[ccxIdx].ctr5 = ctr5 * normalizationFactor;

            // Log normalized values
            Zen5Diagnostics.LogL3CounterNormalized(ccxIdx,
                ccxCounterData[ccxIdx].ctr0, ccxCounterData[ccxIdx].ctr1,
                ccxCounterData[ccxIdx].ctr2, ccxCounterData[ccxIdx].ctr3,
                ccxCounterData[ccxIdx].ctr4, ccxCounterData[ccxIdx].ctr5);

            ccxTotals.ctr0 += ccxCounterData[ccxIdx].ctr0;
            ccxTotals.ctr1 += ccxCounterData[ccxIdx].ctr1;
            ccxTotals.ctr2 += ccxCounterData[ccxIdx].ctr2;
            ccxTotals.ctr3 += ccxCounterData[ccxIdx].ctr3;
            ccxTotals.ctr4 += ccxCounterData[ccxIdx].ctr4;
            ccxTotals.ctr5 += ccxCounterData[ccxIdx].ctr5;
        }

        public Tuple<string, float>[] GetOverallL3CounterValues(ulong aperf, ulong mperf, ulong irperfcount, ulong tsc,
            string ctr0, string ctr1, string ctr2, string ctr3, string ctr4, string ctr5)
        {
            Tuple<string, float>[] retval = new Tuple<string, float>[10];
            retval[0] = new Tuple<string, float>("APERF", aperf);
            retval[1] = new Tuple<string, float>("MPERF", mperf);
            retval[2] = new Tuple<string, float>("TSC", tsc);
            retval[3] = new Tuple<string, float>("IRPerfCount", irperfcount);
            retval[4] = new Tuple<string, float>(ctr0, ccxTotals.ctr0);
            retval[5] = new Tuple<string, float>(ctr1, ccxTotals.ctr1);
            retval[6] = new Tuple<string, float>(ctr2, ccxTotals.ctr2);
            retval[7] = new Tuple<string, float>(ctr3, ccxTotals.ctr3);
            retval[8] = new Tuple<string, float>(ctr4, ccxTotals.ctr4);
            retval[9] = new Tuple<string, float>(ctr5, ccxTotals.ctr5);
            return retval;
        }

        public class HitRateLatencyConfig : MonitoringConfig
        {
            private Zen5L3Cache l3Cache;
            private ulong[] _lastPerfCtlValues = new ulong[6];

            public HitRateLatencyConfig(Zen5L3Cache l3Cache)
            {
                this.l3Cache = l3Cache;
            }

            public string GetConfigName() { return "Hitrate and Latency"; }
            public string[] GetColumns() { return columns; }
            public void Initialize()
            {

                // Then program L3 counters on each CCX sample thread
                foreach (KeyValuePair<int, int> ccxThread in l3Cache.ccxSampleThreads)
                {
                    Zen5Diagnostics.Log($"Initializing L3 counters for CCX {ccxThread.Key} via thread {ccxThread.Value}");
                    ThreadAffinity.Set(1UL << ccxThread.Value);
                    InitializeThread(ccxThread.Key, ccxThread.Value);
                }

                Zen5Diagnostics.Log("L3 counter initialization complete");
            }

            private void InitializeThread(int ccxIdx = -1, int threadIdx = -1)
            {
                // L3 tag lookup state, all coherent accesses to L3
                ulong L3AccessPerfCtl = Get1AhL3PerfCtlValue(0x4, 0xFF, true, 0, true, true, 0, threadMask: 3);
                ulong L3MissPerfCtl = Get1AhL3PerfCtlValue(0x4, 1, true, 0, true, true, 0, threadMask: 3);

                // bit 2,3 of unit mask = near,far ccx's cache
                ulong L3MissLatencyOtherCacheReqs = Get1AhL3PerfCtlValue(0xAD, 0b1100, true, 0, true, enableAllSources: true, sourceId: 0x3, 0b11);
                ulong L3MissLatencyOtherCache = Get1AhL3PerfCtlValue(0xAC, 0b1100, true, 0, true, enableAllSources: true, sourceId: 0x3, 0b11);

                // bits 0,1 of unit mask = near,far dram
                ulong L3MissLatencyDramReqs = Get1AhL3PerfCtlValue(0xAD, 0b11, true, 0, true, enableAllSources: true, sourceId: 0x3, 0b11);
                ulong L3MissLatencyDram = Get1AhL3PerfCtlValue(0xAC, 0b11, true, 0, true, enableAllSources: true, sourceId: 0x3, 0b11);

                _lastPerfCtlValues[0] = L3AccessPerfCtl;
                _lastPerfCtlValues[1] = L3MissPerfCtl;
                _lastPerfCtlValues[2] = L3MissLatencyOtherCacheReqs;
                _lastPerfCtlValues[3] = L3MissLatencyOtherCache;
                _lastPerfCtlValues[4] = L3MissLatencyDramReqs;
                _lastPerfCtlValues[5] = L3MissLatencyDram;

                // Log the perf ctl values being programmed
                if (ccxIdx >= 0)
                {
                    Zen5Diagnostics.LogL3Init(ccxIdx, threadIdx, _lastPerfCtlValues);
                }

                Ring0.WriteMsr(MSR_L3_PERF_CTL_0, L3AccessPerfCtl);
                Ring0.WriteMsr(MSR_L3_PERF_CTL_1, L3MissPerfCtl);
                Ring0.WriteMsr(MSR_L3_PERF_CTL_2, L3MissLatencyOtherCacheReqs);
                Ring0.WriteMsr(MSR_L3_PERF_CTL_3, L3MissLatencyOtherCache);
                Ring0.WriteMsr(MSR_L3_PERF_CTL_4, L3MissLatencyDramReqs);
                Ring0.WriteMsr(MSR_L3_PERF_CTL_5, L3MissLatencyDram);

            }

            public MonitoringUpdateResults Update()
            {
                Zen5Diagnostics.LogUpdateStart("HitRateLatencyConfig");

                MonitoringUpdateResults results = new MonitoringUpdateResults();
                results.unitMetrics = new string[l3Cache.ccxSampleThreads.Count()][];
                float[] ccxClocks = new float[l3Cache.allCcxThreads.Count()];
                l3Cache.ClearTotals();
                ulong totalAperf = 0, totalMperf = 0, totalTsc = 0, totalIrPerfCount = 0;
                List<Tuple<string, float>> overallCounterValues = new List<Tuple<string, float>>();

                Zen5Diagnostics.Log($"Processing {l3Cache.ccxSampleThreads.Count} CCX(s), {l3Cache.allCcxThreads.Count} CCX thread groups");

                // Build thread info strings for each CCX
                Dictionary<int, string> ccxThreadInfo = new Dictionary<int, string>();
                foreach (KeyValuePair<int, List<int>> ccxThreads in l3Cache.allCcxThreads)
                {
                    List<int> threads = ccxThreads.Value;
                    if (threads.Count <= 4)
                    {
                        // Show all thread IDs if 4 or fewer
                        ccxThreadInfo[ccxThreads.Key] = string.Join(",", threads);
                    }
                    else
                    {
                        // Show range for many threads
                        int min = threads[0], max = threads[0];
                        foreach (int t in threads)
                        {
                            if (t < min) min = t;
                            if (t > max) max = t;
                        }
                        ccxThreadInfo[ccxThreads.Key] = string.Format("{0}-{1} ({2})", min, max, threads.Count);
                    }
                }

                foreach (KeyValuePair<int, int> ccxThread in l3Cache.ccxSampleThreads)
                {
                    Zen5Diagnostics.Log($"Processing CCX {ccxThread.Key} (sample thread: {ccxThread.Value})");

                    // Try to determine frequency, by getting max frequency of cores in ccx
                    foreach (int ccxThreadIdx in l3Cache.allCcxThreads[ccxThread.Key])
                    {
                        ThreadAffinity.Set(1UL << ccxThreadIdx);

                        // NOTE: This uses GetThreadCount() + ccxThreadIdx as the normalization index
                        // This creates separate timers for L3 vs core counters
                        int normIndexUsedHere = l3Cache.GetThreadCount() + ccxThreadIdx;
                        float normalizationFactor = l3Cache.GetNormalizationFactor(normIndexUsedHere);

                        // Log the normalization index being used (don't call GetNormalizationFactor again - it has side effects!)
                        Zen5Diagnostics.Log($"    Thread {ccxThreadIdx}: using norm index {normIndexUsedHere}, factor={normalizationFactor:F6}");

                        ulong aperf, mperf, tsc, irperfcount;
                        l3Cache.ReadFixedCounters(ccxThreadIdx, out aperf, out irperfcount, out tsc, out mperf);

                        Zen5Diagnostics.LogFixedCounters(ccxThreadIdx, aperf, mperf, tsc, irperfcount, normalizationFactor);

                        totalAperf += aperf;
                        totalIrPerfCount += irperfcount;
                        totalTsc += tsc;
                        totalMperf += mperf;
                        float clk = tsc * ((float)aperf / mperf) * normalizationFactor;
                        if (clk > ccxClocks[ccxThread.Key]) ccxClocks[ccxThread.Key] = clk;
                        if (ccxThreadIdx == ccxThread.Value)
                        {
                            Zen5Diagnostics.Log($"  Reading L3 counters for CCX {ccxThread.Key} from thread {ccxThread.Value}");
                            l3Cache.UpdateCcxL3CounterData(ccxThread.Key, ccxThread.Value);
                            InitializeThread(ccxThread.Key, ccxThread.Value); // somehow these get cleared every once in a while?

                            string threadInfo = ccxThreadInfo.ContainsKey(ccxThread.Key) ? ccxThreadInfo[ccxThread.Key] : "";
                            results.unitMetrics[ccxThread.Key] = ComputeMetrics("CCX " + ccxThread.Key, threadInfo, l3Cache.ccxCounterData[ccxThread.Key], ccxClocks[ccxThread.Key]);
                            overallCounterValues.Add(new Tuple<string, float>("CCX" + ccxThread.Key + " L3 Access", l3Cache.ccxCounterData[ccxThread.Key].ctr0));
                            overallCounterValues.Add(new Tuple<string, float>("CCX" + ccxThread.Key + " L3 Miss", l3Cache.ccxCounterData[ccxThread.Key].ctr1));
                            overallCounterValues.Add(new Tuple<string, float>("CCX" + ccxThread.Key + " Other CCX Sampled Reqs", l3Cache.ccxCounterData[ccxThread.Key].ctr2));
                            overallCounterValues.Add(new Tuple<string, float>("CCX" + ccxThread.Key + " Other CCX Sampled Latency", l3Cache.ccxCounterData[ccxThread.Key].ctr3));
                            overallCounterValues.Add(new Tuple<string, float>("CCX" + ccxThread.Key + " DRAM Sampled Reqs", l3Cache.ccxCounterData[ccxThread.Key].ctr4));
                            overallCounterValues.Add(new Tuple<string, float>("CCX" + ccxThread.Key + " DRAM Sampled Latency", l3Cache.ccxCounterData[ccxThread.Key].ctr5));
                        }
                    }
                }

                // Log accumulated totals
                Zen5Diagnostics.LogTotals(l3Cache.ccxTotals.ctr0, l3Cache.ccxTotals.ctr1, l3Cache.ccxTotals.ctr2,
                    l3Cache.ccxTotals.ctr3, l3Cache.ccxTotals.ctr4, l3Cache.ccxTotals.ctr5);

                overallCounterValues.Add(new Tuple<string, float>("APERF", totalAperf));
                overallCounterValues.Add(new Tuple<string, float>("MPERF", totalMperf));
                overallCounterValues.Add(new Tuple<string, float>("REF_TSC", totalTsc));
                overallCounterValues.Add(new Tuple<string, float>("IrPerfCount", totalIrPerfCount));

                float avgClk = 0;
                foreach (float ccxClock in ccxClocks) avgClk += ccxClock;
                avgClk /= l3Cache.allCcxThreads.Count();
                string totalThreadInfo = string.Format("{0}c/{1}t", l3Cache.coreCount, l3Cache.GetThreadCount());
                results.overallMetrics = ComputeMetrics("Overall", totalThreadInfo, l3Cache.ccxTotals, avgClk);
                results.overallCounterValues = overallCounterValues.ToArray();

                Zen5Diagnostics.LogUpdateEnd();
                return results;
            }

            public string[] columns = new string[] { "Item", "Threads", "Clk", "Hitrate", "Hit BW", "Miss BW", "Lat CCX", "Lat DRAM" };

            public string GetHelpText() { return ""; }

            private string[] ComputeMetrics(string label, string threadsInfo, L3CounterData counterData, float clk)
            {
                // Check for division by zero issues
                Zen5Diagnostics.LogDivisionCheck($"{label} CCX Latency", counterData.ctr3, counterData.ctr2);
                Zen5Diagnostics.LogDivisionCheck($"{label} DRAM Latency", counterData.ctr5, counterData.ctr4);
                Zen5Diagnostics.LogDivisionCheck($"{label} Hitrate", counterData.ctr1, counterData.ctr0);

                // average sampled latency is XiSampledLatency / XiSampledLatencyRequests * 10 ns
                float ccxL3MissLatencyNs = counterData.ctr2 > 0 ? (float)10 * counterData.ctr3 / counterData.ctr2 : 0;
                float dramL3MissLatencyNs = counterData.ctr4 > 0 ? (float)10 * counterData.ctr5 / counterData.ctr4 : 0;
                float ccxL3Hitrate = counterData.ctr0 > 0 ? (1 - (float)counterData.ctr1 / counterData.ctr0) * 100 : 0;
                float ccxL3HitBw = ((float)counterData.ctr0 - counterData.ctr1) * 64;

                // Log computed metrics
                Zen5Diagnostics.LogComputedMetrics(label, ccxL3Hitrate, ccxL3HitBw, counterData.ctr1 * 64,
                    ccxL3MissLatencyNs, dramL3MissLatencyNs, clk);

                return new string[] { label,
                        threadsInfo,
                        FormatLargeNumber(clk),
                        string.Format("{0:F2}%", ccxL3Hitrate),
                        FormatLargeNumber(ccxL3HitBw) + "B/s",
                        FormatLargeNumber(counterData.ctr1 * 64) + "B/s",
                        string.Format("{0:F1} ns", ccxL3MissLatencyNs),
                        string.Format("{0:F1} ns", dramL3MissLatencyNs)};
            }
        }

        /// <summary>
        /// Monitoring config that displays APIC ID and CCX topology information
        /// </summary>
        public class TopologyConfig : MonitoringConfig
        {
            private Zen5L3Cache l3Cache;

            public TopologyConfig(Zen5L3Cache l3Cache)
            {
                this.l3Cache = l3Cache;
            }

            public string GetConfigName() { return "APIC ID / CCX Topology"; }
            public string[] GetColumns() { return columns; }
            public string[] columns = new string[] { "Thread", "APIC ID", "Core ID", "Threads/Core", "CCX", "Status" };

            public void Initialize() { }

            public string GetHelpText()
            {
                return "Displays Extended APIC ID from CPUID 0x8000001E and CCX mapping for each thread. " +
                       "Use this to debug CCX detection on multi-CCX Zen 5 systems.";
            }

            public MonitoringUpdateResults Update()
            {
                MonitoringUpdateResults results = new MonitoringUpdateResults();
                int threadCount = l3Cache.GetThreadCount();
                results.unitMetrics = new string[threadCount][];

                // Count threads per CCX for summary
                var ccxThreadCounts = new Dictionary<int, int>();

                for (int threadIdx = 0; threadIdx < threadCount; threadIdx++)
                {
                    string apicIdStr, coreIdStr, threadsPerCoreStr, ccxStr, statusStr;

                    if (TryGetExtendedApicIdEx(threadIdx, out uint extendedApicId, out uint coreId, out uint threadsPerCore))
                    {
                        int ccxId = l3Cache.Get1AhCcxId(threadIdx);

                        apicIdStr = string.Format("0x{0:X4}", extendedApicId);
                        coreIdStr = coreId.ToString();
                        threadsPerCoreStr = threadsPerCore.ToString();
                        ccxStr = ccxId.ToString();
                        statusStr = "OK";

                        if (!ccxThreadCounts.ContainsKey(ccxId))
                            ccxThreadCounts[ccxId] = 0;
                        ccxThreadCounts[ccxId]++;
                    }
                    else
                    {
                        int ccxId = l3Cache.Get1AhCcxId(threadIdx);
                        apicIdStr = "N/A";
                        coreIdStr = "N/A";
                        threadsPerCoreStr = "N/A";
                        ccxStr = ccxId.ToString();
                        statusStr = "CPUID Failed";

                        if (!ccxThreadCounts.ContainsKey(ccxId))
                            ccxThreadCounts[ccxId] = 0;
                        ccxThreadCounts[ccxId]++;
                    }

                    results.unitMetrics[threadIdx] = new string[] {
                        string.Format("Thread {0}", threadIdx),
                        apicIdStr,
                        coreIdStr,
                        threadsPerCoreStr,
                        ccxStr,
                        statusStr
                    };
                }

                // Build summary for overall metrics
                string ccxSummary = "";
                foreach (var kvp in ccxThreadCounts)
                {
                    if (ccxSummary.Length > 0) ccxSummary += ", ";
                    ccxSummary += string.Format("CCX{0}:{1}t", kvp.Key, kvp.Value);
                }

                results.overallMetrics = new string[] {
                    "Summary",
                    string.Format("{0} cores", l3Cache.coreCount),
                    string.Format("{0} threads", threadCount),
                    string.Format("{0} CCXs", ccxThreadCounts.Count),
                    ccxSummary,
                    string.Format("{0} cores/CCX", l3Cache.GetCoresPerCcx())
                };

                // Log raw data for export
                List<Tuple<string, float>> counterValues = new List<Tuple<string, float>>();
                counterValues.Add(new Tuple<string, float>("Core Count", l3Cache.coreCount));
                counterValues.Add(new Tuple<string, float>("Thread Count", threadCount));
                counterValues.Add(new Tuple<string, float>("CCX Count", ccxThreadCounts.Count));
                counterValues.Add(new Tuple<string, float>("Cores Per CCX", l3Cache.GetCoresPerCcx()));

                for (int threadIdx = 0; threadIdx < threadCount; threadIdx++)
                {
                    if (TryGetExtendedApicIdEx(threadIdx, out uint extendedApicId, out uint coreId, out uint threadsPerCore))
                    {
                        counterValues.Add(new Tuple<string, float>(
                            string.Format("Thread{0}_APIC", threadIdx), extendedApicId));
                        counterValues.Add(new Tuple<string, float>(
                            string.Format("Thread{0}_CoreId", threadIdx), coreId));
                        counterValues.Add(new Tuple<string, float>(
                            string.Format("Thread{0}_CCX", threadIdx), l3Cache.Get1AhCcxId(threadIdx)));
                    }
                }

                results.overallCounterValues = counterValues.ToArray();
                return results;
            }

            private static bool TryGetExtendedApicIdEx(int threadId, out uint extendedApicId, out uint coreId, out uint threadsPerCore)
            {
                extendedApicId = 0;
                coreId = 0;
                threadsPerCore = 1;

                if (!OpCode.CpuidTx(0x8000001E, 0, out extendedApicId, out uint ebx, out _, out _, 1UL << threadId))
                    return false;

                coreId = ebx & 0xFF;
                threadsPerCore = ((ebx >> 8) & 0xFF) + 1;
                return true;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;

namespace PmcReader.AMD
{
    /// <summary>
    /// Diagnostic logger for Zen 5 multi-CCX debugging
    /// Writes detailed logs to help diagnose hitrate, bandwidth, and latency metric issues
    /// </summary>
    public static class Zen5Diagnostics
    {
        private static readonly object _lock = new object();
        private static string _logPath;
        private static bool _enabled = false;
        private static int _updateCounter = 0;
        private static DateTime _startTime;

        public static bool Enabled
        {
            get { return _enabled; }
            set { _enabled = value; }
        }

        /// <summary>
        /// Initialize the diagnostic logger
        /// </summary>
        public static void Initialize()
        {
            if (!_enabled) return;

            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string logDir = Path.Combine(appData, "PmcReader", "Diagnostics");
                Directory.CreateDirectory(logDir);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                _logPath = Path.Combine(logDir, $"Zen5_Diag_{timestamp}.log");
                _startTime = DateTime.Now;
                _updateCounter = 0;

                Log("=".PadRight(80, '='));
                Log("Zen 5 Diagnostic Log Started");
                Log($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Log($"Log file: {_logPath}");
                Log("=".PadRight(80, '='));
            }
            catch (Exception ex)
            {
                // Silently fail - don't break the app if logging fails
                _enabled = false;
                System.Diagnostics.Debug.WriteLine($"Zen5Diagnostics init failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Get the log file path
        /// </summary>
        public static string GetLogPath()
        {
            return _logPath;
        }

        /// <summary>
        /// Log a message
        /// </summary>
        public static void Log(string message)
        {
            // Safety check - don't crash if Initialize() was never called
            if (!_enabled || string.IsNullOrEmpty(_logPath))
            {
                // Try to auto-initialize if not done yet
                if (_logPath == null && _enabled)
                {
                    Initialize();
                }

                if (!_enabled || string.IsNullOrEmpty(_logPath)) return;
            }

            try
            {
                lock (_lock)
                {
                    string elapsed = (DateTime.Now - _startTime).ToString(@"hh\:mm\:ss\.fff");
                    File.AppendAllText(_logPath, $"[{elapsed}] {message}\n");
                }
            }
            catch { }
        }

        /// <summary>
        /// Log a section header
        /// </summary>
        public static void LogSection(string title)
        {
            Log("");
            Log($"--- {title} ---");
        }

        /// <summary>
        /// Log system/CPU topology information
        /// </summary>
        public static void LogTopology(int coreCount, int threadCount, int ccxCount, Dictionary<int, int> ccxSampleThreads, Dictionary<int, List<int>> allCcxThreads)
        {
            LogSection("CPU TOPOLOGY");
            Log($"Core Count: {coreCount}");
            Log($"Thread Count: {threadCount}");
            Log($"Detected CCX Count: {ccxCount}");
            Log($"SMT Enabled: {(threadCount == coreCount * 2 ? "Yes" : "No")}");

            Log("");
            Log("CCX Sample Threads (one thread sampled per CCX for L3 counters):");
            foreach (var kvp in ccxSampleThreads)
            {
                Log($"  CCX {kvp.Key} -> Sample Thread {kvp.Value}");
            }

            Log("");
            Log("All CCX Thread Assignments:");
            foreach (var kvp in allCcxThreads)
            {
                string threads = string.Join(", ", kvp.Value);
                Log($"  CCX {kvp.Key} -> Threads [{threads}] (count: {kvp.Value.Count})");
            }
        }

        /// <summary>
        /// Log APIC ID information for all threads
        /// </summary>
        public static void LogApicIds(int threadCount, Func<int, (uint apicId, uint coreId, uint threadsPerCore, int ccxId, bool success)> getApicInfo)
        {
            LogSection("APIC ID MAPPING");
            Log("Thread -> APIC ID -> CCX ID mapping:");
            Log(string.Format("  {0,-8} {1,-12} {2,-10} {3,-12} {4,-8}", "Thread", "APIC ID", "Core ID", "Threads/Core", "CCX"));
            Log("  " + "-".PadRight(55, '-'));

            for (int i = 0; i < threadCount; i++)
            {
                var info = getApicInfo(i);
                if (info.success)
                {
                    Log(string.Format("  {0,-8} 0x{1,-10:X4} {2,-10} {3,-12} {4,-8}",
                        i, info.apicId, info.coreId, info.threadsPerCore, info.ccxId));
                }
                else
                {
                    Log(string.Format("  {0,-8} CPUID FAILED", i));
                }
            }
        }

        /// <summary>
        /// Log CCX ID calculation details (only logs first 32 threads to avoid spam)
        /// </summary>
        public static void LogCcxCalculation(int threadId, int coreCount, int threadCount, int coresPerCcx,
            uint apicId, int threadBits, int coreBits, int ccxShift, int resultCcxId)
        {
            // Only log first occurrence per thread to avoid massive log files
            if (threadId < 32 && _updateCounter <= 1)
            {
                Log($"  CCX calc for thread {threadId}: APIC=0x{apicId:X4}, coresPerCcx={coresPerCcx}, " +
                    $"threadBits={threadBits}, coreBits={coreBits}, shift={ccxShift}, result CCX={resultCcxId}");
            }
        }

        /// <summary>
        /// Log L3 counter initialization
        /// </summary>
        public static void LogL3Init(int ccxIdx, int threadIdx, ulong[] perfCtlValues)
        {
            LogSection($"L3 COUNTER INIT - CCX {ccxIdx} via Thread {threadIdx}");
            Log("Programming L3 performance counters:");
            string[] names = { "L3Access", "L3Miss", "OtherCCX_Reqs", "OtherCCX_Lat", "DRAM_Reqs", "DRAM_Lat" };
            for (int i = 0; i < perfCtlValues.Length && i < names.Length; i++)
            {
                Log($"  CTL{i} ({names[i]}): 0x{perfCtlValues[i]:X16}");
            }
        }

        /// <summary>
        /// Log L3 counter read (raw values before normalization)
        /// </summary>
        public static void LogL3CounterRead(int ccxIdx, int threadIdx, ulong ctr0, ulong ctr1, ulong ctr2,
            ulong ctr3, ulong ctr4, ulong ctr5, float normFactor)
        {
            Log($"  L3 Read CCX{ccxIdx} (thread {threadIdx}): " +
                $"ctr0={ctr0}, ctr1={ctr1}, ctr2={ctr2}, ctr3={ctr3}, ctr4={ctr4}, ctr5={ctr5}, normFactor={normFactor:F6}");
        }

        /// <summary>
        /// Log L3 counter values after normalization
        /// </summary>
        public static void LogL3CounterNormalized(int ccxIdx, float ctr0, float ctr1, float ctr2,
            float ctr3, float ctr4, float ctr5)
        {
            Log($"  L3 Normalized CCX{ccxIdx}: " +
                $"Access={ctr0:F2}, Miss={ctr1:F2}, OtherCCX_Reqs={ctr2:F2}, OtherCCX_Lat={ctr3:F2}, " +
                $"DRAM_Reqs={ctr4:F2}, DRAM_Lat={ctr5:F2}");
        }

        /// <summary>
        /// Log fixed counter read for a thread
        /// </summary>
        public static void LogFixedCounters(int threadIdx, ulong aperf, ulong mperf, ulong tsc, ulong irperf, float normFactor)
        {
            Log($"    Fixed ctrs thread {threadIdx}: APERF={aperf}, MPERF={mperf}, TSC={tsc}, IRPerf={irperf}, normFactor={normFactor:F6}");
        }

        /// <summary>
        /// Log computed metrics for a CCX
        /// </summary>
        public static void LogComputedMetrics(string label, float hitrate, float hitBw, float missBw,
            float ccxLatencyNs, float dramLatencyNs, float clk)
        {
            Log($"  Metrics [{label}]: Hitrate={hitrate:F2}%, HitBW={hitBw:F2}, MissBW={missBw:F2}, " +
                $"CCX_Lat={ccxLatencyNs:F1}ns, DRAM_Lat={dramLatencyNs:F1}ns, Clk={clk:F2}");
        }

        /// <summary>
        /// Log totals accumulation
        /// </summary>
        public static void LogTotals(float ctr0, float ctr1, float ctr2, float ctr3, float ctr4, float ctr5)
        {
            Log($"  Accumulated totals: ctr0={ctr0:F2}, ctr1={ctr1:F2}, ctr2={ctr2:F2}, " +
                $"ctr3={ctr3:F2}, ctr4={ctr4:F2}, ctr5={ctr5:F2}");
        }

        /// <summary>
        /// Start logging an update cycle
        /// </summary>
        public static void LogUpdateStart(string configName)
        {
            _updateCounter++;
            LogSection($"UPDATE CYCLE #{_updateCounter} - {configName}");
            Log($"Time: {DateTime.Now:HH:mm:ss.fff}");
        }

        /// <summary>
        /// Log update cycle complete
        /// </summary>
        public static void LogUpdateEnd()
        {
            Log($"Update cycle #{_updateCounter} complete");
        }

        /// <summary>
        /// Log Data Fabric / UMC counter values
        /// </summary>
        public static void LogDFCounters(string configName, ulong[] counters, float normFactor)
        {
            LogSection($"DATA FABRIC COUNTERS - {configName}");
            Log($"Normalization factor: {normFactor:F6}");
            for (int i = 0; i < counters.Length; i++)
            {
                Log($"  Counter {i}: raw={counters[i]}, normalized={counters[i] * normFactor:F2}, " +
                    $"as BW={counters[i] * normFactor * 64:F2} B/s");
            }
        }

        /// <summary>
        /// Log thread affinity change
        /// </summary>
        public static void LogAffinitySet(int threadIdx)
        {
            // Only log occasionally to avoid spam - every 10th update
            if (_updateCounter % 10 == 1)
            {
                Log($"    -> Affinity set to thread {threadIdx}");
            }
        }

        /// <summary>
        /// Log an error or warning
        /// </summary>
        public static void LogError(string message)
        {
            Log($"ERROR: {message}");
        }

        /// <summary>
        /// Log a warning about potential issues
        /// </summary>
        public static void LogWarning(string message)
        {
            Log($"WARNING: {message}");
        }

        /// <summary>
        /// Log division by zero risk
        /// </summary>
        public static void LogDivisionCheck(string label, float numerator, float denominator)
        {
            if (denominator == 0 || float.IsNaN(denominator) || float.IsInfinity(denominator))
            {
                LogWarning($"Division issue in {label}: {numerator} / {denominator} (denominator is zero/NaN/Inf)");
            }
        }

        /// <summary>
        /// Log normalization factor comparison
        /// </summary>
        public static void LogNormFactorComparison(int threadIdx, float factor1, int index1, float factor2, int index2)
        {
            if (Math.Abs(factor1 - factor2) > 0.01f)
            {
                LogWarning($"Normalization factor mismatch for thread {threadIdx}: " +
                    $"index {index1}={factor1:F6} vs index {index2}={factor2:F6}");
            }
        }

        /// <summary>
        /// Flush and finalize logging
        /// </summary>
        public static void Finalize()
        {
            Log("");
            Log("=".PadRight(80, '='));
            Log($"Diagnostic log ended at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Log($"Total update cycles logged: {_updateCounter}");
            Log("=".PadRight(80, '='));
        }
    }
}

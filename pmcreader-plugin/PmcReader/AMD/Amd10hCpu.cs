using PmcReader.Interop;
using System;
using System.Windows.Forms;
using System.Drawing;

namespace PmcReader.AMD
{
    public class Amd10hCpu : GenericMonitoringArea
    {
        public const uint MSR_TSC = 0x00000010;
        public const uint MSR_PERF_CTR_0 = 0xC0010004;
        public const uint MSR_PERF_CTR_1 = 0xC0010005;
        public const uint MSR_PERF_CTR_2 = 0xC0010006;
        public const uint MSR_PERF_CTR_3 = 0xC0010007;
        public const uint MSR_PERF_CTL_0 = 0xC0010000;
        public const uint MSR_PERF_CTL_1 = 0xC0010001;
        public const uint MSR_PERF_CTL_2 = 0xC0010002;
        public const uint MSR_PERF_CTL_3 = 0xC0010003;

        public const uint HWCR = 0xC0010015;

        public NormalizedCoreCounterData[] NormalizedThreadCounts;
        public NormalizedCoreCounterData NormalizedTotalCounts;
        private ulong[] lastThreadTsc;

        public Amd10hCpu()
        {
            architectureName = "AMD 10h Family";
            lastThreadTsc = new ulong[GetThreadCount()];
        }

        /// <summary>
        /// Program core perf counters
        /// </summary>
        /// <param name="ctr0">Counter 0 event select</param>
        /// <param name="ctr1">Counter 1 event select</param>
        /// <param name="ctr2">Counter 2 event select</param>
        /// <param name="ctr3">Counter 3 event select</param>
        public void ProgramPerfCounters(ulong ctr0, ulong ctr1, ulong ctr2, ulong ctr3)
        {
            for (int threadIdx = 0; threadIdx < this.GetThreadCount(); threadIdx++)
            {
                ThreadAffinity.Set(1UL << threadIdx);
                Ring0.WriteMsr(MSR_PERF_CTL_0, ctr0);
                Ring0.WriteMsr(MSR_PERF_CTL_1, ctr1);
                Ring0.WriteMsr(MSR_PERF_CTL_2, ctr2);
                Ring0.WriteMsr(MSR_PERF_CTL_3, ctr3);
            }
        }

        /// <summary>
        /// Update fixed counters for thread, affinity must be set going in
        /// </summary>
        /// <param name="threadIdx">thread to update fixed counters for</param>
        public void ReadFixedCounters(int threadIdx, out ulong elapsedTsc)
        {
            ulong tsc;
            Ring0.ReadMsr(MSR_TSC, out tsc);

            elapsedTsc = tsc;
            if (tsc > lastThreadTsc[threadIdx])
                elapsedTsc = tsc - lastThreadTsc[threadIdx];
            else if (lastThreadTsc[threadIdx] > 0)
                elapsedTsc = tsc + (0xFFFFFFFFFFFFFFFF - lastThreadTsc[threadIdx]);

            lastThreadTsc[threadIdx] = tsc;
        }

        /// <summary>
        /// initialize/reset accumulated totals for core counter data
        /// </summary>
        public void InitializeCoreTotals()
        {
            if (NormalizedTotalCounts == null)
            {
                NormalizedTotalCounts = new NormalizedCoreCounterData();
            }

            NormalizedTotalCounts.tsc = 0;
            NormalizedTotalCounts.ctr0 = 0;
            NormalizedTotalCounts.ctr1 = 0;
            NormalizedTotalCounts.ctr2 = 0;
            NormalizedTotalCounts.ctr3 = 0;
        }

        /// <summary>
        /// Read and update counter data for thread
        /// </summary>
        /// <param name="threadIdx">Thread to set affinity to</param>
        public void UpdateThreadCoreCounterData(int threadIdx)
        {
            ThreadAffinity.Set(1UL << threadIdx);
            float normalizationFactor = GetNormalizationFactor(threadIdx);
            ulong tsc;
            ulong ctr0, ctr1, ctr2, ctr3;
            ReadFixedCounters(threadIdx, out tsc);
            ctr0 = ReadAndClearMsr(MSR_PERF_CTR_0);
            ctr1 = ReadAndClearMsr(MSR_PERF_CTR_1);
            ctr2 = ReadAndClearMsr(MSR_PERF_CTR_2);
            ctr3 = ReadAndClearMsr(MSR_PERF_CTR_3);

            if (NormalizedThreadCounts == null) NormalizedThreadCounts = new NormalizedCoreCounterData[threadCount];
            if (NormalizedThreadCounts[threadIdx] == null) NormalizedThreadCounts[threadIdx] = new NormalizedCoreCounterData();

            if (NormalizedThreadCounts[threadIdx].NormalizationFactor != 0.0f)
            {
                NormalizedThreadCounts[threadIdx].totalctr0 += ctr0;
                NormalizedThreadCounts[threadIdx].totalctr1 += ctr1;
                NormalizedThreadCounts[threadIdx].totalctr2 += ctr2;
                NormalizedThreadCounts[threadIdx].totalctr3 += ctr3;
                NormalizedTotalCounts.totalctr0 += ctr0;
                NormalizedTotalCounts.totalctr1 += ctr1;
                NormalizedTotalCounts.totalctr2 += ctr2;
                NormalizedTotalCounts.totalctr3 += ctr3;
            }

            NormalizedThreadCounts[threadIdx].tsc = tsc * normalizationFactor;
            NormalizedThreadCounts[threadIdx].ctr0 = ctr0 * normalizationFactor;
            NormalizedThreadCounts[threadIdx].ctr1 = ctr1 * normalizationFactor;
            NormalizedThreadCounts[threadIdx].ctr2 = ctr2 * normalizationFactor;
            NormalizedThreadCounts[threadIdx].ctr3 = ctr3 * normalizationFactor;
            NormalizedThreadCounts[threadIdx].NormalizationFactor = normalizationFactor;
            NormalizedTotalCounts.tsc += NormalizedThreadCounts[threadIdx].tsc;
            NormalizedTotalCounts.ctr0 += NormalizedThreadCounts[threadIdx].ctr0;
            NormalizedTotalCounts.ctr1 += NormalizedThreadCounts[threadIdx].ctr1;
            NormalizedTotalCounts.ctr2 += NormalizedThreadCounts[threadIdx].ctr2;
            NormalizedTotalCounts.ctr3 += NormalizedThreadCounts[threadIdx].ctr3;
        }

        /// <summary>
        /// Assemble overall counter values into a Tuple of string, float array.
        /// </summary>
        /// <param name="ctr0">Description for counter 0 value</param>
        /// <param name="ctr1">Description for counter 1 value</param>
        /// <param name="ctr2">Description for counter 2 value</param>
        /// <param name="ctr3">Description for counter 3 value</param>
        /// <returns>Array to put in results object</returns>
        public Tuple<string, float>[] GetOverallCounterValues(string ctr0, string ctr1, string ctr2, string ctr3)
        {
            NormalizedCoreCounterData dataToLog = this.NormalizedTotalCounts;
            if (this.targetLogCoreIndex >= 0)
            {
                dataToLog = NormalizedThreadCounts[this.targetLogCoreIndex];
            }

            Tuple<string, float>[] retval = new Tuple<string, float>[5];
            retval[0] = new Tuple<string, float>("TSC", dataToLog.tsc);
            retval[1] = new Tuple<string, float>(ctr0, dataToLog.ctr0);
            retval[2] = new Tuple<string, float>(ctr1, dataToLog.ctr1);
            retval[3] = new Tuple<string, float>(ctr2, dataToLog.ctr2);
            retval[4] = new Tuple<string, float>(ctr3, dataToLog.ctr3);
            return retval;
        }

        /// <summary>
        /// Get perf ctl value assuming default values for stupid stuff
        /// </summary>
        /// <param name="perfEvent">Perf event, low 16 bits</param>
        /// <param name="umask">Unit mask</param>
        /// <param name="edge">only increment on transition</param>
        /// <param name="cmask">count mask</param>
        /// <param name="perfEventHi">Perf event, high 8 bits</param>
        /// <returns></returns>
        public static ulong GetPerfCtlValue(byte perfEvent, byte umask, bool edge, byte cmask, byte perfEventHi)
        {
            return GetPerfCtlValue(perfEvent, 
                umask, 
                OsUsrMode.All, 
                edge, 
                interrupt: false, 
                enable: true, 
                invert: false, 
                cmask, 
                perfEventHi, 
                HostGuestOnly.All);
        }

        /// <summary>
        /// Get core perf ctl value
        /// </summary>
        /// <param name="perfEvent">Low 16 bits of performance event</param>
        /// <param name="umask">perf event umask</param>
        /// <param name="osUsrMode">Count in os or user mode</param>
        /// <param name="edge">only increment on transition</param>
        /// <param name="interrupt">generate apic interrupt on overflow</param>
        /// <param name="enable">enable perf ctr</param>
        /// <param name="invert">invert cmask</param>
        /// <param name="cmask">0 = increment by event count. >0 = increment by 1 if event count in clock cycle >= cmask</param>
        /// <param name="perfEventHi">high 4 bits of performance event</param>
        /// <param name="hostGuestOnly">Count host or guest events</param>
        /// <returns>value for perf ctl msr</returns>
        public static ulong GetPerfCtlValue(byte perfEvent, byte umask, OsUsrMode osUsrMode, bool edge, bool interrupt, bool enable, bool invert, byte cmask, byte perfEventHi, HostGuestOnly hostGuestOnly)
        {
            return perfEvent |
                (ulong)umask << 8 |
                ((ulong)osUsrMode) << 16 |
                (edge ? 1UL : 0UL) << 18 |
                (interrupt ? 1UL : 0UL) << 20 |
                (enable ? 1UL : 0UL) << 22 |
                (invert ? 1UL : 0UL) << 23 |
                (ulong)cmask << 24 |
                (ulong)perfEventHi << 32 |
                ((ulong)hostGuestOnly) << 40;
        }

        /// <summary>
        /// Selects what ring(s) events are counted for
        /// </summary>
        public enum OsUsrMode
        {
            None = 0b00,
            Usr = 0b01,
            OS = 0b10,
            All = 0b11
        }

        /// <summary>
        /// Whether to count events for guest (VM) or host
        /// </summary>
        public enum HostGuestOnly
        {
            All = 0b00,
            Guest = 0b01,
            Host = 0b10,
            AllSvme = 0b11
        }

        public class NormalizedCoreCounterData
        {
            /// <summary>
            /// Time stamp counter
            /// Increments at P0 frequency
            /// </summary>
            public float tsc;

            /// <summary>
            /// Programmable performance counter values
            /// </summary>
            public float ctr0;
            public float ctr1;
            public float ctr2;
            public float ctr3;

            public float NormalizationFactor;

            public ulong totalctr0;
            public ulong totalctr1;
            public ulong totalctr2;
            public ulong totalctr3;
        }
    }
}

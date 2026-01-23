using PmcReader.Interop;
using System;

namespace PmcReader.Intel
{
    public class AlderLakeUncore : ModernIntelCpu
    {
        public const uint MSR_UNC_PERF_GLOBAL_CTRL = 0x2FF0;
        public const uint MSR_UNC_PERF_FIXED_CTRL = 0x2FDE;
        public const uint MSR_UNC_PERF_FIXED_CTR = 0x2FDF;
        public const uint MSR_UNC_CBO_CONFIG = 0x396;
        public const uint MSR_UNC_CBO_PERFEVTSEL0_base = 0x2000;
        public const uint MSR_UNC_CBO_PERFEVTSEL1_base = 0x2001;
        public const uint MSR_UNC_CBO_PERFCTR0_base = 0x2002;
        public const uint MSR_UNC_CBO_PERFCTR1_base = 0x2003;
        public const uint MSR_UNC_ARB_PERFCTR0 = 0x2FD2;
        public const uint MSR_UNC_ARB_PERFCTR1 = 0x2FD3;
        public const uint MSR_UNC_ARB_PERFEVTSEL0 = 0x2FD0;
        public const uint MSR_UNC_ARB_PERFEVTSEL1 = 0x2FD1;
        public const uint MSR_UNC_CBO_increment = 0x8;

        public AlderLakeUncore()
        {
            architectureName = "Alder Lake Client Uncore";
        }

        /// <summary>
        /// Enable uncore counters, wtih overflow propagation/freezing disabled
        /// </summary>
        public void EnableUncoreCounters()
        {
            // Bit 29 - globally enable all PMU counters. 
            // local counters still have to be individually enabled
            // other bits have to do with PMI or are reserved
            ulong enableUncoreCountersValue = 1UL << 29;
            Ring0.WriteMsr(MSR_UNC_PERF_GLOBAL_CTRL, enableUncoreCountersValue);

            // Bit 22 - locally enable fixed counter
            ulong enableUncoreFixedCtrValue = 1UL << 22;
            Ring0.WriteMsr(MSR_UNC_PERF_FIXED_CTRL, enableUncoreFixedCtrValue);
        }

        /// <summary>
        /// Get value to put in PERFEVTSEL register, for uncore counters
        /// </summary>
        /// <param name="perfEvent">Perf event</param>
        /// <param name="umask">Perf event qualification (umask)</param>
        /// <param name="edge">Edge detect</param>
        /// <param name="ovf_en">Enable overflow forwarding</param>
        /// <param name="enable">Enable counter</param>
        /// <param name="invert">Invert cmask</param>
        /// <param name="cmask">Count mask</param>
        /// <returns>value to put in perfevtsel register</returns>
        public static ulong GetUncorePerfEvtSelRegisterValue(byte perfEvent,
            byte umask,
            bool edge,
            bool ovf_en,
            bool enable,
            bool invert,
            byte cmask)
        {
            return perfEvent |
                (ulong)umask << 8 |
                (edge ? 1UL : 0UL) << 18 |
                (ovf_en ? 1UL : 0UL) << 20 |
                (enable ? 1UL : 0UL) << 22 |
                (invert ? 1UL : 0UL) << 23 |
                (ulong)(cmask & 0xF) << 24;
        }
    }
}

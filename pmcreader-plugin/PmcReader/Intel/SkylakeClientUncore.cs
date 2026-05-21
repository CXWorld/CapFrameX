using PmcReader.Interop;
using System;

namespace PmcReader.Intel
{
    public class SkylakeClientUncore : ModernIntelCpu
    {
        public const uint MSR_UNC_PERF_GLOBAL_CTRL = 0xE01;
        public const uint MSR_UNC_PERF_FIXED_CTRL = 0x394;
        public const uint MSR_UNC_PERF_FIXED_CTR = 0x395;
        public const uint MSR_UNC_CBO_CONFIG = 0x396;
        public const uint MSR_UNC_CBO_PERFEVTSEL0_base = 0x700;
        public const uint MSR_UNC_CBO_PERFEVTSEL1_base = 0x701;
        public const uint MSR_UNC_CBO_PERFCTR0_base = 0x706;
        public const uint MSR_UNC_CBO_PERFCTR1_base = 0x707;
        public const uint MSR_UNC_ARB_PERFCTR0 = 0x3B0;
        public const uint MSR_UNC_ARB_PERFCTR1 = 0x3B1;
        public const uint MSR_UNC_ARB_PERFEVTSEL0 = 0x3B2;
        public const uint MSR_UNC_ARB_PERFEVTSEL1 = 0x3B3;
        public const uint MSR_UNC_CBO_increment = 0x10;

        public const ulong BAR_MASK = 0x0007FFFFF8000;
        public const uint DRAM_GT_REQUESTS_OFFSET = 0x5040;
        public const uint DRAM_IA_REQUESTS_OFFSET = 0x5044;
        public const uint DRAM_IO_REQUESTS_OFFSET = 0x5048;
        public const uint DRAM_DATA_READS_OFFSET = 0x5050;
        public const uint DRAM_DATA_WRITES_OFFSET = 0x5054;

        public ulong barAddress;

        public SkylakeClientUncore()
        {
            architectureName = "Skylake Client Uncore";
            Ring0.ReadPciConfig(Ring0.GetPciAddress(0, 0, 0), 0x48, out uint barLo);
            barAddress = (ulong)barLo & BAR_MASK;
        }

        /// <summary>
        /// Enable skylake uncore counters, wtih overflow propagation/freezing disabled
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
                (ulong)(cmask & 0x1F) << 24;
        }
    }
}

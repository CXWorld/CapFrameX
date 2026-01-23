using PmcReader.Interop;
using System;

namespace PmcReader.Intel
{
    // Really just a container for MSR numbers
    public class MeteorLakeUncore : ModernIntelCpu
    {   // applies to cbox, arb, hac_arb, hac_cbox (hbo)
        public const uint MTL_UNC_INCREMENT = 0x10;

        // same as Ice Lake
        public const uint MTL_UNC_CBO_CONFIG = 0x396;
        public const ulong MTL_UNC_NUM_CBO_MASK = 0xF;

        // SNCU and CNCU provide fixed counters for clock ticks
        public const uint MTL_UNC_SNCU_FIXED_CTRL = 0x2002;
        public const uint MTL_UNC_SNCU_FIXED_CTR = 0x2008;
        public const uint MTL_UNC_SNCU_BOX_CTRL = 0x200e;
        public const uint MTL_UNC_CNCU_FIXED_CTRL = 0x2402;
        public const uint MTL_UNC_CNCU_FIXED_CTR = 0x2408;
        public const uint MTL_UNC_CNCU_BOX_CTRL = 0x240e;

        // Note there are *two* ARB boxes
        // System agent's arbitration queue?
        public const uint MTL_UNC_ARB_CTRL = 0x2412;
        public const uint MTL_UNC_ARB_CTR = 0x2418;

        // Home agent's arbitration queue? Compute tile -> SoC tile
        public const uint MTL_UNC_HAC_ARB_CTRL = 0x2012;
        public const uint MTL_UNC_HAC_ARB_CTR = 0x2018;

        // Home agent cbox? 2 counters
        public const uint MTL_UNC_HAC_CBO_CTRL = 0x2042;
        public const uint MTL_UNC_HAC_CBO_CTR = 0x2048;

        // L3 cboxes. 2x 48-bit ctrs per cbo
        public const uint MTL_UNC_CBO_CTRL = 0x2442;
        public const uint MTL_UNC_CBO_CTR = 0x2448;

        public MeteorLakeUncore()
        {
            architectureName = "Meteor Lake Uncore";
        }

        /// <summary>
        /// Enable uncore counters, wtih overflow propagation/freezing disabled
        /// </summary>
        public void EnableUncoreCounters()
        {
            // MTL doesn't appear to have global uncore enable registers.
            // Setting enable bit on sNCU/cNCU fixed counter ctrl regs is enough to enable counting
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
                // From kernel sources, ADL widens the threshold field to bits 24:29
                // LNL adds a threshold2 field, making it 16 bits
                (ulong)(cmask & 0xFF) << 24; 
        }
    }
}

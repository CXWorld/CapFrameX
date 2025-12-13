using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Windows.Win32.Foundation;
using LibreHardwareMonitor.Hardware;

namespace LibreHardwareMonitor.Hardware.Cpu
{
    /// <summary>
    /// Provides methods to determine CPU architecture features.
    /// </summary>
    public class CpuArchitecture
    {
        private const uint CPUID_CORE_MASK_STATUS = 0x1A;

        /// <summary>
        /// Determines whether the CPU architecture is a hybrid design.
        /// </summary>
        /// <param name="cpuId"></param>
        /// <returns></returns>
        public static bool IsHybridDesign(CpuId[] cpuId)
        {
            Vendor vendor = cpuId[0].Vendor;

            // Alder Lake (Intel 7/10nm): 0x97, 0x9A
            // Raptor Lake (Intel 7/10nm): 0xB7
            // Raptor Lake-H (Intel 7/10nm): 0xBA
            // Raptor Lake (Alder Lake Refresh) (Intel 7/10nm): 0xBF
            // Meteor Lake (Intel 4/7nm): 0xAA
            // Lunar Lake (TSMC 3nm): 0xBD
            // Arrow Lake (TSMC 3nm): 0xC6
            // Arrow Lake-H (TSMC 3nm): 0xC5
            if (vendor == Vendor.Intel)
            {
                bool isHybrid = false;

                for (int i = 0; i < cpuId.Length; i++)
                {
                    var previousAffinity = ThreadAffinity.Set(cpuId[i].Affinity);
                    if (OpCode.CpuId(CPUID_CORE_MASK_STATUS, 0, out uint eax, out uint ebx, out uint ecx, out uint edx))
                    {
                        switch (eax >> 24)
                        {
                            // Efficiency cores (E-cores)
                            case 0x20: isHybrid = true; break;
                            default: break;
                        }
                    }

                    ThreadAffinity.Set(previousAffinity);

                    if (isHybrid)
                        break;
                }

                return isHybrid;

            }
            // Zen 5c Strix Point
            // Zen 4c Phoenix 2
            else if (vendor == Vendor.AMD)
            {
                bool isHybrid = false;

                for (int i = 0; i < cpuId.Length; i++)
                {
                    var previousAffinity = ThreadAffinity.Set(cpuId[i].Affinity);
                    if (OpCode.CpuId(0x80000026, 0, out uint eax, out uint ebx, out uint ecx, out uint edx))
                    {
                        if ((eax & (1u << 30)) != 0)
                        {
                            uint coreType = (ebx >> 28) & 0xF;
                            switch (coreType)
                            {
                                // Dense cores (D-cores)
                                case 1: isHybrid = true; break;
                                default: break;
                            }
                        }
                    }

                    ThreadAffinity.Set(previousAffinity);

                    if (isHybrid)
                        break;
                }

                return isHybrid;
            }
            else
            {
                // Unknown vendor or architecture
                return false;
            }
        }
    }
}

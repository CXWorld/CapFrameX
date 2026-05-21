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
            // Nova Lake-S/Sx (Intel 18A): family 0x12 model 0x03 (new family ID, CPUID 0x00300F30)
            // Nova Lake-U/P/H/HX (Intel 18A): family 0x06 model 0x55 (CPUID 0x00050650)
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
            // AMD hybrid (standard + dense "c" cores on same package):
            //   Zen 4c: Phoenix 2
            //   Zen 5c: Strix Point (also Strix Halo)
            //   Zen 6c: Medusa Point 1/2 (Family 0x1A model 0x88/0xE4),
            //           Medusa Point 3 (Family 0x1B model 0x00)
            // Server dense-only parts (Rushmore EB Dense, Weisshorn SP8D) are not hybrid.
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

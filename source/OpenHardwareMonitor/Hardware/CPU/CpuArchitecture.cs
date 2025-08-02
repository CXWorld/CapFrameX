namespace OpenHardwareMonitor.Hardware.CPU
{
    public class CpuArchitecture
    {
        public static bool IsHybridDesign(CPUID cpu)
        {
            if (cpu.Vendor == Vendor.Intel)
            {
                switch (cpu.Family)
                {
                    case 0x06:
                        // Alder Lake (Intel 7/10nm): 0x97, 0x9A
                        // Raptor Lake (Intel 7/10nm): 0xB7
                        // Raptor Lake-H (Intel 7/10nm): 0xBA
                        // Raptor Lake (Alder Lake Refresh) (Intel 7/10nm): 0xBF
                        // Meteor Lake (Intel 4/7nm): 0xAA
                        // Lunar Lake (TSMC 3nm): 0xBD
                        // Arrow Lake (TSMC 3nm): 0xC6
                        // Arrow Lake-H (TSMC 3nm): 0xC5
                        return cpu.Model == 0x97 || cpu.Model == 0x9A || cpu.Model == 0xB7 || cpu.Model == 0xBA || cpu.Model == 0xBF ||
                            cpu.Model == 0xAA || cpu.Model == 0xBD || cpu.Model == 0xC6 || cpu.Model == 0xC5;

                    case 0x12:
                        // Nova Lake-S (Intel 18A + TSMC N2): 0x01
                        // Nova Lake-L (Intel 18A + TSMC N2): 0x03
                        return cpu.Model == 0x01 || cpu.Model == 0x03;

                    default:
                        return false;
                }
            }
            else if (cpu.Vendor == Vendor.AMD)
            {
                switch (cpu.Family)
                {
                    case 0x1A:
                        return true; // Zen 5c Strix Point
                    case 0x19:
                        return true; // Zen 4c Phoenix 2
                    default:
                        return false;
                }
            }
            else
            {
                // Unknown vendor or architecture
                return false;
            }
        }
    }
}

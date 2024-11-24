namespace OpenHardwareMonitor.Hardware.CPU
{
    public class CpuArchitecture
    {
        public static bool IsHybridDesign(CPUID cpu)
        {
            // Alder Lake (Intel 7/10nm): 0x97, 0x9A
            // Raptor Lake (Intel 7/10nm): 0xB7
            // Raptor Lake-H (Intel 7/10nm): 0xBA
            // Raptor Lake (Alder Lake Refresh) (Intel 7/10nm): 0xBF
            // Meteor Lake (Intel 4/7nm): 0xAA
            // Lunar Lake (TSMC 3nm): 0xBD
            // Arrow Lake (TSMC 3nm): 0xC6
            return cpu.Vendor == Vendor.Intel && cpu.Family == 0x06
                && (cpu.Model == 0x97 || cpu.Model == 0x9A || cpu.Model == 0xB7 || 
                cpu.Model == 0xBF || cpu.Model == 0xBA || cpu.Model == 0xAA || cpu.Model == 0xBD || cpu.Model == 0xC6);
        }
    }
}

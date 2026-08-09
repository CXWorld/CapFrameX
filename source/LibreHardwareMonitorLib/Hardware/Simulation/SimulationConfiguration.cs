using System.Collections.Generic;

#pragma warning disable CS1591 // file exempt from XML documentation

namespace LibreHardwareMonitor.Hardware.Simulation;

public enum SimulationMode
{
    Disabled = 0,
    Enabled = 1
}

public enum SimulatedCpuKind
{
    Amd17Cpu = 0,
    IntelCpu = 1
}

public enum SimulatedGpuKind
{
    AmdGpu = 0,
    NvidiaGpu = 1,
    IntelGclGpu = 2,
    IntelD3DGpu = 3
}

public sealed class SimulationConfiguration
{
    public SimulationMode Mode { get; set; } = SimulationMode.Disabled;

    public SimulatedCpuKind Cpu { get; set; } = SimulatedCpuKind.Amd17Cpu;

    public List<SimulatedGpuKind> Gpus { get; set; } = new List<SimulatedGpuKind>();
}

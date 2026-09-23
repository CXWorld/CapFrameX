using System.Collections.Generic;

#pragma warning disable CS1591 // file exempt from XML documentation

namespace LibreHardwareMonitor.Hardware.Simulation;

public enum SimulationMode
{
    Disabled = 0,
    Enabled = 1
}

/// <summary>
/// Selects the processor the simulated CPU group publishes. The layouts live in
/// <c>SimulatedCpuModels</c>; core names carry the same hybrid suffixes the real drivers derive
/// from CPUID ("P"/"E"/"LPE" on Intel, "P"/"D"/"LP" on AMD, none on homogeneous parts).
/// </summary>
public enum SimulatedCpuKind
{
    /// <summary>AMD Ryzen 9 7950X: 16 identical Zen 4 cores, no hybrid suffix.</summary>
    Amd17Cpu = 0,

    /// <summary>Intel Core i9-14900K: 8 P-cores + 16 E-cores.</summary>
    IntelCpu = 1,

    /// <summary>Intel Core Ultra 9 285H: 6 P-cores + 8 E-cores + 2 LPE-cores.</summary>
    IntelLpeCpu = 2,

    /// <summary>AMD Ryzen AI 9 HX 370: 4 Zen 5 P-cores + 8 Zen 5c D-cores.</summary>
    AmdHybridCpu = 3
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

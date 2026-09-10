using System.Collections.Generic;
using System.Text;

namespace LibreHardwareMonitor.Hardware.Simulation;

internal sealed class SimulatedCpuGroup : IGroup
{
    private readonly List<IHardware> _hardware = new();
    private readonly StringBuilder _report = new();

    public SimulatedCpuGroup(SimulationConfiguration configuration, ISettings settings)
    {
        SimulationConfiguration config = configuration ?? new SimulationConfiguration();

        SimulatedCpuBase cpu = config.Cpu switch
        {
            SimulatedCpuKind.IntelCpu => new SimulatedIntelCpu(SimulatedCpuModels.IntelCoreI9_14900K, 0, settings),
            SimulatedCpuKind.IntelLpeCpu => new SimulatedIntelCpu(SimulatedCpuModels.IntelCoreUltra9_285H, 0, settings),
            SimulatedCpuKind.AmdHybridCpu => new SimulatedAmd17Cpu(SimulatedCpuModels.AmdRyzenAi9Hx370, 0, settings),
            _ => new SimulatedAmd17Cpu(SimulatedCpuModels.AmdRyzen9_7950X, 0, settings)
        };

        _hardware.Add(cpu);

        _report.AppendLine("Simulated CPU");
        _report.AppendLine();
        _report.Append("Mode: ");
        _report.AppendLine(config.Mode.ToString());
        _report.Append("CPU: ");
        _report.AppendLine(config.Cpu.ToString());
        _report.Append("Model: ");
        _report.AppendLine(cpu.Model.Name);
        _report.Append("Layout: ");
        _report.AppendLine(cpu.Model.DescribeLayout());
        _report.AppendLine();
    }

    public IReadOnlyList<IHardware> Hardware => _hardware;

    public string GetReport()
    {
        return _report.ToString();
    }

    public void Close()
    {
        foreach (SimulatedCpuBase cpu in _hardware)
            cpu.Close();
    }
}

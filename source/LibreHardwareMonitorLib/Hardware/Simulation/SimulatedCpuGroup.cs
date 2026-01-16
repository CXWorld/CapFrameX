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

        _report.AppendLine("Simulated CPU");
        _report.AppendLine();
        _report.Append("Mode: ");
        _report.AppendLine(config.Mode.ToString());
        _report.Append("CPU: ");
        _report.AppendLine(config.Cpu.ToString());
        _report.AppendLine();

        switch (config.Cpu)
        {
            case SimulatedCpuKind.IntelCpu:
                _hardware.Add(new SimulatedIntelCpu(0, settings));
                break;
            case SimulatedCpuKind.Amd17Cpu:
            default:
                _hardware.Add(new SimulatedAmd17Cpu(0, settings));
                break;
        }
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

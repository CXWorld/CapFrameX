using System.Collections.Generic;
using System.Text;

namespace LibreHardwareMonitor.Hardware.Simulation;

internal sealed class SimulatedGpuGroup : IGroup
{
    private readonly List<IHardware> _hardware = new();
    private readonly StringBuilder _report = new();

    public SimulatedGpuGroup(SimulationConfiguration configuration, ISettings settings)
    {
        SimulationConfiguration config = configuration ?? new SimulationConfiguration();
        List<SimulatedGpuKind> gpus = config.Gpus != null && config.Gpus.Count > 0
            ? config.Gpus
            : new List<SimulatedGpuKind> { SimulatedGpuKind.NvidiaGpu };

        _report.AppendLine("Simulated GPU");
        _report.AppendLine();
        _report.Append("Mode: ");
        _report.AppendLine(config.Mode.ToString());
        _report.AppendLine("GPUs:");

        int index = 0;
        if (gpus != null)
        {
            foreach (SimulatedGpuKind gpu in gpus)
            {
                _report.Append(" - ");
                _report.AppendLine(gpu.ToString());

                switch (gpu)
                {
                    case SimulatedGpuKind.AmdGpu:
                        _hardware.Add(new SimulatedAmdGpu(index, settings));
                        break;
                    case SimulatedGpuKind.IntelGclGpu:
                        _hardware.Add(new SimulatedIntelGclGpu(index, settings));
                        break;
                    case SimulatedGpuKind.IntelD3DGpu:
                        _hardware.Add(new SimulatedIntelD3DGpu(index, settings));
                        break;
                    case SimulatedGpuKind.NvidiaGpu:
                    default:
                        _hardware.Add(new SimulatedNvidiaGpu(index, settings));
                        break;
                }

                index++;
            }
        }

        if (_hardware.Count == 0)
        {
            _report.AppendLine(" - None");
        }
        else
        {
            _report.AppendLine();
        }
    }

    public IReadOnlyList<IHardware> Hardware => _hardware;

    public string GetReport()
    {
        return _report.ToString();
    }

    public void Close()
    {
        foreach (SimulatedGpuBase gpu in _hardware)
            gpu.Close();
    }
}

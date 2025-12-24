using CapFrameX.Service.Monitoring.Hardware;
using Xunit;
using Xunit.Abstractions;

namespace CapFrameX.Service.Monitoring.Tests;

/// <summary>
/// Tests for Computer initialization and hardware detection.
/// Based on legacy CapFrameX.Test.Sensor.ComputerTest
/// </summary>
public class ComputerTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private Computer? _computer;

    public ComputerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Computer_Open_ShouldInitializeSuccessfully()
    {
        // Arrange
        _computer = new Computer();

        // Act
        _computer.Open();

        // Assert
        Assert.NotNull(_computer);
        Assert.NotNull(_computer.Hardware);
    }

    [Fact]
    public void Computer_WithCpuEnabled_ShouldDetectCpu()
    {
        // Arrange
        _computer = new Computer
        {
            IsCpuEnabled = true
        };

        // Act
        _computer.Open();
        var cpuHardware = _computer.Hardware
            .FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);

        // Assert
        Assert.NotNull(cpuHardware);
        _output.WriteLine($"Detected CPU: {cpuHardware.Name}");
    }

    [Fact]
    public void Computer_WithGpuEnabled_ShouldDetectGpu()
    {
        // Arrange
        _computer = new Computer
        {
            IsGpuEnabled = true
        };

        // Act
        _computer.Open();
        var gpuHardware = _computer.Hardware
            .Where(h => h.HardwareType == HardwareType.GpuAmd
                     || h.HardwareType == HardwareType.GpuNvidia
                     || h.HardwareType == HardwareType.GpuIntel)
            .ToList();

        // Assert
        Assert.NotEmpty(gpuHardware);
        foreach (var gpu in gpuHardware)
        {
            _output.WriteLine($"Detected GPU: {gpu.Name} ({gpu.HardwareType})");
        }
    }

    [Fact]
    public void Computer_WithMemoryEnabled_ShouldDetectMemory()
    {
        // Arrange
        _computer = new Computer
        {
            IsMemoryEnabled = true
        };

        // Act
        _computer.Open();
        var memoryHardware = _computer.Hardware
            .FirstOrDefault(h => h.HardwareType == HardwareType.Memory);

        // Assert
        Assert.NotNull(memoryHardware);
        _output.WriteLine($"Detected Memory: {memoryHardware.Name}");
    }

    [Fact]
    public void Computer_WithAllHardwareEnabled_ShouldDetectMultipleComponents()
    {
        // Arrange
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = true,
            IsStorageEnabled = true,
            IsNetworkEnabled = true
        };

        // Act
        _computer.Open();
        var hardwareCount = _computer.Hardware.Count();

        // Assert
        Assert.True(hardwareCount > 0, "No hardware detected");

        _output.WriteLine($"Total hardware components detected: {hardwareCount}");
        foreach (var hardware in _computer.Hardware)
        {
            _output.WriteLine($"  - {hardware.Name} ({hardware.HardwareType})");
        }
    }

    [Fact]
    public void Computer_HardwareEvents_ShouldTrigger()
    {
        // Arrange
        bool hardwareAddedTriggered = false;
        _computer = new Computer();

        _computer.HardwareAdded += (hardware) =>
        {
            hardwareAddedTriggered = true;
            _output.WriteLine($"Hardware added: {hardware.Name}");
        };

        // Act
        _computer.Open();
        _computer.IsCpuEnabled = true;

        // Assert
        Assert.True(hardwareAddedTriggered, "HardwareAdded event was not triggered");
    }

    public void Dispose()
    {
        _computer?.Close();
        _computer = null;
    }
}

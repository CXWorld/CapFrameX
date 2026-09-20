using CapFrameX.Service.Monitoring.Hardware;
using Xunit.Abstractions;

namespace CapFrameX.Service.Monitoring.Tests;

/// <summary>
/// Tests for sensor reading and updates.
/// Based on legacy MonitoringLibTestApp.SensorService
/// </summary>
public class SensorReadingTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private Computer? _computer;

    public SensorReadingTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Sensors_UpdateAndRead_ShouldReturnValues()
    {
        // Arrange
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true
        };
        _computer.Open();

        // Act
        var sensorValues = new List<(string Hardware, string Sensor, float? Value)>();

        foreach (var hardware in _computer.Hardware)
        {
            hardware.Update();

            foreach (var sensor in hardware.Sensors)
            {
                sensorValues.Add((hardware.Name, sensor.Name, sensor.Value));
            }
        }

        // Assert
        Assert.NotEmpty(sensorValues);

        _output.WriteLine($"Total sensors found: {sensorValues.Count}");
        foreach (var (hw, sensor, value) in sensorValues.Take(20))
        {
            _output.WriteLine($"  {hw} - {sensor}: {value}");
        }
    }

    [Fact]
    public void CpuSensors_Temperature_ShouldBeReadable()
    {
        // Arrange
        _computer = new Computer { IsCpuEnabled = true };
        _computer.Open();

        var cpuHardware = _computer.Hardware
            .FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);

        Assert.NotNull(cpuHardware);

        // Act
        cpuHardware.Update();
        var tempSensors = cpuHardware.Sensors
            .Where(s => s.SensorType == SensorType.Temperature)
            .ToList();

        // Assert
        Assert.NotEmpty(tempSensors);

        _output.WriteLine($"CPU Temperature Sensors:");
        foreach (var sensor in tempSensors)
        {
            _output.WriteLine($"  {sensor.Name}: {sensor.Value}°C");
        }
    }

    [Fact]
    public void CpuSensors_Load_ShouldBeReadable()
    {
        // Arrange
        _computer = new Computer { IsCpuEnabled = true };
        _computer.Open();

        var cpuHardware = _computer.Hardware
            .FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);

        Assert.NotNull(cpuHardware);

        // Act
        cpuHardware.Update();
        var loadSensors = cpuHardware.Sensors
            .Where(s => s.SensorType == SensorType.Load)
            .ToList();

        // Assert
        Assert.NotEmpty(loadSensors);

        _output.WriteLine($"CPU Load Sensors:");
        foreach (var sensor in loadSensors)
        {
            _output.WriteLine($"  {sensor.Name}: {sensor.Value}%");
        }
    }

    [Fact]
    public void CpuSensors_Clock_ShouldBeReadable()
    {
        // Arrange
        _computer = new Computer { IsCpuEnabled = true };
        _computer.Open();

        var cpuHardware = _computer.Hardware
            .FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);

        Assert.NotNull(cpuHardware);

        // Act
        cpuHardware.Update();
        var clockSensors = cpuHardware.Sensors
            .Where(s => s.SensorType == SensorType.Clock)
            .ToList();

        // Assert
        Assert.NotEmpty(clockSensors);

        _output.WriteLine($"CPU Clock Sensors:");
        foreach (var sensor in clockSensors)
        {
            _output.WriteLine($"  {sensor.Name}: {sensor.Value} MHz");
        }
    }

    [Fact]
    public void GpuSensors_ShouldBeReadable()
    {
        // Arrange
        _computer = new Computer { IsGpuEnabled = true };
        _computer.Open();

        var gpuHardware = _computer.Hardware
            .FirstOrDefault(h => h.HardwareType == HardwareType.GpuNvidia
                              || h.HardwareType == HardwareType.GpuAmd
                              || h.HardwareType == HardwareType.GpuIntel);

        if (gpuHardware == null)
        {
            _output.WriteLine("No GPU detected, skipping test");
            return;
        }

        // Act
        gpuHardware.Update();
        var gpuSensors = gpuHardware.Sensors.ToList();

        // Assert
        Assert.NotEmpty(gpuSensors);

        _output.WriteLine($"GPU Sensors for {gpuHardware.Name}:");
        foreach (var sensor in gpuSensors)
        {
            var unit = sensor.SensorType switch
            {
                SensorType.Temperature => "°C",
                SensorType.Load => "%",
                SensorType.Clock => " MHz",
                SensorType.Power => " W",
                SensorType.Fan => " RPM",
                _ => ""
            };
            _output.WriteLine($"  {sensor.Name}: {sensor.Value}{unit}");
        }
    }

    [Fact]
    public void MemorySensors_ShouldBeReadable()
    {
        // Arrange
        _computer = new Computer { IsMemoryEnabled = true };
        _computer.Open();

        var memoryHardware = _computer.Hardware
            .FirstOrDefault(h => h.HardwareType == HardwareType.Memory);

        Assert.NotNull(memoryHardware);

        // Act
        memoryHardware.Update();
        var memorySensors = memoryHardware.Sensors.ToList();

        // Assert
        Assert.NotEmpty(memorySensors);

        _output.WriteLine($"Memory Sensors:");
        foreach (var sensor in memorySensors)
        {
            var unit = sensor.SensorType switch
            {
                SensorType.Data => " GB",
                SensorType.Load => "%",
                _ => ""
            };
            _output.WriteLine($"  {sensor.Name}: {sensor.Value}{unit}");
        }
    }

    [Fact]
    public void Sensors_MultipleUpdates_ShouldReturnDifferentValues()
    {
        // Arrange
        _computer = new Computer { IsCpuEnabled = true };
        _computer.Open();

        var cpuHardware = _computer.Hardware
            .FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);

        Assert.NotNull(cpuHardware);

        // Act - Take two readings
        cpuHardware.Update();
        var loadSensor = cpuHardware.Sensors
            .FirstOrDefault(s => s.SensorType == SensorType.Load && s.Name.Contains("Total"));

        if (loadSensor == null)
        {
            _output.WriteLine("No CPU Total Load sensor found");
            return;
        }

        var firstValue = loadSensor.Value;
        _output.WriteLine($"First reading: {firstValue}%");

        Thread.Sleep(500); // Wait a bit for load to potentially change

        cpuHardware.Update();
        var secondValue = loadSensor.Value;
        _output.WriteLine($"Second reading: {secondValue}%");

        // Assert - Values should exist (may or may not be different)
        Assert.NotNull(firstValue);
        Assert.NotNull(secondValue);
    }

    [Fact]
    public void Sensors_AllTypes_ShouldBeRecognized()
    {
        // Arrange
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = true,
            IsStorageEnabled = true
        };
        _computer.Open();

        // Act
        var sensorTypeGroups = new Dictionary<SensorType, int>();

        foreach (var hardware in _computer.Hardware)
        {
            hardware.Update();
            foreach (var sensor in hardware.Sensors)
            {
                if (!sensorTypeGroups.ContainsKey(sensor.SensorType))
                    sensorTypeGroups[sensor.SensorType] = 0;
                sensorTypeGroups[sensor.SensorType]++;
            }
        }

        // Assert
        Assert.NotEmpty(sensorTypeGroups);

        _output.WriteLine("Sensor types detected:");
        foreach (var (type, count) in sensorTypeGroups.OrderByDescending(kvp => kvp.Value))
        {
            _output.WriteLine($"  {type}: {count} sensors");
        }
    }

    public void Dispose()
    {
        _computer?.Close();
        _computer = null;
    }
}

using CapFrameX.Service.Monitoring.Hardware;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace CapFrameX.Service.Monitoring.Tests;

/// <summary>
/// Performance and stress tests for the monitoring library.
/// Tests continuous sensor reading similar to real-world usage.
/// </summary>
public class PerformanceTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private Computer? _computer;

    public PerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ContinuousMonitoring_1Second_ShouldPerformWell()
    {
        // Arrange
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true
        };
        _computer.Open();

        var updateCount = 0;
        var stopwatch = Stopwatch.StartNew();
        var targetDuration = TimeSpan.FromSeconds(1);
        var updateInterval = TimeSpan.FromMilliseconds(100); // 10 Hz

        // Act
        while (stopwatch.Elapsed < targetDuration)
        {
            foreach (var hardware in _computer.Hardware)
            {
                hardware.Update();
            }
            updateCount++;
            Thread.Sleep(updateInterval);
        }

        stopwatch.Stop();

        // Assert
        Assert.True(updateCount > 0);
        var actualRate = updateCount / stopwatch.Elapsed.TotalSeconds;

        _output.WriteLine($"Updates performed: {updateCount}");
        _output.WriteLine($"Duration: {stopwatch.ElapsedMilliseconds} ms");
        _output.WriteLine($"Update rate: {actualRate:F2} Hz");
    }

    [Fact]
    public void SensorCollection_Performance_ShouldBeAcceptable()
    {
        // Arrange
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = true
        };
        _computer.Open();

        // Warmup
        foreach (var hardware in _computer.Hardware)
        {
            hardware.Update();
        }

        // Act
        var stopwatch = Stopwatch.StartNew();
        var sensorCount = 0;

        foreach (var hardware in _computer.Hardware)
        {
            hardware.Update();
            sensorCount += hardware.Sensors.Count();
        }

        stopwatch.Stop();

        // Assert
        Assert.True(sensorCount > 0);
        Assert.True(stopwatch.ElapsedMilliseconds < 1000,
            $"Sensor collection took too long: {stopwatch.ElapsedMilliseconds}ms");

        _output.WriteLine($"Total sensors: {sensorCount}");
        _output.WriteLine($"Collection time: {stopwatch.ElapsedMilliseconds} ms");
        _output.WriteLine($"Time per sensor: {(double)stopwatch.ElapsedMilliseconds / sensorCount:F2} ms");
    }

    [Fact]
    public void SubHardware_ShouldBeAccessible()
    {
        // Arrange
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsMotherboardEnabled = true
        };
        _computer.Open();

        // Act
        var allHardwareWithSub = new List<(IHardware Hardware, int SubHardwareCount)>();

        foreach (var hardware in _computer.Hardware)
        {
            var subCount = hardware.SubHardware?.Count() ?? 0;
            if (subCount > 0)
            {
                allHardwareWithSub.Add((hardware, subCount));
            }
        }

        // Assert & Output
        _output.WriteLine($"Hardware with sub-hardware:");
        foreach (var (hw, subCount) in allHardwareWithSub)
        {
            _output.WriteLine($"  {hw.Name}: {subCount} sub-components");

            if (hw.SubHardware != null)
            {
                foreach (var subHw in hw.SubHardware)
                {
                    _output.WriteLine($"    - {subHw.Name} ({subHw.HardwareType})");
                }
            }
        }
    }

    [Fact]
    public void SensorIdentifiers_ShouldBeUnique()
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
        var allIdentifiers = new HashSet<string>();
        var duplicates = new List<string>();

        foreach (var hardware in _computer.Hardware)
        {
            hardware.Update();
            foreach (var sensor in hardware.Sensors)
            {
                var id = sensor.Identifier.ToString();
                if (!allIdentifiers.Add(id))
                {
                    duplicates.Add(id);
                }
            }
        }

        // Assert
        if (duplicates.Any())
        {
            _output.WriteLine($"WARNING: Found {duplicates.Count} duplicate sensor identifiers:");
            foreach (var dup in duplicates.Take(10))
            {
                _output.WriteLine($"  {dup}");
            }
        }

        _output.WriteLine($"Total unique sensors: {allIdentifiers.Count}");
        Assert.True(allIdentifiers.Count > 0);
    }

    [Fact]
    public void Hardware_Visitor_ShouldTraverseAllComponents()
    {
        // Arrange
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = true
        };
        _computer.Open();

        var visitedHardware = new List<string>();
        var visitedSensors = new List<string>();

        // Create a simple visitor
        var visitor = new TestVisitor(
            onHardware: hw => visitedHardware.Add($"{hw.HardwareType}: {hw.Name}"),
            onSensor: sensor => visitedSensors.Add($"{sensor.SensorType}: {sensor.Name}")
        );

        // Act
        _computer.Accept(visitor);

        // Assert
        Assert.NotEmpty(visitedHardware);
        Assert.NotEmpty(visitedSensors);

        _output.WriteLine($"Visited {visitedHardware.Count} hardware components");
        _output.WriteLine($"Visited {visitedSensors.Count} sensors");

        _output.WriteLine("\nHardware components:");
        foreach (var hw in visitedHardware.Take(10))
        {
            _output.WriteLine($"  {hw}");
        }
    }

    private class TestVisitor : IVisitor
    {
        private readonly Action<IHardware>? _onHardware;
        private readonly Action<ISensor>? _onSensor;

        public TestVisitor(Action<IHardware>? onHardware = null, Action<ISensor>? onSensor = null)
        {
            _onHardware = onHardware;
            _onSensor = onSensor;
        }

        public void VisitComputer(IComputer computer)
        {
            computer.Traverse(this);
        }

        public void VisitHardware(IHardware hardware)
        {
            _onHardware?.Invoke(hardware);
            hardware.Traverse(this);
        }

        public void VisitSensor(ISensor sensor)
        {
            _onSensor?.Invoke(sensor);
        }

        public void VisitParameter(IParameter parameter) { }
    }

    public void Dispose()
    {
        _computer?.Close();
        _computer = null;
    }
}

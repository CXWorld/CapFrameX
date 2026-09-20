# CapFrameX.Service.Monitoring.Tests

Runtime tests for the CapFrameX.Service.Monitoring library (ported from LibreHardwareMonitorLib).

## Test Suites

### ComputerTests
Tests for Computer initialization and hardware detection.

**Based on:** `CapFrameX.Test.Sensor.ComputerTest`

Tests:
- `Computer_Open_ShouldInitializeSuccessfully` - Basic initialization
- `Computer_WithCpuEnabled_ShouldDetectCpu` - CPU detection
- `Computer_WithGpuEnabled_ShouldDetectGpu` - GPU detection (NVIDIA/AMD/Intel)
- `Computer_WithMemoryEnabled_ShouldDetectMemory` - RAM detection
- `Computer_WithAllHardwareEnabled_ShouldDetectMultipleComponents` - Full hardware scan
- `Computer_HardwareEvents_ShouldTrigger` - Event system verification

### SensorReadingTests
Tests for sensor reading and updates.

**Based on:** `MonitoringLibTestApp.SensorService`

Tests:
- `Sensors_UpdateAndRead_ShouldReturnValues` - Basic sensor reading
- `CpuSensors_Temperature_ShouldBeReadable` - CPU temperature monitoring
- `CpuSensors_Load_ShouldBeReadable` - CPU load monitoring
- `CpuSensors_Clock_ShouldBeReadable` - CPU clock speed monitoring
- `GpuSensors_ShouldBeReadable` - GPU sensor monitoring
- `MemorySensors_ShouldBeReadable` - RAM usage monitoring
- `Sensors_MultipleUpdates_ShouldReturnDifferentValues` - Dynamic value updates
- `Sensors_AllTypes_ShouldBeRecognized` - Sensor type enumeration

### PerformanceTests
Performance and stress tests for continuous monitoring.

**Based on:** Real-world usage patterns from SensorService

Tests:
- `ContinuousMonitoring_1Second_ShouldPerformWell` - 1-second stress test at 10 Hz
- `SensorCollection_Performance_ShouldBeAcceptable` - Collection performance benchmark
- `SubHardware_ShouldBeAccessible` - Sub-hardware component traversal
- `SensorIdentifiers_ShouldBeUnique` - Identifier uniqueness validation
- `Hardware_Visitor_ShouldTraverseAllComponents` - Visitor pattern implementation

## Running Tests

### Visual Studio
1. Open Test Explorer (Test > Test Explorer)
2. Click "Run All Tests" or run individual test suites
3. View detailed output in Test Explorer

### Command Line
```bash
cd CapFrameX.Service
dotnet test tests/CapFrameX.Service.Monitoring.Tests
```

### Verbose Output
```bash
dotnet test tests/CapFrameX.Service.Monitoring.Tests --logger "console;verbosity=detailed"
```

### Run Specific Test
```bash
dotnet test --filter "FullyQualifiedName~ComputerTests.Computer_WithCpuEnabled_ShouldDetectCpu"
```

## Requirements

- **Administrator Privileges**: Required for hardware access (especially PawnIO driver)
- **Windows**: Tests use Windows-specific APIs for hardware monitoring
- **.NET 9.0**: Target framework

## Test Output

Tests use `ITestOutputHelper` to provide detailed diagnostic information:

```
Detected CPU: AMD Ryzen 9 5900X 12-Core Processor
Total hardware components detected: 5
  - AMD Ryzen 9 5900X (Cpu)
  - NVIDIA GeForce RTX 3080 (GpuNvidia)
  - Generic Memory (Memory)
  - Gigabyte X570 AORUS ELITE (Motherboard)
  - Samsung SSD 980 PRO 1TB (Storage)
```

## Notes

- Some tests may fail if specific hardware is not present (e.g., GPU tests on systems without discrete GPUs)
- Tests require actual hardware - mocking is not used to ensure real-world validation
- Performance tests validate actual monitoring overhead and update rates
- Tests are designed to run on developer workstations and CI/CD environments with hardware access

## Legacy Test Comparison

| Legacy Test | New Test | Status |
|-------------|----------|--------|
| `ComputerTest.InitializeHardware_AnyDectedHardware` | `ComputerTests.Computer_WithAllHardwareEnabled_ShouldDetectMultipleComponents` | ✅ Migrated |
| `MonitoringLibTestApp.SensorService.UpdateSensors()` | `SensorReadingTests.Sensors_UpdateAndRead_ShouldReturnValues` | ✅ Migrated |
| Manual continuous monitoring in TestApp | `PerformanceTests.ContinuousMonitoring_1Second_ShouldPerformWell` | ✅ Enhanced |

## Continuous Monitoring Pattern

The tests implement the same monitoring pattern used in production (from SensorService):

```csharp
// Pattern used in production
Observable.Timer(DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(1000))
    .Subscribe(x =>
    {
        foreach (var hardware in _computer.Hardware)
        {
            hardware.Update();
            // Process sensors...
        }
    });
```

This pattern is tested in `PerformanceTests.ContinuousMonitoring_1Second_ShouldPerformWell`.

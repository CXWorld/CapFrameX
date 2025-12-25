# CapFrameX.Service.Capture.Tests

Comprehensive test suite for the PresentMon capture service.

## Test Structure

### Unit Tests (21 tests)
These tests run quickly without external dependencies and validate core functionality:

#### [CaptureServiceTests.cs](CaptureServiceTests.cs)
- Parameter mapping initialization
- Stream availability and subscription
- Initial state validation
- Process filtering with null/empty filters
- Stop service behavior

#### [ProcessFilteringTests.cs](ProcessFilteringTests.cs)
- Process filtering with exclusion lists
- Case-insensitive filtering
- Consistent results across multiple calls
- Null vs empty filter equivalence
- Thread-safety with concurrent reads (100 iterations per thread)

#### [FrameDataStreamTests.cs](FrameDataStreamTests.cs)
- Observable stream subscription and unsubscribe
- Multiple subscribers receiving data
- Capture mode state changes
- Backpressure handling with sampling
- Concurrent subscriptions
- Parameter mapping validation (8 parameters)
- Index uniqueness verification
- Read-only dictionary enforcement

### Integration Tests (6 tests - skipped by default)
These tests require the TestRenderer application and validate real-world capture:

#### [TestRendererIntegrationTests.cs](TestRendererIntegrationTests.cs)
1. **Frame Data Capture** - Validates PresentMon captures frame timing data from TestRenderer
2. **Process Detection** - Verifies the service detects the TestRenderer process
3. **Process Filtering** - Tests filtering works with real processes
4. **Capture State** - Validates start/stop state transitions
5. **Frame Timing Consistency** - Verifies frame timing quality (FPS > 30, CV < 0.5)
6. **Memory Leak Test** - Long-running capture (30s) with memory growth validation (< 50MB)

## Configuration

### PresentMon Configuration
The tests use the exact PresentMon configuration from the legacy CapFrameX application:

**[PresentMonTestConfiguration.cs](PresentMonTestConfiguration.cs)** provides:
- `CreateRedirectedStartInfo()` - Creates start info matching legacy behavior
- `GetDefaultIgnoreList()` - Returns common blacklisted processes

**PresentMon Arguments** (matches legacy `PresentMonServiceConfiguration`):
```
--restart_as_admin
--stop_existing_session
--output_stdout
--no_track_input
--qpc_time_ms
--track_pc_latency
--exclude dwm.exe
--exclude explorer.exe
--exclude taskmgr.exe
... (+ additional processes from ignore list)
```

### Default Ignore List
Based on the legacy `ProcessList/Processes.json` (341 blacklisted processes):
- System processes: dwm, explorer, taskmgr, svchost, etc.
- Common applications: Discord, Spotify, Chrome, Firefox, Steam, etc.
- Monitoring tools: HWiNFO64, MSIAfterburner, RTSS, OBS64, etc.
- CapFrameX itself

## Running Tests

### Run All Unit Tests
```bash
dotnet test --filter "FullyQualifiedName!~IntegrationTests"
```

### Run Integration Tests (requires TestRenderer)
First, build the TestRenderer:
```bash
dotnet build CapFrameX.Service/tests/CapFrameX.TestRenderer/CapFrameX.TestRenderer.csproj
```

Then run integration tests:
```bash
dotnet test --filter "FullyQualifiedName~IntegrationTests"
```

### Run Specific Test
```bash
dotnet test --filter "FullyQualifiedName~CaptureService_WithTestRenderer_ShouldCaptureFrameData"
```

## Test Requirements

### Unit Tests
- No external dependencies
- No admin rights required
- Run in < 1 second

### Integration Tests
- Requires TestRenderer to be built
- Requires admin rights (PresentMon needs elevation)
- Run in 3-30 seconds per test
- TestRenderer must NOT be in the PresentMon ignore list

## Helper Classes

### [TestHelpers.cs](TestHelpers.cs)
- `CreateCaptureService()` - Creates service instance with NullLogger

### [PresentMonTestConfiguration.cs](PresentMonTestConfiguration.cs)
- `CreateRedirectedStartInfo()` - Creates PresentMon start info with legacy configuration
- `GetDefaultIgnoreList()` - Returns default process blacklist

## Implementation Notes

### Thread-Safety
All integration tests use **thread-safe collections** (`ConcurrentBag<T>`) for data collected from Observable subscriptions:
- `frameDataReceived` - Frame data from `FrameDataStream`
- `captureStates` - State changes from `IsCaptureModeActiveStream`
- `frameTimings` - Timing data extracted from frames

This ensures no race conditions occur when Observable streams emit data on background threads.

### Legacy Compatibility
All integration tests use the **exact same PresentMon configuration** as the legacy CapFrameX application:
- Same command-line arguments
- Same exclude list
- Same redirected output mode
- Same parameter mapping

This ensures the new capture service behaves identically to the legacy implementation.

### TestRenderer Path
The integration tests look for TestRenderer at:
```
..\..\..\..\CapFrameX.TestRenderer\bin\Debug\net10.0-windows\win-x64\CapFrameX.TestRenderer.exe
```

This is relative to the test assembly output directory.

### Parameter Mapping
The service maps PresentMon v2.4.0 CSV columns to named indices:
- ApplicationName (index 0)
- ProcessID (index 1)
- MsBetweenPresents (index 10)
- MsBetweenDisplayChange (index 11)
- MsPCLatency (index 15)
- TimeInSeconds (index 16)
- CpuBusy (index 18)
- GpuBusy (index 22)

Valid lines must have >= 27 columns.

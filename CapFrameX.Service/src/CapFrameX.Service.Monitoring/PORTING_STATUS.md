# CapFrameX.Service.Monitoring - Porting Status

Last updated: 2026-05-21

## Overview

`CapFrameX.Service.Monitoring` is the service-side port of `source/LibreHardwareMonitorLib`.

The current service tree has been synchronized with the latest local legacy library state. A file-level comparison after namespace/resource adaptation reports no missing legacy files in the service project.

## Current Build Status

Build result: success

Verified commands:

```powershell
dotnet build CapFrameX.Service\src\CapFrameX.Service.Monitoring\CapFrameX.Service.Monitoring.csproj
dotnet build CapFrameX.Service\src\CapFrameX.Service.Api\CapFrameX.Service.Api.csproj
```

Remaining warnings are inherited from the legacy-style monitoring code and include:

- Nullability warnings.
- XML documentation warnings.
- Unused interop field/event warnings.
- NuGet package pruning warnings.

There are no current compile errors.

## Completed Porting Work

### Project Structure

- SDK-style .NET 10 project.
- Unsafe code enabled for driver and hardware access.
- Platforms configured for x64, x86, and ARM64.
- `AnyCPU` maps to `x64` to keep CsWin32 generation stable for direct project builds.
- Legacy directory structure retained.

### Namespace Migration

- `LibreHardwareMonitor` namespace mapped to `CapFrameX.Service.Monitoring`.
- Legacy `CapFrameX.Monitoring.Contracts` usage mapped to local service contracts.
- Legacy `CapFrameX.Extensions` usage mapped to local service extensions.

### Internal Service Dependencies

- `Contracts/IProcessService.cs`
- `Contracts/ProcessServiceProvider.cs`
- `Contracts/ISensorConfig.cs`
- `Extensions/ArrayExtensions.cs`

### Latest Legacy Sync

The 2026-05-21 sync transferred the latest local `source/LibreHardwareMonitorLib` state, including:

- ADLX AMD GPU interop.
- Nvidia display handle mapping.
- Hardware simulation support.
- Intel IMC support.
- Intel OC mailbox support.
- Intel OOBMSM wrapper and clocks.
- New PawnIO resource binaries present in the legacy tree.
- Updated package references matching the current legacy monitoring implementation.

### Resources

PawnIO driver files and embedded module resources are copied or embedded by the project file.

`IntelOOBMSM.bin` is intentionally configured as a conditional embedded resource. It is referenced by the OOBMSM wrapper and will be embedded automatically when the binary is added later.

### CsWin32

`NativeMethods.txt` includes the SetupAPI entries required by battery/device enumeration:

- `SetupDiGetClassDevs`
- `SP_DEVICE_INTERFACE_DATA`
- `SP_DEVICE_INTERFACE_DETAIL_DATA_W`

This fixes the earlier missing CsWin32-generated type errors.

## Current Capabilities

- CPU monitoring for AMD and Intel paths inherited from LibreHardwareMonitorLib.
- GPU monitoring for NVIDIA, AMD, and Intel paths.
- Motherboard, SuperIO, embedded controller, voltage, temperature, and fan sensors.
- Memory and SPD reading through RAMSPDToolkit.
- Storage and NVMe health monitoring.
- Battery and network monitoring.
- PSU and controller integrations.
- PawnIO-based privileged hardware access on Windows.

## Platform Boundary

This project is currently Windows-heavy because of PawnIO, Win32 APIs, WMI, vendor APIs, and driver integrations.

For the full service redesign, monitoring should be exposed through a provider boundary:

- The service contracts and API remain cross-platform.
- Windows providers register PawnIO/vendor-backed sensors.
- Linux providers can later register Linux-native sensor sources.
- Unsupported providers report unavailable capabilities instead of preventing service startup.

## Known Gaps

- `IntelOOBMSM.bin` is not yet present in the legacy resource tree. The project is ready for it.
- Warnings should be reduced over time, but they are not blocking the current port.
- Runtime validation is still needed on representative Intel, AMD, NVIDIA, and mobile/battery systems.
- Linux behavior needs a provider split before this project should be loaded as-is on Linux.

# CapFrameX.Service.Monitoring - Porting Status

## Overview
Successfully ported LibreHardwareMonitorLib from `source/LibreHardwareMonitorLib` to `CapFrameX.Service/src/CapFrameX.Service.Monitoring`.

## Completed Steps

### 1. Project Structure ✅
- Created new .NET 9.0 project with SDK-style csproj
- Copied all 176 C# source files
- Copied all binary resources (PawnIO drivers, embedded firmware)
- Maintained directory structure

### 2. Namespace Migration ✅
- Updated all 176 files from `LibreHardwareMonitor` to `CapFrameX.Service.Monitoring`
- Updated 408+ namespace references across the codebase
- All namespace declarations, using statements, and XML documentation updated

### 3. Dependencies ✅
- Migrated to .NET 9.0 (from multi-targeting net472/netstandard2.0/net8.0/net9.0)
- Configured NuGet packages:
  - HidSharp 2.6.3
  - Microsoft.Windows.CsWin32 0.3.205
  - RAMSPDToolkit-NDD 1.3.2
  - Serilog 2.9.0
  - System.Management 9.0.9
  - System.Reactive 4.3.2
  - And other required packages

### 4. Internal Dependencies ✅
- Created `Contracts/IProcessService.cs` and `ProcessServiceProvider.cs` (previously from CapFrameX.Monitoring.Contracts)
- Created `Extensions/ArrayExtensions.cs` with `IsNullOrEmpty` extension method (previously from CapFrameX.Extensions)

### 5. Project Integration ✅
- Added to CapFrameX.Service.sln
- Configured for x64, x86, and ARM64 platforms
- Enabled unsafe code blocks (required for driver access)
- Enabled nullable reference types
- Enabled implicit usings

## Current Build Status

**Build Result:** FAILED (8 errors, 466 warnings)

### Remaining Errors (8 total)

All errors are related to CsWin32-generated Windows API bindings in `Hardware/Battery/BatteryGroup.cs`:

1. Missing type: `SP_DEVICE_INTERFACE_DATA` (3 occurrences)
2. Missing type: `SP_DEVICE_INTERFACE_DETAIL_DATA_W` (4 occurrences)
3. Missing method: `PInvoke.SetupDiEnumDeviceInterfaces` (2 occurrences)
4. Missing method: `PInvoke.SetupDiGetDeviceInterfaceDetail` (3 occurrences)

### Root Cause

The Microsoft.Windows.CsWin32 package generates P/Invoke declarations based on `NativeMethods.txt`. The required Setup API types and methods were added to NativeMethods.txt but are not being generated.

**Possible reasons:**
1. CsWin32 0.3.205 may not have metadata for these older SetupAPI functions
2. The API names may need to be in a different format
3. These APIs may require additional configuration or different package version

### Warnings (466 total)

Mostly benign:
- CS0649: Field never assigned (in P/Invoke structures - expected)
- CS8625: Null literal to non-nullable reference
- CS8765/CS8767: Nullability mismatches
- NU1510: Unnecessary package references
- PInvoke005: Platform-specific APIs

## Project Features

### Hardware Monitoring Capabilities
- **CPU**: AMD (all families), Intel (all generations), load monitoring
- **GPU**: NVIDIA (NvAPI, NVML), AMD (ADL), Intel (iGPU)
- **Motherboard**: SuperIO chips, embedded controllers, voltage/temp/fan sensors
- **Memory**: RAM usage, SPD reading via RAMSPDToolkit
- **Storage**: HDD/SSD S.M.A.R.T., NVMe health monitoring
- **Battery**: Laptop battery monitoring (currently has build errors)
- **Network**: Network interface monitoring
- **PSU**: Corsair and MSI power supplies
- **Controllers**: AeroCool, AquaComputer, Heatmaster, NZXT, Razer, TBalancer

### Special Features
- PawnIO kernel driver for privileged hardware access
- Multi-vendor GPU support
- CPU-specific MSR reading
- Ryzen SMU access
- Thread affinity management
- Reactive programming support (System.Reactive)
- Comprehensive logging (Serilog)

## Next Steps

### Option 1: Fix CsWin32 Generation
1. Update Microsoft.Windows.CsWin32 to latest version
2. Research correct API names for SetupAPI functions
3. Consider using Win32MetadataLookup to verify API availability

### Option 2: Manual P/Invoke Declarations
Create manual P/Invoke declarations for the missing SetupAPI functions in a separate file:
- `SP_DEVICE_INTERFACE_DATA` struct
- `SP_DEVICE_INTERFACE_DETAIL_DATA_W` struct
- `SetupDiEnumDeviceInterfaces` function
- `SetupDiGetDeviceInterfaceDetail` function

### Option 3: Conditional Compilation
Temporarily disable Battery monitoring with `#if` directives until Windows API bindings are resolved.

## File Structure

```
CapFrameX.Service.Monitoring/
├── Contracts/
│   ├── IProcessService.cs
│   └── ProcessServiceProvider.cs
├── Extensions/
│   └── ArrayExtensions.cs
├── Hardware/
│   ├── Battery/          (has build errors)
│   ├── Cpu/              ✅
│   ├── Gpu/              ✅
│   ├── Memory/           ✅
│   ├── Motherboard/      ✅
│   ├── Network/          ✅
│   ├── Psu/              ✅
│   ├── Storage/          ✅
│   └── Controller/       ✅
├── Interop/              ✅
├── PawnIo/               ✅
├── Software/             ✅
├── Resources/            ✅
└── CapFrameX.Service.Monitoring.csproj
```

## Recommendation

**Immediate:** Use Option 2 (Manual P/Invoke) to complete the port quickly. Battery monitoring is a small part of the library and can use traditional P/Invoke declarations.

**Long-term:** Investigate CsWin32 version compatibility and metadata availability for better maintainability.

# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

CapFrameX is a Windows desktop application for frametime capture and analysis, built on Intel's PresentMon. It provides an overlay via Rivatuner Statistics Server (RTSS) and is used for gaming performance benchmarking.

## Build Commands

### Prerequisites
- Visual Studio 2022
- WiX Toolset v3.14.1 with VS 2022 Extension
- C++ MFC build tools

### Build the Main Application
```bash
nuget restore CapFrameX.sln
msbuild source\CapFrameX\CapFrameX.csproj /p:Configuration=Release /p:Platform=x64 /p:VisualStudioVersion=17.0
```

### Build Native C++ Components (required for full functionality)
```bash
msbuild source\CapFrameX.Hwinfo\CapFrameX.Hwinfo.vcxproj /p:SolutionDir=%CD%\ /p:Configuration=Release /p:Platform=x64 /p:VisualStudioVersion=17.0
msbuild source\CapFrameX.IGCL\CapFrameX.IGCL.vcxproj /p:SolutionDir=%CD%\ /p:Configuration=Release /p:Platform=x64 /p:VisualStudioVersion=17.0
msbuild source\CapFrameX.ADLX\CapFrameX.ADLX.vcxproj /p:SolutionDir=%CD%\ /p:Configuration=Release /p:Platform=x64 /p:VisualStudioVersion=17.0
```

### Build Installer
```bash
msbuild source\CapFrameXInstaller\CapFrameXInstaller.wixproj /p:SolutionDir=%CD%\ /p:Configuration=Release /p:Platform=x64
msbuild source\CapFrameXBootstrapper\CapFrameXBootstrapper.wixproj /p:SolutionDir=%CD%\ /p:Configuration=Release /p:Platform=x64
```

### Run Tests
Tests use MSTest framework:
```bash
vstest.console source\CapFrameX.Test\bin\x64\Release\net9.0-windows\CapFrameX.Test.dll /Platform:x64
```

## Architecture

### Solution Structure
The solution (`CapFrameX.sln`) contains ~40 projects mixing C# (.NET Framework 4.7.2 / .NET Standard / .NET Core 3.1) and C++ native code.

### Layer Organization

**UI Layer (WPF + MVVM)**
- `CapFrameX` - Main shell application, entry point, DI container setup (DryIoc)
- `CapFrameX.View` - XAML views and UI controls
- `CapFrameX.ViewModel` - ViewModels for all views (30+ view models)
- `CapFrameX.MVVM` - MVVM infrastructure and base classes

**Core Services**
- `CapFrameX.PresentMonInterface` - Wrapper around Intel PresentMon for frametime capture
- `CapFrameX.Capture.Contracts` - Capture service interfaces
- `CapFrameX.Statistics.NetStandard` - Statistical calculations (percentiles, averages)
- `CapFrameX.Statistics.PlotBuilder` - Chart data generation
- `CapFrameX.Overlay` - Overlay management
- `CapFrameX.Sensor` / `CapFrameX.Sensor.Reporting` - Hardware sensor data collection

**Data Layer**
- `CapFrameX.Data` - File I/O, record management (JSON/CSV capture files)
- `CapFrameX.Data.Session` - Session state management
- `CapFrameX.Configuration` - AppSettings.json handling
- `CapFrameX.Contracts` - Interface definitions

**Native Interop (C++ DLLs)**
- `CapFrameX.RTSSInterface` - Rivatuner Statistics Server integration
- `CapFrameX.Hwinfo` - HWInfo64 sensor integration
- `CapFrameX.IGCL` - Intel Graphics Control Library
- `CapFrameX.ADLX` - AMD Display Library

**Webservice (ASP.NET Core 3.1)**
- `CapFrameX.Webservice.Host` - API host
- `CapFrameX.Webservice.Implementation` - Business logic
- `CapFrameX.Webservice.Data` - Entity Framework Core models
- `CapFrameX.Webservice.Persistance` - Data persistence

**Charting**
- `CapFrameX.Charts/Core40` - Core charting engine
- `CapFrameX.Charts/OxyPlot` - OxyPlot library
- `CapFrameX.Charts/WpfView` - WPF chart controls

### Key Dependencies
- Prism 7.0 (MVVM framework)
- DryIoc (IoC container)
- MahApps.Metro + MaterialDesign (UI styling)
- System.Reactive (Rx)
- OxyPlot (charting)
- Serilog (logging)

### Build Output
- Platform: x64
- Target framework (1.9+): `net9.0-windows` (SDK-style projects; legacy 1.8.x was .NET Framework 4.7.2)
- Main output: `source\CapFrameX\bin\x64\Release\net9.0-windows\`
- Installer output: `source\CapFrameXBootstrapper\bin\x64\Release\CapFrameXBootstrapper.exe`

## Hook-free OSD (external source)

The hook-free OSD lives in the **private** repo [CXWorld/CapFrameX.OSD](https://github.com/CXWorld/CapFrameX.OSD) (local checkout: `E:\Code\CapFrameX.OSD`), consumed as an *optional* git submodule at `external/CapFrameX.OSD` with prebuilt-binary fallback in `external/CapFrameX.OSD-prebuilt/` — the public repo builds either way:

- With the submodule checked out, `CapFrameX.csproj` and `CapFrameX.OSD.Integration.csproj` set `CfxOsdFromSource=true` and build `CapFrameX.OSD.Interop` from source; without it they reference the prebuilt DLLs. Force the fallback with `/p:CfxOsdFromSource=false`.
- The native renderer `cfx_osd_core.dll` is staged from the submodule's CMake output if built (`external/CapFrameX.OSD/CapFrameX.OSD/build/bin/RelWithDebInfo`), else from the prebuilt folder.
- `source/CapFrameX.OSD.Integration` (adapter mapping `IOverlayEntry` onto the OSD, references `CapFrameX.Contracts`) intentionally stays in this repo; everything CapFrameX-independent (native core, Interop, WPF editor controls) lives in the OSD repo.
- After OSD changes: update the DLLs in `external/CapFrameX.OSD-prebuilt/` (see its README) and bump the submodule commit.

## Configuration Files
- User settings: `%appdata%/CapFrameX/Configuration/AppSettings.json`
- Overlay config: `%appdata%/CapFrameX/Configuration/OverlayEntryConfiguration_(0/1/2).json`
- Version: `version/Version.txt`

## NuGet Package Issues
If package conflicts occur, run in Package Manager Console:
```
Update-Package -reinstall
```

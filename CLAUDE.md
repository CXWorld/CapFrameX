# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

CapFrameX is a Windows desktop application for frametime capture and analysis, built on Intel's PresentMon. It provides an overlay via Rivatuner Statistics Server (RTSS) and is used for gaming performance benchmarking.

## Build Commands

### Prerequisites
- Visual Studio 2022
- WiX Toolset v3.14.1 with VS 2022 Extension
- C++ MFC build tools
- FrameView SDK (installer at `installers/FVSDKSetup.exe`)

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
msbuild source\CapFrameX.FrameView\CapFrameX.FrameView.vcxproj /p:SolutionDir=%CD%\ /p:Configuration=Release /p:Platform=x64 /p:VisualStudioVersion=17.0
```

### Build Installer
```bash
msbuild source\CapFrameXInstaller\CapFrameXInstaller.wixproj /p:SolutionDir=%CD%\ /p:Configuration=Release /p:Platform=x64
msbuild source\CapFrameXBootstrapper\CapFrameXBootstrapper.wixproj /p:SolutionDir=%CD%\ /p:Configuration=Release /p:Platform=x64
```

### Run Tests
Tests use MSTest framework:
```bash
vstest.console source\CapFrameX.Test\bin\x64\Release\CapFrameX.Test.dll
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
- `CapFrameX.FrameView` - Intel FrameView SDK

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
- Platform: x64 (primary), x86 supported
- Main output: `source\CapFrameX\bin\x64\Release\`
- Installer output: `source\CapFrameXBootstrapper\bin\x64\Release\CapFrameXBootstrapper.exe`

## Configuration Files
- User settings: `%appdata%/CapFrameX/Configuration/AppSettings.json`
- Overlay config: `%appdata%/CapFrameX/Configuration/OverlayEntryConfiguration_(0/1/2).json`
- Version: `version/Version.txt`

## NuGet Package Issues
If package conflicts occur, run in Package Manager Console:
```
Update-Package -reinstall
```

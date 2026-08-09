# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

CapFrameX is a Windows desktop application for frametime capture and analysis, built on Intel's PresentMon. It provides an overlay via Rivatuner Statistics Server (RTSS) and is used for gaming performance benchmarking.

## Build Commands

### Prerequisites
- Visual Studio 2022
- WiX Toolset v3.14.1 ([wix314.exe](https://github.com/wixtoolset/wix3/releases/tag/wix3141rtm)) — **v3 only**, v4+ uses an incompatible SDK-style project format. It sets the `WIX` environment variable that the installer's `heat.exe` pre-build step needs, and installs the targets under `Program Files (x86)\MSBuild\Microsoft\WiX\v3.x\`. The separate VS 2022 extension only adds IDE integration and is not required for the msbuild command line.
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

This skips the `Integration` category, which drives the **real** PresentMon capture service and
`CaptureManager` against `vkcube.exe` (staged from `source\CapFrameX.Overlay\3d-test-app\` into the
app output). Those tests report themselves as *skipped* — not failed — whenever their prerequisites
are missing, so a green run is no evidence that they executed. Run them explicitly, **from an
elevated shell**:
```bash
vstest.console source\CapFrameX.Test\bin\x64\Release\net9.0-windows\CapFrameX.Test.dll /Platform:x64 /TestCaseFilter:TestCategory=Integration
```
Prerequisites, each of which turns into a skip instead of a failure:
- administrator privileges (ETW),
- the main application built, so `PresentMon\` and `3d-test-app\` exist in its output directory,
- a Vulkan-capable GPU — the tests abort if vkcube exits immediately.

The run takes about a minute, opens vkcube windows and starts PresentMon. Frame-time assertions
tolerate occasional stalls: vkcube shares the desktop, and a window losing the foreground or being
occluded stalls its presents.

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
- `CapFrameX.Webservice.Data` - DTOs, commands and queries; `netstandard2.0`, and the only
  webservice project the desktop app references (through `CapFrameX.ViewModel`)

Data is served from Squidex (`SquidexService`), not from a database — the Entity Framework
persistence layer was dropped. netcoreapp3.1 is out of support; the packages warn about it, which
`SuppressTfmSupportBuildWarnings` silences in Host and Implementation.

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

### Vulkan titles: implicit layer instead of injection

Vulkan games present through the driver's ICD, so the DXGI Present hook never fires. They are served by an implicit loader layer (`VK_LAYER_CAPFRAMEX_overlay`) staged one folder per bitness — `vulkan\cfx_osd_vklayer.dll` and `vulkan\x86\cfx_osd_vklayer.dll`, mirroring the hook's `hook\` / `hook\x86\` split. Both manifests are byte-identical; `library_path` inside them is relative, so only the folder decides which DLL the loader picks up. The x86 layer comes from its own CMake tree in the OSD repo (`vk_layer`, `cmake -B build-x86 -A Win32`).

Registration is bitness-scoped and this is load-bearing: the loader identifies a layer by the NAME in its manifest, so a manifest reachable by processes that cannot load its DLL **shadows** the correct registration and disables the layer for that bitness. `CapFrameXInstaller` therefore ships one component per bitness — `Win64="yes"` for the 64-bit view, `Win64="no"` for `WOW6432Node`. The 32-bit one is anchored to `TARGETDIR`, not `INSTALLFOLDER`, because ICE80 rejects a 32-bit component in a directory below `ProgramFiles64Folder`.

**Never register the layer in HKCU** (the OSD repo's `register_layer.cmd` uses HKLM only and purges HKCU leftovers). HKCU is user-controlled, so the loader ignores it for targets started elevated — e.g. a game launched from a Visual Studio that debugs the admin-only `CapFrameX.exe` — and it is not split by bitness, so it also triggers the shadowing above. Both failure modes are silent and look exactly like "this game has no Vulkan": `VulkanActivityProbe` finds no renderer-state mapping, `VulkanLayerModuleProbe` finds no layer module, and `HookOverlayManager` correctly concludes DXGI and injects the hook into a Vulkan title — where the in-game arbiter denies its renderer lease and the status parks on `Initializing` until the hook-free fallback takes over.

## Update service

`IUpdateService` (`CapFrameX.Updater/UpdateService.cs`) fetches a JSON manifest from the CapFrameX
update server, compares it against the running assembly version and, once the user confirms,
downloads the installer package into the updates folder. The manifest URI comes from the
`UpdateManifestUri` key in `App.config`; **while it is empty the whole feature stays inert** and no
update UI appears. The wire format is documented by `CapFrameX.Updater/update-manifest.sample.json`.

Installing happens on the *next* app start, not at download time, because the installer replaces the
files of the running app. `UpdateInstaller.TryStartPendingUpdate` therefore runs in `App.OnStartup`
**before the bootstrapper builds the container** — it reads the `pending-update.json` marker,
re-verifies the package against its SHA-256, starts it and returns true, at which point `App` sets
`_skipShutdownSequence` (nothing has been started yet, so the shutdown sequence in `ApplicationExit`
would only throw) and exits. The marker is deleted *before* the installer is launched so a failing
or cancelled install cannot loop forever; with no marker present, leftover packages are deleted.
A package is only ever executed when it is an `.exe`/`.msi` and its name resolves inside the updates
folder — the manifest is remote input.

Version comparison is normalized to `Major.Minor.Build`: assembly versions carry a fourth component
that manifests do not, and `Version` sorts an unset component below zero, so `1.9.1` would otherwise
look older than `1.9.1.0`.

One shared `UpdateViewModel` singleton backs all three surfaces: the indicator on the right of the
status bar (`StateView`), the UPDATE tab in the options popup (`ColorbarView`), and the embedded
dialog. The dialog's `DialogHost` sits in `Shell.xaml`, not in the options popup — a `DialogHost`
nested inside a WPF `Popup` does not reliably render its overlay (same caveat as `ControlView.xaml`).

## Text field styles

`CapFrameX.View/Styles/CxTextFields.xaml` (merged in `App.xaml`) replaces MDIX's implicit `TextBox`
and `ComboBox` styles with `CxTextBox` / `CxFloatingHintTextBox` / `CxComboBox`. They differ from
the MDIX originals by `VerticalContentAlignment="Bottom"` only, which anchors the text to the
underline instead of centring it in the box — without it every field that is forced taller than its
natural height (the settings UI pins all of them to 40-45px) drifts off its baseline, TextBox and
ComboBox by different amounts. **Use the `Cx*` keys, not the `MaterialDesign*` ones**, whenever a
view sets a text field style explicitly or bases a local style on one; anything that just relies on
the implicit style is already covered.

## Configuration Files
- User settings: `%appdata%/CapFrameX/Configuration/AppSettings.json`
- Overlay config: `%appdata%/CapFrameX/Configuration/OverlayEntryConfiguration_(0/1/2).json`
- Staged update packages: `%appdata%/CapFrameX/Updates` (portable mode: `<appdir>/Updates`)
- Build version: `version/Version.txt`
- Release channel (`release` or `beta`): `version/Channel.txt`

`version/Version.props` validates both values and generates the executable's version and
release-channel assembly metadata. The update UI uses only the catalog configured through
`UpdateCatalogUri`; the former GitHub `Version.txt` web check has been removed.

Versions have four significant components (`major.minor.patch.build`). The update catalog v2
groups every package into the Release or Beta channel and preserves the fourth component during
comparison, rollback and display. The MSI project maps patch/build monotonically into MSI's
three-component ProductVersion while the app and bootstrapper retain the full version.

## NuGet Package Issues
If package conflicts occur, run in Package Manager Console:
```
Update-Package -reinstall
```

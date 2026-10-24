# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

CapFrameX is a Windows desktop application for frametime capture and analysis, built on Intel's PresentMon. It provides an overlay via Rivatuner Statistics Server (RTSS) and is used for gaming performance benchmarking.

## Build Commands

### Prerequisites
- Visual Studio 2026 (toolset v145; the native projects pin it)
- WiX Toolset v6.0.2 — nothing to install. `CapFrameXInstaller` and `CapFrameXBootstrapper` are SDK-style projects (`<Project Sdk="WixToolset.Sdk/6.0.2">`); MSBuild restores the SDK and the `WixToolset.*.wixext` extensions from NuGet, so build them with `/restore`. The extension versions live in `source/Directory.Packages.props` and must match the SDK version. Stay on v6: WiX v7 requires accepting the Open Source Maintenance Fee EULA before it builds, which is the owner's decision. The HeatWave Visual Studio extension only adds IDE integration.
- C++ MFC build tools

### Build the Main Application
```bash
nuget restore CapFrameX.sln
msbuild source\CapFrameX\CapFrameX.csproj /p:Configuration=Release /p:Platform=x64 /p:VisualStudioVersion=18.0
```

### Build Native C++ Components (required for full functionality)
```bash
msbuild source\CapFrameX.Hwinfo\CapFrameX.Hwinfo.vcxproj /p:Configuration=Release /p:Platform=x64 /p:VisualStudioVersion=18.0
msbuild source\CapFrameX.IGCL\CapFrameX.IGCL.vcxproj /p:Configuration=Release /p:Platform=x64 /p:VisualStudioVersion=18.0
msbuild source\CapFrameX.ADLX\CapFrameX.ADLX.vcxproj /p:Configuration=Release /p:Platform=x64 /p:VisualStudioVersion=18.0
```
Build them before the application. They are reached through P/Invoke only, so they are solution
build dependencies, not project references, and they build into their own
`source\<project>\x64\<Configuration>\`. The `StageCapFrameXNativeRuntime` target in
`CapFrameX.csproj` copies the three DLLs from there next to `CapFrameX.exe` and logs every DLL it
cannot find; a missing one does not fail the build, the sensor groups just fall back silently.
`CapFrameX.RTSSInterface` (C++/CLI) is an ordinary project reference and needs no staging.

### Output layout
Every project builds into its own `bin\x64\<Configuration>\<tfm>\` (`obj\` beside it); the native
projects into `x64\<Configuration>\`. There is no shared output folder: `source\bin` and the flat
`source\CapFrameX\bin\x64\<Configuration>\` were the former LibreHardwareMonitorLib and .NET
Framework layouts. Folders for other target frameworks (`net9.0`, `netstandard2.0`, `net472`, ...)
are leftovers of earlier builds and can be deleted.

### Build Installer
```bash
msbuild source\CapFrameXInstaller\CapFrameXInstaller.wixproj /restore /p:SolutionDir=%CD%\ /p:Configuration=Release /p:Platform=x64
msbuild source\CapFrameXBootstrapper\CapFrameXBootstrapper.wixproj /restore /p:SolutionDir=%CD%\ /p:Configuration=Release /p:Platform=x64
```
Build the application (and the native DLLs) first: the MSI's file list is harvested at build time
from `source\CapFrameX\bin\x64\<Configuration>\net10.0-windows` by the `<Files>` element in
`Product.wxs`, whose `<Exclude>` entries replace the former `heat.exe` step and `filter.xslt`.
The bundle stays a 32-bit Burn bundle (`InstallerPlatform=x86`), as WiX v3 bundles were; the chained
MSI is x64. Its VC++ redistributables are remote payloads verified by their Authenticode signer.
The bundle also builds `CapFrameXBootstrapper.BAFunctions` (native, always Win32, packages restored by
`/restore` or `nuget restore`): a BAFunctions DLL for WixStdBA that plans a newer installed CapFrameX
bundle as an upgrade instead of a downgrade. Without it Burn refuses to run an older setup
(`0x80070666`) although the MSI allows downgrades, which also blocks the in-app rollback. Only setups
that contain the DLL can downgrade; releases built before it still refuse.

### Run Tests
Tests use MSTest framework:
```bash
vstest.console source\CapFrameX.Test\bin\x64\Release\net10.0-windows\CapFrameX.Test.dll /Platform:x64
```

This skips the `Integration` category, which drives the **real** PresentMon capture service and
`CaptureManager` against `vkcube.exe` (staged from `source\CapFrameX.Overlay\3d-test-app\` into the
app output). Those tests report themselves as *skipped* — not failed — whenever their prerequisites
are missing, so a green run is no evidence that they executed. Run them explicitly, **from an
elevated shell**:
```bash
vstest.console source\CapFrameX.Test\bin\x64\Release\net10.0-windows\CapFrameX.Test.dll /Platform:x64 /TestCaseFilter:TestCategory=Integration
```
Prerequisites, each of which turns into a skip instead of a failure:
- administrator privileges (ETW),
- the main application built, so `PresentMon\` and `3d-test-app\` exist in its output directory,
- a Vulkan-capable GPU — the tests abort if vkcube exits immediately.

The run takes about a minute, opens vkcube windows and starts PresentMon. Frame-time assertions
tolerate occasional stalls: vkcube shares the desktop, and a window losing the foreground or being
occluded stalls its presents.

### Measure CPU load
`scripts\Measure-CpuLoad.ps1` (elevated, no CapFrameX running) measures the CPU time and cycles of
CapFrameX.exe and its PresentMon process without capturing: `Idle`, `PresentMon` (game running,
renderer `Off`), and per renderer `-Hidden`, `-NoSensors`, `-Sensors` (optionally `-Design`). It runs
the app output in a portable sandbox under `temp\cpu-load`, one fresh start per scenario with vkcube
as target, primes the configuration once (the `NoSensors` overlay profile is saved by the app through
MCP), breaks CapFrameX's CPU down by thread name and compares with `-BaselineReport`. Keep it in step
with the settings it writes (`OverlayRenderer` and its mirror flags, `IsOverlayActive`,
`ActiveOverlayDesignProfileId`) and the endpoints it reads (`/api/version`, `/api/processes`,
`/api/metrics`, MCP `cfx_get_overlay_entries`/`cfx_set_overlay_entry`). Details: `scripts\CPU_LOAD_TEST.md`.

## Architecture

### Solution Structure
The solution (`CapFrameX.sln`) contains ~40 projects mixing C# (`net10.0` / `net10.0-windows`,
plus one .NET Framework 4.7.2 installer-action project) and C++ native code.

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

**Webservice (ASP.NET Core, `net10.0`)**
- `CapFrameX.Webservice.Host` - API host
- `CapFrameX.Webservice.Implementation` - Business logic
- `CapFrameX.Webservice.Data` - DTOs, commands and queries; `net10.0`, and the only
  webservice project the desktop app references (through `CapFrameX.ViewModel`)

Data is served from Squidex (`SquidexService`), not from a database — the Entity Framework
persistence layer was dropped.

**Charting**
- `CapFrameX.Charts/Core40` - Vendored LiveCharts core
- `CapFrameX.Charts/WpfView` - Vendored LiveCharts WPF controls
- OxyPlot is consumed through NuGet packages rather than a repository project

### Key Dependencies
- Prism 9.x (MVVM framework)
- DryIoc (IoC container)
- MahApps.Metro + MaterialDesign (UI styling)
- System.Reactive (Rx)
- OxyPlot 2.2 NuGet packages (charting)
- Serilog (logging)

### Build Output
- Platform: x64
- Target framework (1.9+): `net10.0-windows` (SDK-style projects; legacy 1.8.x was .NET Framework 4.7.2)
- Main output: `source\CapFrameX\bin\x64\Release\net10.0-windows\`
- Installer output: `source\CapFrameXBootstrapper\bin\x64\Release\CapFrameXBootstrapper.exe`

## Hook-free OSD (external source)

The hook-free OSD lives in the **private** repo [CXWorld/CapFrameX.OSD](https://github.com/CXWorld/CapFrameX.OSD), checked out separately at `..\CapFrameX.OSD`, a sibling of this repository. Prebuilt binaries in `external/CapFrameX.OSD-prebuilt/` let the public repo build without private sources:

- `source/Directory.Build.props` detects the sibling checkout and selects source builds for Interop and Controls. Override its location with `CfxOsdSourceDir`; the legacy `CfxOsdSubmoduleDir` override remains supported. Without source, builds reference the prebuilt DLLs. Force the fallback with `/p:CfxOsdFromSource=false`.
- The native renderer `cfx_osd_core.dll` is staged from the selected checkout's CMake output if built (`../CapFrameX.OSD/CapFrameX.OSD/build/bin/RelWithDebInfo`), else from the prebuilt folder. Explicit prebuilt mode also applies to hooks and Vulkan payloads.
- `source/CapFrameX.OSD.Integration` (adapter mapping `IOverlayEntry` onto the OSD, references `CapFrameX.Contracts`) intentionally stays in this repo; everything CapFrameX-independent (native core, Interop, WPF editor controls) lives in the OSD repo.
- After OSD changes: update the DLLs in `external/CapFrameX.OSD-prebuilt/` (see its README) and record their source revision and hashes there.

### Hook-free visibility

The hook-free overlay is only visible while the capture process list has at least one entry.
`CaptureViewModel` publishes the list's size through `IProcessService.ProcessCountStream` — the PID
stream is no substitute, it stays 0 while several processes wait for a selection — and
`OsdOverlayBridge` folds it into its visibility (`_active && _hasProcesses && exist`). An empty list
is a soft-hide like any other, so the renderer keeps existing. The overlay hotkey is untouched: it
toggles `IsOverlayActive`, which hides a visible overlay at any time but cannot show one past the
gate; the toggled state takes effect once a process is detected. RTSS and the in-game hook are not
affected.

### Remote API and the overlay hotkey

`/api/osd` and `/ws/osd` serve `IOverlayService.CurrentOverlayEntries`, and they keep working with
the overlay switched off (ALT+O). `IRemoteOverlayDemand` tracks the API clients: an HTTP read
holds a 30 s lease, a `/ws/osd` connection holds the demand until it disconnects. While it is
active, `OverlayService` runs the entry feed in `EntryFeedMode.RemoteOnly`: RTSS is released
and never fed, and the hook-free bridge and the hook metrics publisher ignore the updates. The
sensor poller treats it like an active overlay, so the hardware values in the API stay current.
The renderers follow `IsOverlayActive` alone. "Hide OSD on RTSS" (`HideOverlay`) is unrelated:
it keeps the overlay *on* and only suppresses the RTSS output.

### Overlay renderer selection

`IAppConfiguration.OverlayRenderer` (`EOverlayRenderer`: `Rtss`, `InGame`, `HookFree`, `Off`) is the
stored selection. `EnableHookOverlay` and `EnableHookFreeOverlay` are derived from it and stored as its
mirror, so builds that only know the flags still read a usable choice; `Off` mirrors to the flags of
RTSS. Writing a flag selects a renderer (`OverlayRendererSelection.WithHookOverlay` / `WithHookFreeOverlay`).
A switch raises `OverlayRenderer` first and then each changed flag, all after the new selection is
stored. At load, a stored renderer is kept unless a stored flag contradicts it: then an older build
changed the selection (after a rollback), and the flags win.

`Off` clears both flags, so `HookOverlayManager` (no process-start watcher, no early-injection probe,
no injection), the metrics and design publishers (no shared memory, so the Vulkan layer stays passive)
and `OsdOverlayBridge` stay idle. `OverlayService` neither initializes nor feeds RTSS, switches the
overlay off on the switch to `Off`, and keeps `PresentMonOverlayMetrics` running for the remote API, as
for the CapFrameX renderers. `OverlayActivation` refuses every activation and the toggles are disabled.
Ask `UsesRtss()` whether RTSS renders, never "both flags false". Tests that mock `IAppConfiguration`
and switch renderers call `WireOverlayRenderer` (`CapFrameX.Test/Mocks`) after `SetupAllProperties`.

### Graphics API label (`<APP>`)

RTSS resolves its `<APP>` macro itself. For the CapFrameX renderers and the remote API,
`PresentMonOverlayMetrics.ApplySnapshot` replaces it in copies of the entries, in this order: the
API the in-game renderer proved for the target (`HookOverlayStatus.RenderApi`: `D3D11`/`D3D12` from
the hook's device type, `Vulkan` from the loaded layer), the PresentMon `PresentRuntime` (`DXGI`,
`D3D9`, `Other`), then "Performance". The hook-free renderer only has PresentMon. The proven API is
bound to the target PID and kept through statuses without one (hidden, idle, transitional): every
label change renames the group and makes the renderer rebuild its scene. A `Disabled` status (another
renderer selected) and a PID change drop it. API names follow RTSS's spelling (`D3D11`, `D3D12`,
`Vulkan`, `OpenGL`, ...), also in `RTSSCoreControl.cpp` (`IRTSSService.GetApiInfo`): both sources feed a
capture's `ApiInfo` and the `GraphicsAPI` overlay entry, so they must spell alike.

### OLED pixel orbit

`OsdPixelOrbit` (`CapFrameX.Contracts/Configuration`) moves the CapFrameX renderers by one pixel per
interval along a serpentine walk over a (radius + 1)² square. Offsets are never negative, so the
configured margins stay the minimum distance to the screen edge. A design requests the orbit with a
top-level `pixelShift` object (`intervalSeconds` 10..3600, `radius` 1..32). The OSD editor
round-trips and edits it (Layout tab). `OverlayRuntimeDesign` reads it into `PixelShift` and removes
it from `TemplateJson`, so the renderers never see it. The global `OsdPixelOrbit*` options cover every
other design and the classic rows. The active design's request wins (`OverlayPixelOrbitResolver`).

Both placement paths add the offset on every OSD tick: `HookMetricsPublisher` for the hook and Vulkan
layer placement channel, `OsdOverlayBridge` for the hook-free window. Both publish only on change, so
a step is one move per interval, never an animation. Both read `Environment.TickCount64`, so they always
agree on the position. RTSS is not affected. The editor's import is a strict round trip: a Controls
build without this change rejects any design that contains `pixelShift`, so the prebuilt Controls DLL
must include it. Example design: `overlay-templates/OLED-Minimal.json`.

### Resolution-relative sizing

`IAppConfiguration.OsdScaleWithResolution` ("Scale with resolution", Overlay tab, default off) makes
the CapFrameX renderers keep a design's share of the screen: the native core builds the scene with
`OsdZoom` × target height / 1080 and scales the anchor margins by the same factor (whole percent,
0.5..3.0). Only the height counts; a wider aspect ratio just adds room beside the anchored panel. The
target is the game's back buffer for the in-game hook and the Vulkan layer, and the monitor for the
hook-free window. The hook and the layer receive the switch as metrics header bit5
(`HookMetricsChannel.FlagResolutionScale`, composed in `HookMetricsPublisher.BuildHeaderFlags`) and
measure the height themselves; `OsdOverlayBridge` passes `OsdHost.DesignReferenceHeight` through
`OsdHost.SetReferenceHeight`, which survives a renderer restart. The pixel orbit offset is part of
the margins, so its steps scale too. RTSS is not affected. Details: the OSD repo's CLAUDE.md.

### Overlay fonts

Two sources, RTSS untouched. A tile design carries one font: the design font
(`OsdDesignDocument.FontFamily`, the editor's Style tab, `fontFamily` in the MCP tile document).
Text tiles have no font of their own; `Validate` rejects a `family` on them (`Editor_TileFontRemoved`),
only an icon names its glyph font. `FromJson` reads designs whose tiles drew fonts of their own as
saved and then gives the design the font most text tiles drew (a tie keeps the design font,
`UnifyTileFonts`); the GREENLINE, ICELINE and Turquoise Reference starters were regenerated so
(catalog version 22).
The classic layout takes `IAppConfiguration.OsdFontFamily` ("Overlay font", Overlay tab, default
Segoe UI, MCP `fontFamily` option); it never overrides a design. `OsdOverlayBridge` forwards it
through `OsdHost.SetFontFamily` (retained across renderer restarts); `HookMetricsPublisher` hands it
to `HookMetricsChannel.Publish`, which sets header bit6 (`FlagFontFamily`) and writes the name as
UTF-8 into the 128-byte field at `OffFontFamily` (0x18000), behind the records, so the version stays
5 and older hooks ignore it. `TruncateUtf8` keeps the name on a character boundary. The prebuilt
Controls must contain this change: a Controls build without it rejects designs whose theme font is
not Segoe UI. Details: the OSD repo's CLAUDE.md.

### Tile styles

Every tile owns its colors; the design shares only `TileOpacity` and the font (the editor's Style
tab). `OsdTileStyle` (OSD Controls) is the one catalog: `PartsOf(tile)` lists the colors a tile
draws as it is set up (a bar: fill, label, value, track; a chart without its badge: line, captions,
area, outline; ...), `Resolve` the color the renderer draws for an unset one (the document's
`Accent`/`TileColor`/`TextColor` are only these defaults and no longer editable), `Set` changes one
color (a group's title keeps its color when the bars change), `CopyToSameKind` and `Reset` back
the Tile style buttons. The inspector's rows (`OsdTileStyleRow`, `TileStyle[field]`) are fixed XAML
rows bound to the catalog, so `DesignTextBindings` sees their text boxes.

Unset colors stay off the scene, so every saved design exports byte for byte as before; the native
widgets gained the keys they need (bar/gauge `labelColor`, `valueColor`, gauge `trackColor`, chart
`labelColor`, `valueColor`, `backgroundColor`, `borderColor`, heatmap `labelColor`), which older
hooks ignore. Designs saved before tile styles may hold label, value and unit colors on kinds that
never drew them (the hardware templates carry them on charts and groups): `MarkUndrawnColors`
reads per tile from the saved scene which colors were drawn, the others stay in the metadata and
off the scene until set (`OsdTileStyle.DrawnColor`), and the MCP tile document leaves them out.
`TileOpacity` (0..1, `editor.tileOpacity`, omitted at 1) scales the alpha of every tile background
at export (`DrawnBackground`); transparent tiles stay transparent. Tests: `OverlayTileStyleTest`.

### Heatmap tiles

A group tile with `GroupDisplay = "heatmap"` exports the native `heatmap` widget, one cell per
member, and keys every cell `avg:{seconds}:{source}` (`GroupWindowSeconds`, 1..600, default 10).
The native widget only draws. The averages come from CapFrameX: `OverlayMovingAverages`
(`CapFrameX.OSD.Integration`) keeps one `SlidingWindowAverage` (`CapFrameX.Statistics.NetStandard`,
a running sum, O(1) amortized per sample) per tracked key; the newest reading stays current for twice
the gap between the last two readings, so a window shorter than the overlay refresh (1 s at the default
`OSDRefreshPeriod`) never falls empty between readings. `OverlayDesignRuntimePublisher` feeds it
from every telemetry snapshot and asks the binder for the plain sources behind the averaged keys
(`GetRequiredSourceIds`). The designer's live preview keeps its own instance. The MCP render has no
history and shows current readings. `OverlayRuntimeSourceBinder.ExpandHeatmap` expands a
`role:cpu-loads` heatmap like the editor: ordered by core type, core and thread, with `types` so
that every core type forms a labelled block of its own (`HeatmapRows` and the label row repeat the
renderer's layout for the flow height). A group without an explicit window takes it from its saved
cell keys (`SavedHeatmapWindow`), so designs saved while the default was 60 s keep it, as the editor's
`FromJson` does.
`OverlayRuntimeSourceBinderTest` checks parity for hybrid CPUs with HT P-cores. The prebuilt
Controls must contain the heatmap: a Controls build without it rejects FUSION. Block and row rules:
the OSD repo's CLAUDE.md.

### Multi-line charts and stutter thresholds

A chart tile may carry `chartLines` (`OsdChartLine`, 1..4), the renderer's `lines`, drawn in order on
one Y axis. `kind` `value` draws a frame series, `stutter` its adaptive stutter threshold: `factor`
(1..10, default 2.5, per line) x the moving average of the analysis chart
(`FrametimeStatisticProvider.GetMovingAverage`); the live chart takes N from the mean of its visible
window instead of the capture. Lines are all in ms or all in FPS, a threshold needs frametimes or
display times, and the tile's `key` is the series of its first value line (unit, value badge and feed
follow it). The editor (`OsdDesignDocument.ValidateChartLines`) and the native parser apply the same
rules; `OverlayRuntimeDesign.Parse` requests the feed of every line's series, and `OverlayTileDocument`
checks the fields of each line. The editor keeps each line's `label` (the legend) on its localized
default unless it was set elsewhere. The palette's "Stutter charts" (`OsdChartPresets`) adds frametimes
and display times with their thresholds: a container on a canvas, two tiles in a grid.
`OverlayChartLinesTest` covers export, round trips, validation and the feeds. The prebuilt Controls
must contain this change: a Controls build without it rejects designs with `chartLines`. Native
details: the OSD repo's CLAUDE.md.

### Generic hardware starters

APEX 3D, REDLINE and the FUSION starters (ARL, RPL, Ryzen) fit a CPU and GPU family, not one model:
they name the installed hardware with `gpuNameShort`/`cpuNameShort`, show no model-specific graphics
(`Resources/OverlayGraphics/SOURCES.md`) and bind every sensor portably. FUSION's P-CORES and E-CORES
tiles are roles of one core type, `role:cpu-loads|P` and `|E`. The runtime binder
(`OverlayRuntimeSourceBinder`) and the editor (`OsdDesignDocument.BindPresetSources`, which binds an
unmodified starter when the designer opens it) filter the family alike; keep them in sync, the parity
tests compare both. The editor keeps the type in the bound key (`device|family|unit|P`), so refreshing
the members adds that type only. The FUSION bar tiles fix the bar height but not the width (32 columns),
so their bars spread over the whole tile whatever the core count. Max clocks per core type come from `cpuPCoreClock`/`cpuECoreClock`
(sensors `CPU P Max`/`CPU E Max`). The former model-specific presets were renamed but keep their
profile IDs (`idPresetId`); `PreviousNames` lets an upgrade replace an unmodified copy under its old
name, unless another profile already has the new one. The prebuilt Controls must contain the
core-type roles: without them the designer leaves FUSION's P/E tiles unbound (the overlay binds them).

### Sharing tile designs (import mapping)

`OverlayDesignProfileTransfer` (`CapFrameX.View/OverlayDesignAuthoring`) exports a design as saved
(validated, UTF-8 without BOM, through a temporary file) and imports at most
`OsdDesignDocument.MaximumJsonLength`; `OverlayDesignImportMapper` adapts the import to this PC and the
designer shows what changed before it asks for the name. A design without `sensor:`/`sensor-id:` keys is
**generic**: its semantic keys and roles bind through `BindPresetSources` as a starter's do, without
restrictions. A design with hardware keys maps every device its sensor IDs name
(`OverlaySensorSources.TryParseSensorId`): onto the same model if this PC has it (the card the design
already reads, also beside an identical twin), else a GPU onto a GPU and a CPU onto a CPU **of the same
vendor**. Of several candidates the one reporting most of the design's readings of that device wins; a
tie is `AmbiguousDevice` and never guessed. Memory, drives and other devices only map onto the same
model. A reading maps onto the sensor of the same type and name, else onto the first sensor of its
semantic rule (`OverlaySemanticKeys`: "GPU TBP" or "GPU Power"). CPU core readings and core groups map
onto **the same core types only**, per family as `OverlaySensorSources.TryGetCoreReading` reads the LHM
names: a group rebinds to this CPU's members of the core types it showed (enabled members), so its size
follows the CPU; labels and hidden members carry over by sensor name, and a single type stays in the key
(`|P`). Unmatched keys keep their value, so the design works again on its own hardware, and are listed
in `OverlayDesignImportResult.Unresolved` with the reason. Semantic keys and roles inside a hardware
design bind as in a generic one. A foreign device's kind and vendor come from a group's device key, else
from its sensor names and device name (`InferDeviceKind` checks CPU names first: APUs name their
Radeon). No GPU semantic key is assigned while two GPUs report the reading (iGPU beside a card), so
generic GPU tiles stay empty there and the import reports them. `CapFrameX.Test/Integration/OverlayDesignTransferTest`
exports and imports between 13 simulated PCs (`OverlayDesignTransferHardware`, catalogs built by the real
`OverlayTelemetryService`): all pairs against an independent oracle of these rules, every starter bound
on every PC, round trips and focused cases.

### Hook-free stall diagnostics

Chart pauses/hitches in the hook-free overlay have three unrelated possible causes — the render
thread not running, the replay clock holding, or PresentMon rows never reaching the feed — and
the logs are laid out to tell them apart with the **Extended OSD logging** switch (Overlay tab;
`ExtendedOsdLoggingController` sets `verboseLog` in `OsdDebug.json` and `CFX_OSD_VERBOSE_LOG=1`
in the user and process environment). Nothing below is written while the switch is off. The
hook-free overlay follows the toggle live: `OsdOverlayBridge.ApplyDiagnosticsSwitch` re-reads the
process variable once per OSD refresh and pushes it to the feed diagnostics, the managed render
loop and, through `OsdHost.VerboseDiagnostics` → `cfx_osd_set_verbose_log`, the native core.
Only the in-game hook and the Vulkan layer still read it at load:

- `%TEMP%\cfx-osd-logs\cfx_osd.log` — the native core's `[diag]` lines (timestamped): host tick gaps, slow
  ticks with a phase breakdown, replay holds/snaps, feed arrival vs. source gaps, scene rebuilds
  with the entries that changed, window placement, topmost fights, scale changes, Present
  HRESULT changes. Documented in the OSD repo's CLAUDE.md.
- `CapFrameX.log` — `HookFree OSD: render loop stall/slow step/10s` (the managed render loop:
  iteration gaps, wake latency, pump/drain/tick durations, GC collection and pause deltas — the
  one thing native code cannot see) and `HookFree feed: ...` from
  `CapFrameX.OSD.Integration/HookFreeFeedDiagnostics.cs`: target PID changes, streaks of rows
  rejected for a foreign PID while the target is silent, additional swapchains inside the
  target process (the hook-free feed does not filter by swapchain, unlike captures), runtime
  label flips (they rename the `<APP>` group and rebuild the scene), frametime outliers, source
  and arrival gaps, backdated rows; one summary per 10 s. `Overlay target process: ... PID a -> b`
  (`CaptureViewModel`) and `Process list: removed ...` (`PresentMonProcessList`) show why a
  PID changed. Repeating lines are rate-limited; summaries come once per 10 s.

### Vulkan titles: implicit layer instead of injection

Vulkan games present through the driver's ICD, so the DXGI Present hook never fires. They are served by an implicit loader layer (`VK_LAYER_CAPFRAMEX_overlay`) staged one folder per bitness — `vulkan\cfx_osd_vklayer.dll` and `vulkan\x86\cfx_osd_vklayer.dll`, mirroring the hook's `hook\` / `hook\x86\` split. Both manifests are byte-identical; `library_path` inside them is relative, so only the folder decides which DLL the loader picks up. The x86 layer comes from its own CMake tree in the OSD repo (`vk_layer`, `cmake -B build-x86 -A Win32`).

The current installer defaults to `C:\Program Files\CapFrameX`: the x64 layer and its `cfx_osd_vklayer_v1.json` manifest go in `C:\Program Files\CapFrameX\vulkan`, and the x86 pair goes in `C:\Program Files\CapFrameX\vulkan\x86`. `Program Files (x86)\CapFrameX` is the legacy install location. Before updating a runtime layer, inspect the manifest paths in both HKLM registry views; development registrations may point at build outputs, and updating those outputs does not update the installed DLLs.

Registration is bitness-scoped and this is load-bearing: the loader identifies a layer by the NAME in its manifest, so a manifest reachable by processes that cannot load its DLL **shadows** the correct registration and disables the layer for that bitness. `CapFrameXInstaller` therefore ships one component per bitness — `Bitness="always64"` for the 64-bit view, `Bitness="always32"` for `WOW6432Node`. The 32-bit one is anchored to `TARGETDIR`, not `INSTALLFOLDER`, because ICE80 rejects a 32-bit component in a directory below `ProgramFiles64Folder`.

**Never register the layer in HKCU** (the OSD repo's `register_layer.cmd` uses HKLM only and purges HKCU leftovers). HKCU is user-controlled, so the loader ignores it for targets started elevated — e.g. a game launched from a Visual Studio that debugs the admin-only `CapFrameX.exe` — and it is not split by bitness, so it also triggers the shadowing above. Both failure modes are silent and look exactly like "this game has no Vulkan": `VulkanActivityProbe` finds no renderer-state mapping, `VulkanLayerModuleProbe` finds no layer module, and `HookOverlayManager` correctly concludes DXGI and injects the hook into a Vulkan title — where the in-game arbiter denies its renderer lease and the status parks on `Initializing` until it turns `Unavailable`.

## In-game hook compatibility probing

The profile catalog (`HookCompatibilityProfileCatalog.LoadProfiles`, read once at startup) merges the
embedded `HookCompatibilityProfiles.xml`, the editable copy next to the executable and the user file
`HookCompatibilityProfiles.xml` in the configuration folder (created empty if missing). A later source
replaces the whole profile per executable name; an invalid or unreadable file is logged and ignored as a
whole. User-facing documentation: README "Custom in-game compatibility profiles" and "Learned compatibility
profiles". The catalog is only a starting point. For every injection `HookOverlayManager` plans a
**stage ladder** (`HookCompatibilityStagePlanner`), judges the injected hook's status against per-stage time
budgets (`HookCompatibilityProbeSession` + `HookCompatibilityVerdictClassifier`) and persists the outcome in
`%appdata%\CapFrameX\Configuration\HookCompatibilityProfiles.learned.json` (`HookLearnedProfileStore`;
keyed by executable name + evidence signature, bound to the hook build hash so a hook update re-verifies).
Learning is mandatory: `_autoCompatibility` is a constant `true`, legacy `HookOverlayAutoCompatibility=false`
settings are ignored. All of it is pure and unit-tested; the manager only executes the session's actions.

- **Stages**: vendor-aware (flags None) → vendor-aware + XeSS-FG native queue → generic D3D12 (RTSS model)
  → generic without FidelityFX lifecycle hooks, each optionally with early injection (gate `d3d12.dll`, or
  `sl.interposer.dll` for Streamline-only evidence). Generic stages exist only when `d3d12.dll` is loaded:
  under the generic route the hook never draws on a D3D11 swapchain (`overlay.cpp`, `knownD3D12Route`).
- **Evidence** (`HookTargetEvidenceProbe`, ToolHelp scan *with paths*): Streamline / DLSS-G / XeSS-FG /
  FSR-FG modules, a `dxgi.dll` outside System32/SysWOW64 (OptiScaler), duplicate FidelityFX loader copies.
  Late attach with FG evidence starts on generic; loader duplicates or a dxgi proxy start on generic-without-FFX;
  a catalog entry is always stage 1 and the ladder continues after it. A learned stage (pending, verified,
  or from an older hook build) only starts at a route that ladder still contains (`CanReuseLearnedStage`),
  never before a catalog/user profile. A verified entry is the start stage; the rest of the ladder stays.
  Exhaustion belongs to the observed run: it runs without an overlay and the entry is marked
  exhausted, but the next launch probes again. There is no reset in the UI: delete
  `HookCompatibilityProfiles.learned.json` while CapFrameX is closed (the store loads once and rewrites it).
- **Verdicts** read status block V2 (`HookStatusProbe`: install phase + FidelityFX module/export detail,
  present coverage, queue state, decline reason, FG telemetry): success confirmed after 2 s, install hung
  after 6 s, no queue after 5 s, stage budget 20 s; idle/dormant samples pause the clocks.
  A hook that is armed but has seen no Present is only judged while PresentMon still delivers frame
  rows of the target (within 3 s, `HookCompatibilityVerdictClassifier.IsTargetPresenting`, fed by
  `HookOverlayManager.OnFrameRow`): a title that is still loading (Metro 2033 Redux presents its
  first frame 42 s after the hook is armed) gives the hook nothing to see, so the verdict is
  `Inconclusive` and both the 15 s first-Present wait and the stage budget stand still. The wait
  keeps what it already counted (`HookCompatibilityProbeSession.UpdateClocks` moves its start
  along), so NoPresent still precedes the budget when the title presents and the hook sees nothing.
  A verified route that pauses, hides or loses its draw progress (a resize, a swapchain replacement)
  leaves `Learned` and is confirmed again within the session (`Reconfirmation` actions). The stored entry
  keeps `verified` until a real failure revokes it, because an unverified entry is not reused on the next
  launch and quitting a game passes through exactly such a moment. A reconfirmation that changes nothing
  is not written (`HookLearnedProfileEntry.RecordsVerifiedOutcome`) and logs its verdict at Debug.
- **Escalation** runs **live** for every bit the hook advertises in `liveReloadCapabilities` (`0xE`: the
  XeSS-FG queue bit, the generic route, the FidelityFX lifecycle switch). `HookCompatibilityChannel` always
  creates the 64-byte V2 mapping (`Local\CfxOsdHookCompatibilityV2_{pid}`, plus the 16-byte V1 one for older
  hooks when flags ≠ None) and `TryPublishStage` rewrites it with a bumped sequence; the hook polls it per
  present, releases its renderer and rebuilds on the new route. The two routing bits only ever turn *on* —
  they neutralize hooks that are already installed, which cannot be undone — so dropping one, early injection
  and the injection delay still need a fresh process: `ScheduleRestart` sets status `RestartPending`, the
  session runs without an overlay, the next stage is persisted as pending and applied on the next
  launch. The session waits up to 3 s for the hook to echo the flags in `AppliedFlags` before it judges the
  new stage; no echo → restart.
- **A frame-generation switch inside the running game** is the case the ladder alone cannot see: the probe
  settled minutes earlier, and a new runtime taking over presentation stands the hook down for good.
  `TryRearmProbeOnEvidenceChange` re-scans the target's modules while the overlay is down (after
  `ProbeRearmStandDownMs`, at most every `ProbeEvidenceRescanIntervalMs`, at most `MaxProbeRearmsPerProcess`
  times) and re-plans through `HookCompatibilityStagePlanner.Replan` when the evidence signature really
  changed. `Replan` keeps the stage the hook is running in the ladder and never starts below it, so what the
  re-probe learns describes what is actually in effect.
- **Log lines**: `HookOverlay: compatibility plan for pid …` (evidence, signature, start stage, reason),
  `… compatibility stage k/n (…) … ended with <verdict>: …`, `… learned profile for '<exe>' (…) is now …`.

## Tile profile authoring over MCP

`TileProfileTools` (`CapFrameX.Mcp`, the `cfx_*_tile_*` tools) lets MCP clients create, edit, render
and activate tile overlay profiles. The tools only shape arguments and results; the work happens in
`IOverlayDesignAuthoringService` (`CapFrameX.Contracts/Overlay`), implemented by
`OverlayDesignAuthoringService` in `CapFrameX.View/OverlayDesignAuthoring` because it needs the OSD
Controls assembly. Clients exchange the **tile document** of `OverlayTileDocument`: the editor's
document settings plus its tile model exactly as `OsdDesignDocument` stores it in each widget's
`editor` metadata. Building one runs `OsdDesignDocument.Validate`, and the store canonicalizes with
`OsdDesignDocument.FromJson(json).ToJson()` like the editor, so every profile written over MCP opens in
the editor unchanged. `OverlayTileDocumentTest` round-trips every starter profile and gallery template
through the tile document; keep it passing. Defaults for omitted fields may only touch fields the
editor always serializes; defaulting an optional (`WhenWritingNull`) field would change designs on
unrelated edits.

- **UI thread**: every operation runs on the app dispatcher, which owns the designer window and the
  native bitmap preview (`OverlayDesignPreviewRenderer`, the editor's renderer).
- **Editor guard**: unsaved changes or a pending prompt in the open designer refuse every library
  write (the store's stale-writer check would otherwise fail the user's next save). After a write the
  designer reloads on the changed profile (`OverlayDesignerControl.ReloadLibraryAsync`, a no-op while
  it has edits), and `IOverlayDesignService.RefreshProfiles` lets an active design update on screen.
- **Binding**: semantic keys and template roles bind to this machine's sources through
  `BindPresetSources`, as when the editor applies a template; `groupKey` set to an
  `OsdSourceGroup` key fills a group's members. Omitted canvas frames are laid out by column.
- **Guide**: `cfx_get_tile_design_guide` returns `OverlayTileDesignGuide`. Update it when the
  Controls assembly adds widget kinds, fields or ranges; the field list at its end is generated.
- **MCP infrastructure**: a tool may return `McpToolResult` for several content blocks (the render
  tool sends an image plus text); `McpParameterSchemaAttribute` replaces a parameter's generated
  schema; `JObject`/`JArray` parameters also accept their JSON as a string.

## Update service

`IUpdateService` (`CapFrameX.Updater/UpdateService.cs`) fetches a JSON manifest from the CapFrameX
update server, compares it against the running assembly version and, once the user confirms,
downloads the installer package into the updates folder. The catalog URI comes from the
`UpdateCatalogUri` key in `App.config`; **while it is empty the whole feature stays inert** and no
update UI appears. The wire format is documented by `CapFrameX.Updater/update-manifest.sample.json`.

The installer replaces the files of the running app, so the instance that downloaded the package
never starts it. `UpdateInstaller.TryStartPendingUpdate` runs in `App.OnStartup`
**before the bootstrapper builds the container** — it reads the `pending-update.json` marker,
re-verifies the package against its SHA-256, starts it and returns true, at which point `App` sets
`_skipShutdownSequence` (nothing has been started yet, so the shutdown sequence in `ApplicationExit`
would only throw) and exits. The marker is deleted *before* the installer is launched so a failing
or cancelled install cannot loop forever; with no marker present, leftover packages are deleted.

That start happens right after the download: a finished download, which the user always asked for,
makes `UpdateViewModel` call `IUpdateService.StartInstallAfterExit` and shut the app down. It starts
a new instance with `--install-update-after <pid>` (`UpdateRestart`), which waits for the old
process at the very top of `OnStartup` — before the single-instance mutex, which `ApplicationExit`
disposes first — ends it after 30 s because the ADLX teardown can deadlock, and then takes the path
above. Only a process running the same executable is awaited or ended. A capture that is running or
still being written (`CaptureManager.IsCapturing`, `LockCaptureService`, `DelayCountdownRunning`)
defers the restart; if the new instance cannot be started, the package stays staged for the next start.
A package is only ever executed when it is an `.exe`/`.msi` and its name resolves inside the updates
folder — the manifest is remote input.

The catalog's SHA-256 comes from the same server as the package, so it cannot tell a genuine release
from one a compromised server announces. `PackageSignature` (Authenticode via `WinVerifyTrust`, no
revocation check) closes that gap twice: on the `.part` download before it is staged, and again in
`TryStartPendingUpdate`, where checksum, signature and `Process.Start` share one read handle that
denies writers — the updates folder is user-writable while the installer runs elevated. Accepted are
the maintainer's Certum code signing certificate through a trusted chain, pinned by subject CN and
the Certum issuer organization rather than by thumbprint so a renewal keeps working, and the pinned
self-signed release certificate (`DAA75711…`, the 1.9.1.0 beta). **A signing certificate with a
different subject name must first be added to `PackageSignature.PublisherNames` in a release signed
with the old certificate**; otherwise installed clients reject every later update.

The signature covers the package, not the command line it is started with. Installer arguments come
from the catalog and wait in the user-writable marker, so the catalog parser and
`TryStartPendingUpdate` both accept only `PackageIntegrity.AllowedInstallerArguments` (`/passive`,
`/quiet`, `/qn`, `/qb`, `/norestart`, `LAUNCHAPP=0|1`); with `TRANSFORMS=` or `/l*v` a signed MSI
would still run foreign code or write files elevated. One package outside the list rejects the whole
catalog, so **a new argument has to ship in the allowlist before any release uses it**.

Version comparison is normalized to `Major.Minor.Build`: assembly versions carry a fourth component
that manifests do not, and `Version` sorts an unset component below zero, so `1.9.1` would otherwise
look older than `1.9.1.0`.

One shared `UpdateViewModel` singleton backs all three surfaces: the indicator on the right of the
status bar (`StateView`), the UPDATE tab in the options popup (`ColorbarView`), and the embedded
dialog. The dialog's `DialogHost` sits in `Shell.xaml`, not in the options popup — a `DialogHost`
nested inside a WPF `Popup` does not reliably render its overlay (same caveat as `ControlView.xaml`).

## PMD time axis

A capture file stores PMD power without times: value i belongs to i × `SampleTime` after the first
frame. No PMD device delivers exactly one sample per `SampleTime`: a measured Powenetics unit sends
979 instead of 1000 packets/s (no losses, the device is simply slower), and BENCHLAB pipe requests
take longer than `MonitoringInterval`. Storing samples as they came compressed a 600 s capture by
12 s. So every PMD sample carries its time in ms on the QPC clock — the clock of `CPUStartQPCTime`
— in `PoweneticsChannel.TimeStamp` / `SensorSample.TimeStamp`, and `CaptureManager.AddPmdData` puts
the samples onto the file's grid with `PmdTimeSeriesResampler` (time-weighted, keeps the average).
`CaptureManager` subscribes before it takes the capture start time and cuts the window by time, so
PMD data covers the first frame and the 2.5 s after the stop are dropped.

Powenetics packets have a 16-bit packet number but no time, and `SerialPortStream` hands the data
over only about every 106 ms (about 104 packets per read, also with blocking reads). Arrival times are
therefore upper bounds. `PoweneticsPacketClock` takes the rate from a least-squares fit of packet
index against arrival time and the offset from the lower envelope of the arrivals (the last packet of
a read arrives right after it was sent); it counts lost packets across the 16-bit wrap and restarts
on implausible jumps. The first ~6 s after connecting use plain arrival times while the rate is
learned. `PoweneticsService` bins by time (`DownSamplingSize` is a length in ms, bins aligned to
multiples of it) instead of by packet count; a bin's `TimeStamp` is its start. `PmdThroughput` and
the "Sample Rate" display show the raw device packet rate. Unit tests: `CapFrameX.Test/PMD/PmdTimingTest`
simulates the measured device including the batched delivery.

## Text field styles

`CapFrameX.View/Styles/CxTextFields.xaml` (merged in `App.xaml`) replaces MDIX's implicit `TextBox`
and `ComboBox` styles with `CxTextBox` / `CxFloatingHintTextBox` / `CxComboBox`. They differ from
the MDIX originals by `VerticalContentAlignment="Bottom"` only, which anchors the text to the
underline instead of centring it in the box — without it every field that is forced taller than its
natural height (the settings UI pins all of them to 40-45px) drifts off its baseline, TextBox and
ComboBox by different amounts. **Use the `Cx*` keys, not the `MaterialDesign*` ones**, whenever a
view sets a text field style explicitly or bases a local style on one; anything that just relies on
the implicit style is already covered.

## Tab headers

Every view with tabs (Analysis, Comparison, Capture, Overlay, PMD, Synchronization) merges
`CapFrameX.View/Styles/CxTabHeaders.xaml` and sets two implicit styles from it: `CxTabControl` for
the `TabControl` and `CxTabHeaderItem` for its `TabItem`s. `CxTabControl` only switches MDIX's
`HorizontalContentAlignment` from `Stretch` to `Left`. With `Stretch`, MDIX hosts the headers in a
uniform grid that spreads them over the whole control, which turned every header into a wide bar
once the localization removed the fixed `TabItem` widths. A header is as wide as its text, has no
`MinWidth` and stops at `MaxWidth` 200: a longer translation ends in an ellipsis, and
`TrimmedTextToolTip` (`CapFrameX.View/Controls`) shows the full text as the tool tip, but only while
it is cut off. **Do not give a `TabItem` a `Width` or `MinWidth` to make a translation fit.** Tab
content starts at the left edge of the headers.

## Overlay editor section headers

MDIX indents every expander header by 24 px (`ExpanderAssist.HorizontalHeaderPadding`, default
`24,12,24,12`). The property is inherited, so `OverlayDesignerControl.xaml` sets `0,12,0,12` once
on the `OsdEditorControl`: every section header of the editor starts where its fields start and
keeps its chevron at the right edge, like the combo boxes. The OSD Controls stay free of MDIX; do
not align these headers with margins. Test: `InspectorSectionHeadersStartWhereTheFieldsStart`.

## Configuration Files
- User settings: `%appdata%/CapFrameX/Configuration/AppSettings.json`
- Overlay config: `%appdata%/CapFrameX/Configuration/OverlayEntryConfiguration_(0/1/2).json`
- Record index: `%appdata%/CapFrameX/Configuration/RecordInfoIndex.json` — the metadata of the
  record files read so far, keyed by path, size and write time (`RecordInfoIndex`), so a record
  folder is listed without reading its files again. Deleting it costs one full read of the folder.
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

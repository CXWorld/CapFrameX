# CapFrameX Service Redesign - Development Log

Last updated: 2026-05-21

This document records the implementation state and decisions for the CapFrameX backend redesign. It complements the broader CEF/Angular plan in `dev-plans/CEF_Angular_CapFrameX_Next_DevPlan.md`.

## Goals

- Move CapFrameX backend work into a modern service boundary.
- Keep frontend communication typed, observable, and testable without a desktop host.
- Support Windows and Linux at the service level where possible.
- Keep Windows-only capabilities such as PresentMon, PawnIO, RTSS, and vendor driver integrations isolated behind provider boundaries.
- Preserve proven legacy monitoring and capture behavior during migration.

## Architecture Direction

The service is split into a small host plus capability-specific modules:

- `CapFrameX.Service.Api`: ASP.NET Core host, localhost API, lifecycle and bridge endpoints.
- `CapFrameX.Service.Contracts`: DTOs shared between service and frontend bridge.
- `CapFrameX.Service.Core`: domain models and interfaces.
- `CapFrameX.Service.Application`: application orchestration.
- `CapFrameX.Service.Infrastructure`: infrastructure services and event bus.
- `CapFrameX.Service.Data`: SQLite/data access layer.
- `CapFrameX.Service.Input`: input abstraction.
- `CapFrameX.Service.Capture`: PresentMon capture integration; Windows-only capability.
- `CapFrameX.Service.Monitoring`: LibreHardwareMonitor-derived sensor stack; currently Windows-heavy because of PawnIO and vendor APIs.
- `CapFrameX.UI`: Angular frontend consuming the service through typed HTTP DTOs and event streaming.

Linux should not load or require PawnIO, PresentMon, RTSS, or Windows driver integrations. These should become optional Windows provider assemblies or Windows-only capability registrations.

## Frontend Bridge Status

Implemented baseline:

- Shared DTO project: `CapFrameX.Service.Contracts`.
- HTTP endpoints for app health/version, capabilities, capture status, and records.
- Server-sent event endpoint at `/api/events`.
- Bridge heartbeat service publishing `app.heartbeat`.
- Angular API client with typed DTOs.
- Angular `EventSource` bridge client for backend events.
- CORS/local-origin policy prepared for localhost, Tauri, and app-style schemes.

Current transport choice:

- HTTP for request/response calls.
- SSE for low-frequency backend events.
- WebSocket or another binary/streaming path should be evaluated before sending high-frequency frametime or sensor streams through Angular change detection.
- A future CEF host should keep native host actions narrow and allowlisted. The service bridge should remain testable without launching CEF.

## Monitoring Sync Status

On 2026-05-21, `source/LibreHardwareMonitorLib` was synchronized into `CapFrameX.Service/src/CapFrameX.Service.Monitoring`.

Transferred areas include:

- ADLX-based AMD GPU interop.
- Nvidia display handle mapping updates.
- Hardware simulation support.
- Intel IMC, OC mailbox, and OOBMSM wrappers.
- New PawnIO resource binaries present in the legacy tree.
- Updated package versions to match the legacy monitoring implementation.

Service-specific adaptations:

- Namespace mapped from `LibreHardwareMonitor` to `CapFrameX.Service.Monitoring`.
- Legacy `CapFrameX.Monitoring.Contracts.ISensorConfig` dependency mapped to a local service contract.
- Legacy `CapFrameX.Extensions` dependency mapped to local service extensions.
- `NativeMethods.txt` keeps SetupAPI entries required by CsWin32 for battery/device enumeration.
- `AnyCPU` builds map to `x64` so CsWin32 generation produces the expected Windows bindings.
- `IntelOOBMSM.bin` is conditionally embedded when the binary is added later.

Verification:

- `dotnet build CapFrameX.Service\src\CapFrameX.Service.Monitoring\CapFrameX.Service.Monitoring.csproj` succeeds.
- `dotnet build CapFrameX.Service\src\CapFrameX.Service.Api\CapFrameX.Service.Api.csproj` succeeds.
- The monitoring project still reports legacy warnings, mostly nullability, XML documentation, unused interop fields, and NuGet pruning warnings.

Known monitoring gap:

- `IntelOOBMSM.bin` is referenced by the new OOBMSM wrapper but is not yet present in the legacy tree. The service project is prepared to embed it once it is added.

## Ignore/Generated Output Policy

Generated frontend output should not be committed:

- `node_modules/`
- `.angular/`
- `/CapFrameX.UI/dist/`

The Angular production bundle can be regenerated with `npm run build` from `CapFrameX.UI`.

## Immediate Next Development Steps

1. Convert capability discovery from static placeholders to real provider registration.
2. Split Windows-only capture and monitoring providers from cross-platform service contracts.
3. Add a provider health/capability endpoint that reports unavailable features instead of failing service startup.
4. Add bridge contract generation or a stricter manual sync process for TypeScript DTOs.
5. Add integration tests for `/api/health`, `/api/app/version`, `/api/capabilities`, and `/api/events`.
6. Decide whether the next desktop host milestone uses CefSharp first or a custom CEF host.
7. Define the high-frequency streaming path for capture frames and sensors before wiring real live data to the Angular UI.

## 2026-09-20 - Implementation planning

- Scope: planning only, no code changes.
- Added `CapFrameX_2.0_UI_Implementation_Plan.md` (mockup-driven UI plan, backend slices, work
  packages M0/M1, decisions D1-D7) and `CapFrameX_2.0_Overlay_DevPlan.md` (tile-based overlay on
  `cfx_osd_core`, design basis TroyMetrics/Benchmark-Overlays, licensing constraints, core gap
  list G1-G15). The central UI mockup is stored in `dev-plans/mockups/`.
- The "Immediate Next Development Steps" above are superseded by section 8 of the UI plan; items
  1-3 (capability providers) move to milestone M2, items 4-5 are work packages B4 and B1.
- Findings recorded while surveying the code: `release/2.0.0` is 364 commits behind
  `release/1.9.1`; the API host has no authentication; `Service.Application` is empty and nothing
  from Data/Infrastructure/Capture/Monitoring is registered in `Program.cs`; no statistics code and
  no legacy record importer exist in the service; `CapFrameX.UI` cannot run `ng test` (missing
  `tsconfig.spec.json` and karma dependencies) and references missing `src/assets`/`favicon.ico`.

## 2026-09-20 - Requirement: frontend on Windows and Linux

- Scope: planning only, no code changes.
- Decision input: the Angular frontend and its desktop shell must run on both platforms. CefSharp is
  therefore out; recommended shell is a native C++ CEF host (CEF Views), verified by a spike on both
  platforms (WP-H1). Tauri stays in the tree as a documented fallback until the spike is accepted.
- UI plan section 6 now specifies: shell options, the host bridge per platform, capability-driven
  frontend rules (no platform checks in feature code, bundled font), service platform providers
  (`Service.Capture.Linux` as a client of the `capframex-linux` daemon, `Service.Monitoring.Linux`
  on hwmon/sysfs/NVML, hotkeys, system info), XDG paths through `IAppPaths`, CI on Windows + Ubuntu
  from M0, AppImage/.deb packaging.
- Overlay plan section 9: design, template schema, presets and editor are shared; renderers are not
  (`cfx_osd_core` on Windows, the Vulkan-layer overlay on Linux).
- New open decisions: D6 now includes the Windows elevation constraint (PresentMon needs
  elevation, the CEF host must not run elevated); D8 - retiring the Avalonia GUI.
- Milestone M1 (records + analysis) needs no platform provider and is accepted on both platforms.

## 2026-09-20 - Service architecture, folder split, OSD reuse, lightweight modes

- Scope: planning only, no code changes.
- Decisions taken: two separately implemented services; the Windows service always runs elevated
  and keeps PresentMon + PawnIO; the Linux service takes presents from the Vulkan layer; the
  Avalonia GUI is removed; top-level folders `CapFrameX.UI`, `CapFrameX.Service.Windows`,
  `CapFrameX.Service.Linux`; `CapFrameX.OSD` (above all its Vulkan layer) is reused on Linux;
  lightweight modes and the "evaluate on demand / load on demand" policy from issue #396 are
  requirements for 2.0.
- New plans: `CapFrameX_2.0_Service_Architecture_DevPlan.md` (shared core + two hosts, platform
  ports, one contract with a conformance suite, repository layout, demand registry),
  `CapFrameX_2.0_Windows_Service_DevPlan.md` (elevated user-session process via a highest-run-level
  scheduled task instead of a session-0 Windows service; privilege-boundary rules for the API),
  `CapFrameX_2.0_Linux_Service_DevPlan.md` (Avalonia salvage + removal, daemon absorbed by the
  service, hotkeys, packaging), `CapFrameX_2.0_Linux_Telemetry_Validation_Plan.md` (kernel
  interfaces as primary source, NVML/RAPL/Super-I/O gaps, probe tool, validation phases V0-V4),
  `CapFrameX_2.0_OSD_CrossPlatform_DevPlan.md` (one OSD repository split into shared/windows/linux;
  portable rasteriser behind `Renderer`, POSIX IPC shim, capture ported into the OSD Vulkan layer).
- Findings: `vk_compositor.cpp`, `layer.cpp`, the shaders and the scene model of `CapFrameX.OSD`
  contain no Win32/Direct2D code; the Vulkan layer already consumes CPU-rasterised BGRA frames
  (`cfx_osd_create_cpu`), so Linux needs a different rasteriser and IPC shim, not a new renderer.
  The old Linux app writes CSV + JSON sidecar records, not the Windows JSON record - a read-only
  importer is planned. The legacy app already registers a `TaskRunLevel.Highest` scheduled task for
  autostart, which the Windows service model reuses.
- D9 confirmed the same day: the platform-neutral core lives in a fourth top-level folder,
  `CapFrameX.Service.Shared`.
- Windows process model decided the same day: the service is a console application started
  headless (scheduled task with highest run level, `conhost.exe --headless`), not an SCM Windows
  service.
- Open: rasteriser choice after OSD spike X1, telemetry helper decision
  after validation phase V3.
- The earlier entry's Linux notes (`Service.Capture.Linux` as client of the daemon inside one
  service, Tauri/CefGlue fallbacks) are superseded where they conflict with the above.

## 2026-09-20 - Implementation started: restructure, ports, first TDD slices

Scope: first code of the 2.0 service work. Windows written test-first, Linux written against the
shared ports with its pure parts under test on any platform.

- **Structure (A1)**: `CapFrameX.Service/` dissolved with `git mv` into `CapFrameX.Service.Shared`
  (Contracts, Core, Application, Data, Api + Data.Tests), `CapFrameX.Service.Windows`
  (Capture, Telemetry = ex-Monitoring, Input, Overlay, Ipc = ex-Infrastructure, installer, tools,
  tests, native) and `CapFrameX.Service.Linux` (new). Renamed projects keep their old
  `RootNamespace`/`AssemblyName`, so namespaces and embedded resource names are unchanged.
  One solution per service, classic `.sln`.
- **Contracts/ports (A2, A4)**: `FrameSample`, `FrameMetrics`, `FrameSourceInfo`,
  `PresentingProcess`, `WellKnownMetrics`, `CapabilityIds`; ports `IFrameSource`,
  `IFrameSubscription`, `ITelemetrySource`, `IHotkeyBackend`, `IOverlayBackend`, `IAppPaths`,
  `IPrivilegeInfo` plus `PlatformAvailability`, `SensorDescriptor`, `SensorSample`.
- **DemandRegistry** (issue #396, shared): leases per demand key, grace period on release so a view
  switch does not restart PresentMon, effective interval = shortest any holder insists on.
  Test-first, 12 tests, `TimeProvider`-based so the grace period is deterministic.
- **PresentMon 2.5.1**: binary taken from `release/1.9.1`, replaces 2.4.0. The fixed column indices
  are gone: `PresentMonColumnLayout` reads the indices from PresentMon's header line,
  `PresentMonFrameMapper` maps a row to `FrameSample`, and the capture service re-binds its columns
  when the header arrives. Unavailable values stay `null` instead of becoming zero. Test-first,
  25 tests. `ParameterNameIndexMapping` keeps the legacy alias names.
- **PawnIO**: complete wrapper, driver and modules taken from `release/1.9.1` (42 files, namespaces
  mapped). New: `NvidiaThermal.cs`, `PawnIO.inf`/`.cat` (PnP install), `IntelOOBMSM.bin` (the gap
  the log noted in May), `IntelPCHThermal.bin`, `Nvidia.bin`, `ZhaoxinMSR.bin`; 22 modules embedded.
  `IntelCpu` call site ported to the new `IntelOcMailbox` signature (NGU sentinel fallback).
  Module lookups now use a literal namespace and one folder casing, pinned through `LogicalName` -
  `nameof(CapFrameX.Service.Monitoring)` would resolve to `Monitoring` and miss every module.
- **Linux (against the ports)**: `LayerMessageHeader`, `LayerFrameData`, `LayerProtocolReader`
  (stream reassembly, payload limit), `LayerFrameMapper` (absolute ns to capture-relative seconds,
  the layer's "zero means unmeasured" convention) and `XdgAppPaths`. All of this is pure and runs
  under test on Windows; 26 tests. Sockets, layer and telemetry readers are still open.
- **Test suite repairs**: the Monitoring tests opened the sensor stack from several classes in
  parallel and a different test failed on every run - parallelisation disabled for that assembly,
  19/19 stable across repeated runs. The capture integration tests now report themselves as
  *skipped* without administrator rights instead of failing.
- **Also fixed**: `CapFrameX.DatabaseTool` targeted `net9.0` against `net10.0` projects and could
  not build.

Verification: `dotnet build` and `dotnet test` on both solutions - Windows 151 passed, 6 skipped
(need elevation); Linux 62 passed.

Known gaps: the 1.9.1 merge (plan task P1) has still not happened, so the rest of the Monitoring
tree remains the May 2026 snapshot while its PawnIO layer is current - re-syncing
`LibreHardwareMonitorLib` wholesale is the follow-up. PresentMon 2.5.1 has not been exercised
against a running game, because that needs an elevated session.

## Documentation Rules For Future Steps

For every meaningful backend/frontend migration step, update this log with:

- Date.
- Scope.
- Files or modules touched.
- Design decision made.
- Verification command and result.
- Known gaps or follow-up work.

Keep detailed subsystem documentation in the subsystem README. Keep cross-cutting architecture decisions and chronological progress here.

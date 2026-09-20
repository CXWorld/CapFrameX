# CapFrameX 2.0 Service Architecture - Development Plan

Last updated: 2026-09-20

> **Decisions this plan is built on (2026-09-20):**
> 1. There are **two services, implemented separately**: a Windows service and a Linux service.
> 2. The Windows service **always runs with administrator rights**; it keeps PresentMon and PawnIO.
> 3. The Linux service takes presents from the **Vulkan layer**; its telemetry source is to be
>    validated (`CapFrameX_2.0_Linux_Telemetry_Validation_Plan.md`).
> 4. The Avalonia GUI in `capframex-linux/` is **removed**; the Angular frontend is the only UI.
> 5. Folder structure: `CapFrameX.UI`, `CapFrameX.Service.Windows`, `CapFrameX.Service.Linux`,
>    `CapFrameX.Service.Shared` (section 2).
> 6. The `CapFrameX.OSD` code - above all its Vulkan layer - is reused on Linux (OSD plan).
> 7. Lightweight modes and the on-demand policy from issue #396 (section 8).
>
> Companion plans: `CapFrameX_2.0_Windows_Service_DevPlan.md`,
> `CapFrameX_2.0_Linux_Service_DevPlan.md`, `CapFrameX_2.0_Linux_Telemetry_Validation_Plan.md`,
> `CapFrameX_2.0_UI_Implementation_Plan.md`, `CapFrameX_2.0_Overlay_DevPlan.md`,
> `CapFrameX_2.0_OSD_CrossPlatform_DevPlan.md`.
>
> This plan resolves decisions D6 and D8 of the UI plan and replaces its section 6.4.
>
> **Diagram:** `dev-plans/architecture/cx2-client-service-architecture.html` - processes, the
> privilege boundary, data paths, the demand registry and the lightweight modes on one page (open in
> a browser; light and dark theme). Keep it in step with this plan when the architecture changes.

## 1. What "separate" means - and what it must not mean

There is **one frontend**. It talks to whichever service runs on the machine, so both services must
expose the **same API contract**, byte for byte: same routes, same DTOs, same event names, same
error shapes. Everything else may differ.

Two ways to get there:

| Approach | Verdict |
|---|---|
| **A. Two host executables with separate platform implementations over a shared, platform-neutral core** (contract, API endpoints, records, analysis, statistics, database, settings, capture state machine) | **Chosen.** Capture, telemetry, hotkeys, overlay, system info, privileges, packaging and process model are implemented twice, independently, in platform-specific assemblies. The contract cannot drift because both hosts map the same endpoint library. |
| B. Two fully independent code bases | Rejected. Roughly two thirds of the service is platform-neutral: record index, legacy-record reader, analysis adapter over `CapFrameX.Statistics.NetStandard`, comparison sets, settings, SSE/WebSocket framing, auth. Writing that twice reproduces the situation the repository is already in - `capframex-linux/src/app/CapFrameX.Core/Analysis/StatisticsCalculator.cs` and `CapFrameX.Statistics.NetStandard` are two independent implementations of "1% low" - and one frontend would show different numbers for the same file depending on the OS. |
| C. One executable with runtime OS switches / plug-in loading (the earlier wording of the UI plan, section 6.4) | Rejected with this decision. No `OperatingSystem.IsWindows()` branches in shared code, no provider discovery at run time. |

Rule: **composition is a compile-time property of the host project.** `CapFrameX.Service.Windows`
references only Windows assemblies, `CapFrameX.Service.Linux` only Linux assemblies (each plus
`CapFrameX.Service.Shared`). Shared code
never references a platform assembly and contains no OS checks (enforced by an architecture test,
section 6).

## 2. Repository layout

**Decision (2026-09-20):** the 2.0 code is split into top-level folders by deliverable:
`CapFrameX.UI`, `CapFrameX.Service.Windows`, `CapFrameX.Service.Linux` and
`CapFrameX.Service.Shared`. Today's `CapFrameX.Service/` and `capframex-linux/` folders are
dissolved into them.

The platform-neutral core of section 1 needs a home that neither service owns. It gets a fourth
folder, `CapFrameX.Service.Shared` - putting it inside one service folder would make the other
service depend on its sibling's tree, and copying it into both is approach B. **Confirmed
2026-09-20 (D9).**

```
CapFrameX.UI/                                  the one frontend + its desktop shell
|-- src/                                       Angular workspace (UI plan section 3)
|-- host/                                      native CEF host, C++/CMake, builds for Windows and Linux (UI plan 6.2)
|-- e2e/                                       Playwright suite, shared screenshot baseline
`-- package.json, angular.json, ...

CapFrameX.Service.Shared/                      net10.0, no OS-specific API
|-- src/
|   |-- CapFrameX.Service.Contracts            DTOs, event types, capability ids  = THE contract
|   |-- CapFrameX.Service.Core                 domain model + platform ports (section 3)
|   |-- CapFrameX.Service.Application          CaptureOrchestrator, RecordLibrary, AnalysisService,
|   |                                          OverlayProfiles, SettingsService, SensorHub, DemandRegistry
|   |-- CapFrameX.Service.Records              legacy JSON/CSV readers + writer, indexer
|   |-- CapFrameX.Service.Analysis             adapter over CapFrameX.Statistics.NetStandard
|   |-- CapFrameX.Service.Data                 EF Core + SQLite
|   `-- CapFrameX.Service.Api                  **class library**: AddCapFrameXApi() / MapCapFrameXApi(),
|                                              controllers, SSE, WebSocket, token auth, problem-details
`-- tests/                                     unit tests + API conformance suite (fake ports)

CapFrameX.Service.Windows/                     net10.0-windows
|-- CapFrameX.Service.Windows.sln              includes ../CapFrameX.Service.Shared/src/*
|-- src/
|   |-- CapFrameX.Service.Windows              console app, started headless + elevated - composition root
|   |-- CapFrameX.Service.Windows.Capture      PresentMon        (today: CapFrameX.Service/src/CapFrameX.Service.Capture)
|   |-- CapFrameX.Service.Windows.Telemetry    LHM port + PawnIO (today: ...Service.Monitoring)
|   |-- CapFrameX.Service.Windows.Input        hotkeys           (today: ...Service.Input)
|   |-- CapFrameX.Service.Windows.Overlay      cfx_osd_core host, hook injection, RTSS (today: ...Service.Overlay + OverlayPlugin + 1.9.1 overlay stack)
|   |-- CapFrameX.Service.Windows.Pmd          PMD / Benchlab
|   `-- CapFrameX.Service.Windows.Platform     paths, trash, system info, privilege, task registration
|-- installer/                                 WiX (today: ...Service.Installer)
|-- tools/                                     CapFrameX.DatabaseTool
`-- tests/                                     Capture, Telemetry, Input suites (moved), TestRenderer

CapFrameX.Service.Linux/                       net10.0, RID linux-x64 (linux-arm64 later)
|-- CapFrameX.Service.Linux.sln                includes ../CapFrameX.Service.Shared/src/*
|-- src/
|   |-- CapFrameX.Service.Linux                console app, user process - composition root
|   |-- CapFrameX.Service.Linux.Capture        Vulkan-layer frame server
|   |-- CapFrameX.Service.Linux.Telemetry      kernel interfaces + NVML (pending validation)
|   |-- CapFrameX.Service.Linux.Input          hotkeys
|   |-- CapFrameX.Service.Linux.Overlay        layer overlay control
|   `-- CapFrameX.Service.Linux.Platform       XDG paths, trash, system info
|-- native/
|   |-- layer/                                 CMake wrapper: builds CapFrameX.OSD `linux/vk_layer` from the submodule or stages prebuilt .so
|   |-- legacy/                                old C capture layer + daemon from capframex-linux (bring-up only, deleted in L3)
|   `-- telemetry-helper/                      optional privileged helper (Linux plan section 5)
|-- tools/                                     cfx-telemetry-probe, capframex-ctl
|-- packaging/                                 .deb, AppImage, systemd units, layer manifests, Flatpak extension
`-- tests/
```

What stays where it is: `source/` (the 1.x WPF application and the libraries both generations use:
`CapFrameX.Statistics.NetStandard`, `CapFrameX.Data.Session`, `CapFrameX.SystemInfo.NetStandard`,
`CapFrameX.OSD.Integration`, the native `CapFrameX.ADLX` / `.IGCL` / `.Hwinfo` projects) - the
services reference these projects in place; nothing is copied. `external/` (OSD submodule and
prebuilt binaries), `version/`, `dev-plans/`, `benchlab-service/`, `pmcreader-plugin/` are unchanged.

Migration of what exists today:
- `CapFrameX.Service/src/CapFrameX.Service.{Contracts,Core,Application,Data,Api}` ->
  `CapFrameX.Service.Shared/src/`. `Service.Api` changes from a `Microsoft.NET.Sdk.Worker`
  executable to a library; its `Program.cs` moves into the two hosts; `Worker.cs` (template stub) is
  deleted; `UseWindowsService()` is removed (Windows plan, section 2).
- `Service.Capture`, `Service.Monitoring`, `Service.Input`, `Service.Overlay`, `Service.OverlayPlugin`,
  `Service.Installer`, `tools/`, the corresponding tests -> `CapFrameX.Service.Windows/`. They are
  Windows code today (PresentMon, PawnIO, Win32 hotkeys, RTSS); the move makes that explicit.
- `Service.Infrastructure` (`InMemoryEventBus`, named-pipe servers) is dissolved: the event path
  becomes `Application -> BridgeEventStream` (UI plan 5.1); the `CapFrameXSensorData` pipe server is
  Windows-only and moves to `Windows.Overlay`.
- `capframex-linux/src/layer` and `src/daemon` -> `CapFrameX.Service.Linux/native/legacy/` until
  phase L3 replaces them with the Linux build of the `CapFrameX.OSD` Vulkan layer (OSD plan);
  `capframex-linux/src/app` (Avalonia) is salvaged and deleted (Linux plan, section 2); its two plan
  documents move to `dev-plans/archive/`. The `capframex-linux/` folder disappears.
- `CapFrameX.UI/src-tauri/` is removed once the CEF host in `CapFrameX.UI/host/` is accepted (UI
  plan WP-H3).
- Do all moves **after** the `release/1.9.1` merge, as mechanical commits without content changes
  (`git mv`), one per target folder, so history stays followable.
- CI: the Windows runner builds `CapFrameX.Service.Windows.sln` + UI; the Ubuntu runner builds
  `CapFrameX.Service.Linux.sln` + UI; both run the `CapFrameX.Service.Shared` tests.

## 3. Platform ports

Interfaces in `Service.Core`; each is implemented exactly once per platform. Shared code sees only
these.

| Port | Responsibility | Windows | Linux |
|---|---|---|---|
| `IFrameSource` | discover presenting processes; stream normalised `FrameSample`s per target; report which frame metrics the source can deliver | PresentMon (ETW) | capture module of the `CapFrameX.OSD` Vulkan layer (Unix socket + memfd frame ring) |
| `ITelemetrySource` | sensor catalogue, snapshot stream, well-known metric mapping (3.2) | LHM port + PawnIO, NVAPI, ADLX, IGCL | kernel interfaces + NVML (to be validated) |
| `IHotkeyBackend` | register/unregister global hotkeys, report what is possible | Win32 | X11 / portal (restricted on Wayland) |
| `IOverlayBackend` | apply overlay profile, show/hide, state, preview | `CapFrameX.OSD`: hook-free window, DXGI hook, Vulkan layer; RTSS | `CapFrameX.OSD` Vulkan layer (same scene model and compositor) |
| `ISystemInfoProvider` | CPU/GPU/RAM/board/OS/driver strings for `SessionInfo` and UI chips | WMI, registry, vendor APIs | `/proc`, `/sys`, DMI, Vulkan device properties |
| `IAppPaths` | config, data, capture, log, runtime directories; portable mode | Known Folders | XDG |
| `ITrash` | move a record to the platform trash | `IFileOperation` | freedesktop trash spec |
| `IPrivilegeInfo` | what the process is allowed to do; reasons for unavailable capabilities | always elevated - verified at start | unprivileged; optional helper present? |
| `IPlatformLifecycle` | single instance, tray icon, autostart registration | mutex, `Shell_NotifyIcon`, scheduled task | runtime-dir lock, StatusNotifierItem, `systemd --user` / XDG autostart |

Because the service is the long-lived process on both platforms (capture must keep working with the
UI closed), **the tray icon and autostart belong to the service**, not to the desktop host. This
supersedes the tray/autostart rows of the host-bridge table in the UI plan (6.2); the host keeps
window control, dialogs, reveal-in-file-manager and external links.

### 3.1 Normalised frame model
`FrameSample` is the union of what both sources can deliver; every optional field is nullable and
the source declares availability once per target, so the frontend can explain a missing chart
instead of drawing zeros.

| Field | PresentMon (Windows) | Vulkan layer (Linux, today) |
|---|---|---|
| process id, name, swap-chain id | yes | pid + name; swap-chain events (`MSG_SWAPCHAIN_CREATED`) |
| time in seconds (monotonic, per capture) | `TimeInSeconds` | `timestamp_ns` |
| ms between presents | yes | `frametime_ms` (CPU-side, in `vkQueuePresentKHR`) |
| ms between display change | yes | `actual_frametime_ms` - **only** with `VK_EXT_present_timing` |
| ms until render complete / until displayed | yes | `ms_until_render_complete` / `ms_until_displayed` - only with `VK_EXT_present_timing` |
| GPU busy / GPU time | yes | **no** - candidates: Vulkan timestamp queries in the layer, DRM fdinfo engine time (telemetry plan) |
| CPU busy / CPU wait | yes | derivable once GPU time exists; not today |
| PC latency, input-to-display | yes (PresentMon 2.x) | no |
| animation error | yes (1.9.x metrics) | derivable when display times exist |
| dropped / present mode / tearing | yes | present mode from swap-chain create info; dropped: no |
| frame type (frame generation) | yes | no (FSR-FG on Proton presents through the same swap chain - to be examined) |
| graphics API | D3D9-12, Vulkan, OpenGL | Vulkan only - which covers DXVK and VKD3D-Proton, i.e. nearly all Proton titles; native OpenGL titles are not captured |

Capability ids per metric (`frames.displayChange`, `frames.gpuBusy`, `frames.pcLatency`, ...) are
part of `GET /api/capabilities` and of each record's metadata.

### 3.2 Well-known metrics
Sensor ids are platform-specific and stay so. On top of them the contract defines a small,
platform-neutral vocabulary - `cpu.load`, `cpu.load.core[i]`, `cpu.power`, `cpu.temp`, `cpu.clock`,
`gpu.load`, `gpu.power`, `gpu.temp`, `gpu.clock`, `gpu.vram.used`, `gpu.vram.total`, `ram.used`,
`ram.total`, `cpu.throttle`, `gpu.throttle`, ... Each `ITelemetrySource` maps its sensors onto the
vocabulary and marks what it cannot provide. Overlay presets (overlay plan 4.3), live stat tiles and
the per-record sensor summary bind to these keys, never to platform sensor ids. The Sensor view
shows the full platform catalogue.

### 3.3 Record format
One format on both platforms: the existing CapFrameX JSON record (`CapFrameX.Data.Session`,
`netstandard2.0`), written by `Service.Records`. Series a source cannot deliver stay empty.
`SessionInfo`/`SessionRun` record the source (`PresentMonRuntime` already exists; the Linux service
writes `CXVulkanLayer <version>` there) and the platform, so analysis can name the reason for a
missing metric. A record captured on Linux opens on Windows and vice versa - records are shared
between machines constantly (reviews, cloud upload).

The Avalonia app wrote a different format (CSV with `MsBetweenPresents,MsUntilRenderComplete,
MsUntilDisplayed,MsActualPresent` + JSON sidecar). `Service.Records` gets a read-only importer for
it so existing Linux captures are not lost; nothing writes that format any more.

## 4. One contract, enforced

- `Service.Api` is the only place where routes exist. Hosts call `AddCapFrameXApi()` and
  `MapCapFrameXApi()`; a host must not map additional public routes.
- The OpenAPI document is generated from `Service.Api` alone (UI plan 5.5) - there is one document,
  not one per platform.
- **Conformance suite** (`CapFrameX.Service.Shared/tests/CapFrameX.Service.Api.ConformanceTests`): the same xunit
  tests run against both composition roots with fake ports, in CI on both operating systems. They
  cover routes, status codes, auth, event framing and the rule that a capability reported
  `unavailable` answers its endpoints with `409` + problem-details instead of `404` or `500`.
- Platform-only features are **capabilities, not routes**: `/api/overlay/*` exists on both; RTSS,
  PMD, in-game hook settings are capability-gated sub-resources that a Linux service reports as
  `unavailable` with reason `not supported on this platform`.
- Architecture tests (NetArchTest or equivalent): nothing in `CapFrameX.Service.Shared` references
  an assembly of `CapFrameX.Service.Windows` or `CapFrameX.Service.Linux`; no `OperatingSystem.Is*`,
  no `RuntimeInformation`, no P/Invoke in the shared assemblies; the two service trees never
  reference each other.

## 5. Shared behaviour both hosts get from the core

- **Capture state machine** (`CaptureOrchestrator`): idle -> armed -> delay -> capturing ->
  processing -> idle; run history and aggregation; one capture at a time; hotkey and API commands
  go through the same transitions. It consumes `IFrameSource` and `ITelemetrySource` only.
- **Online metrics** (live average, lows, stuttering): port of `OnlineMetricService` into
  `Application`, fed by `FrameSample`s - identical on both platforms.
- **SensorHub**: samples `ITelemetrySource` at the configured interval, aligns sensor snapshots to
  the capture timeline, fans out to SSE/WebSocket and to the overlay backend.
- **Live stream**: WebSocket `/api/stream`, binary batches, 10-20 Hz (UI plan 5.6).
- **Auth**: token + Origin/Host validation in `Service.Api`. How the token reaches the frontend is
  platform-specific (Windows plan section 4, Linux plan section 5).

## 6. Work packages

| WP | Deliverable | Acceptance |
|---|---|---|
| A1 | Restructure per section 2 after the `release/1.9.1` merge: the four top-level folders, `git mv` moves, `Service.Api` as library, two host projects that start and serve `/api/health`, one solution per service | both hosts build and start on their OS; existing test suites still green |
| A2 | Ports of section 3 in `Service.Core` + fake implementations for tests | conformance suite runs against both hosts with fakes |
| A3 | Architecture tests (section 4) | a deliberate violation fails CI |
| A4 | `FrameSample`, metric capability ids, well-known metric vocabulary in `Service.Contracts` | documented in the OpenAPI output; reviewed against the PresentMon column set of the merged 1.9.1 code and the layer's `FrameDataPoint` |
| A5 | `Service.Records` writer for the shared JSON format + importer for the Avalonia CSV/JSON format | round-trip test on fixtures from both platforms |
| A6 | CI: Windows runner builds `CapFrameX.Service.Windows.sln`, Ubuntu runner builds `CapFrameX.Service.Linux.sln`, both run the shared tests | green on both |

A1-A3 replace work package B1's "host wiring" step of the UI plan in scope, not in order: they come
directly after the merge and before B2/B3, because every later backend package lands in `CapFrameX.Service.Shared`.

## 7. Risks

| Risk | Mitigation |
|---|---|
| "Separate" erodes into copy-paste between the two service folders | anything needed twice with identical behaviour moves to `CapFrameX.Service.Shared` behind a port; reviewed at each milestone |
| Shared core grows Windows assumptions unnoticed (paths, case-insensitivity, `\r\n`) | Ubuntu CI from the first commit; `IAppPaths`; architecture tests |
| Frame-metric gaps make Linux records look broken in the UI | per-metric capabilities in records and in the live API; the UI states the reason (UI plan 6.3) |
| Renaming projects right after a 364-commit merge causes conflicts | merge first, rename in one mechanical commit, no parallel feature branches during A1 |

## 8. On-demand policy and lightweight modes

Source: [issue #396](https://github.com/CXWorld/CapFrameX/issues/396) ("Make a light weight
version"). The maintainer's statements there are requirements for 2.0:

1. 2.0 introduces **lightweight modes**: one focused on the **in-game overlay**, one focused on
   **capture** (FrameView/FRAPS-like).
2. "Lightweight" means a **less feature-rich UI reduced to a core function** - it is UI configuration.
3. **No reduced or split code path**, no second app version to maintain.
4. CPU load is reduced by a strict **"evaluate on demand"** policy.
5. **Components/DLLs are loaded on demand.**

The two-process architecture already gives the lightest mode for free: the service with its tray icon
and hotkeys runs **without any UI process**; the CEF host is only started when the user wants to see
something. The modes themselves are frontend configuration (UI plan, section 4a). What the service
has to guarantee is points 4 and 5 - as rules of the shared core, identical on both platforms:

### 8.1 Demand registry
`DemandRegistry` (in `Service.Application`) is the single place that knows who currently needs what.
Consumers take **leases** on demand keys; everything expensive is driven by lease counts.

| Consumer | Leases it takes |
|---|---|
| overlay profile active + visible | the well-known metrics and frame metrics its modules bind to |
| capture armed / running | frame stream of the target, sensors selected for logging |
| UI view subscribed over WebSocket/SSE | what that view shows (live graph, sensor table, process list) - released automatically when the connection drops or the view unsubscribes |
| record library / analysis request | indexer, parsed-session cache - request-scoped |

| Demand | Activated only while leased |
|---|---|
| frame source | Windows: the PresentMon process and its ETW session; Linux: frame delivery from the layer for that pid (the layer stays passive otherwise - Linux plan section 7) |
| process list / target detection | process polling |
| telemetry | **per sensor**: `SensorHub` polls only sensors with a lease, updates only the hardware groups that contain one, at the slowest interval any lease-holder accepts; no lease -> no polling at all |
| low-level hardware access | Windows: PawnIO device opened and modules loaded on the first lease of a sensor that needs them, closed after a grace period; Linux: privileged helper contacted likewise |
| online metrics (averages, lows, stutter %) | computed only for leased metrics |
| overlay backend | OSD core / hook injection / layer overlay only with an active profile |
| PMD, RTSS bridge, cloud, update check | first use |
| record indexer | runs on first library demand, then stays on the file watcher (cheap); analysis caches are size- and time-bounded |

Releasing is delayed by a short grace period (seconds) so that switching views does not restart
PresentMon or re-open PawnIO.

### 8.2 Loading on demand
- Composition roots register platform modules as **lazy factories** (`Lazy<T>` / factory
  delegates); no platform module is constructed at start-up. .NET then loads the assembly on first
  use, so `Windows.Telemetry` (183 files, embedded PawnIO blobs), the OSD interop, EF Core and the
  analysis stack are not even mapped into an overlay-less, capture-less service.
- Native libraries (`cfx_osd_core`, ADLX/IGCL/Hwinfo wrappers, NVAPI/NVML, `libnvidia-ml.so.1`) are
  loaded by their module on first lease, never at process start.
- Start-up does exactly: configuration, token, API listener, tray, hotkey registration.
- Guard: an integration test starts each host, takes no lease, and asserts on the **loaded module
  list** (managed assemblies and native DLLs) and on the absence of child processes; a second test
  takes an overlay-only lease set and asserts that capture/records/analysis assemblies stay unloaded.

### 8.3 Budgets (measured per milestone, both platforms, logged in the dev log)
| State | Service CPU | Service RAM | Processes |
|---|---|---|---|
| idle, no leases | ~0 % (no timers faster than 1 s) | target < 60 MB | service only |
| overlay mode, UI closed | dominated by the demanded sensors; compare against 1.9.1 with the same overlay | measured | service (+ PresentMon on Windows when frame metrics are shown) |
| capture mode, UI closed | as overlay mode + capture buffers | measured | same |
| full UI open | + CEF host (its cost is the UI's, not the service's) | measured | + host and CEF subprocesses |

### 8.4 What is explicitly not done
No "lite" build, no feature flags at compile time, no second installer, no separate overlay-only
executable. One service per platform, one frontend; modes select what is *shown* and therefore what
is *demanded*.

Related points from the discussion in the issue: dropping the RTSS dependency is already met by the
OSD core's own delivery paths (overlay plan); dropping administrator rights on Windows is **not**
planned - the Windows service always runs elevated (Windows plan) - but with 8.1 PawnIO is not
touched unless a sensor that needs it is demanded.

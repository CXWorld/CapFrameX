# CapFrameX Next CEF/Angular Architecture - Development Plan

> **Goal:** Build the next CapFrameX desktop application around a native Windows backend with a CEF-hosted Angular frontend, following the architecture pattern observed in the NVIDIA App: a Chromium Embedded Framework shell, Angular/Angular Material web UI bundles, a typed native bridge, and native plugin/service DLLs for hardware-facing work.

> **Decision:** CapFrameX should not move to Electron. Electron would duplicate Chromium plus Node.js and increase runtime size and attack surface without solving the hard parts of CapFrameX: capture orchestration, native sensor integrations, overlay control, driver/tool interop, and low-overhead real-time data streaming. Use CEF for the UI runtime and keep performance-critical and privileged work in .NET/native components.

## Implementation Status

Last updated: 2026-08-23

The first backend/frontend bridge slice is implemented in `CapFrameX.Service` and `CapFrameX.UI`:

- `CapFrameX.Service.Contracts` contains shared DTOs for app metadata, health, capabilities, capture status, records, and bridge events.
- `CapFrameX.Service.Api` exposes typed localhost HTTP endpoints and `/api/events` as a server-sent event stream.
- The Angular app consumes the service through a typed API service and an `EventSource` bridge client.
- The bridge currently covers health/version/capabilities/status/records placeholders plus heartbeat events.
- High-frequency capture and sensor streaming is not implemented yet and should be designed before wiring live frametime or sensor data to Angular.

Detailed progress is tracked in `dev-plans/CapFrameX_Service_Redesign_Development_Log.md`.

**Platform requirement (2026-09-20): the frontend must run on Windows and on Linux.** This
overrides every Windows-only assumption below. In particular the "prefer CefSharp" line in section 2
no longer applies - CefSharp is Windows-only; the recommended shell is a native C++ CEF host built
on CEF Views (same Chromium on both platforms), with CefGlue and Tauri as fallbacks. Shell choice,
host bridge, service platform providers, packaging and CI for both platforms are specified in
section 6 of the UI implementation plan. Of the existing Linux code base (`capframex-linux/`) the
Avalonia GUI is removed, the daemon is absorbed by `CapFrameX.Service.Linux`, and the capture layer
is replaced by the Linux build of the `CapFrameX.OSD` Vulkan layer.

Further decisions of 2026-09-20, each detailed in its own plan: two separately implemented
services (Windows always elevated; Linux with presents from the Vulkan layer) over the shared core
`CapFrameX.Service.Shared` (`CapFrameX_2.0_Service_Architecture_DevPlan.md`,
`CapFrameX_2.0_Windows_Service_DevPlan.md`, `CapFrameX_2.0_Linux_Service_DevPlan.md`,
`CapFrameX_2.0_Linux_Telemetry_Validation_Plan.md`); reuse of `CapFrameX.OSD` on Linux
(`CapFrameX_2.0_OSD_CrossPlatform_DevPlan.md`); lightweight modes and the on-demand policy from
issue #396; the folder layout in section 4.

Implementation-level planning for UI and overlay (added 2026-09-20):

- `dev-plans/CapFrameX_2.0_UI_Implementation_Plan.md` - work packages, milestones, API surface and
  design system, driven by the central UI mockup `dev-plans/mockups/capframex_redesign_mockup.html`.
  It narrows two points of this document: the UI kit is an own component set on Angular CDK (no
  Angular Material theme), and the `CapFrameX.UI/src-tauri` scaffold is a leftover that the CEF host
  replaces.
- `dev-plans/CapFrameX_2.0_Overlay_DevPlan.md` - Phase 3 (overlay configuration) in detail, with
  TroyMetrics/Benchmark-Overlays as the design basis and the `cfx_osd_core` widget tree as the
  render target.

## 1. Target Architecture

Revised 2026-09-20: client and service are **separate processes**; the service exists once per
platform over a shared core. Diagram: `dev-plans/architecture/cx2-client-service-architecture.html`.

```
CapFrameX.UI  (client - unelevated, identical on Windows and Linux, may be closed)
|-- native CEF host: CEF bootstrap and lifecycle, app://capframex scheme, window management, DPI,
|   single instance, narrow host bridge (window controls, dialogs, external links)
|-- Angular frontend: capture, analysis, overlay, comparison, aggregation, sensor, report, cloud,
|   settings; app modes Full / Capture / Overlay
        |
        |  HTTP on 127.0.0.1 + token   |   SSE events   |   WebSocket live stream
        v
CapFrameX.Service.Windows (always elevated)      |      CapFrameX.Service.Linux (user process)
|-- CapFrameX.Service.Shared - identical in both: typed API + event stream, capture lifecycle,
|   record/session storage and index, analysis, settings and profiles, demand registry
|-- platform modules behind ports:
|   Windows: PresentMon, PawnIO-based sensors, Win32 hotkeys, CapFrameX.OSD (hook-free window,
|            DXGI hook, Vulkan layer), RTSS, PMD
|   Linux:   CapFrameX.OSD Vulkan layer (presents + overlay), kernel telemetry + NVML, hotkeys
|-- owns tray icon, hotkeys and autostart - capture and overlay work without the UI
```

Core principle: the frontend renders and orchestrates workflows; it does not own capture, sensor, file, overlay, or driver-facing logic.

## 2. Technology Stack

### Desktop shell

- **CEF / Chromium Embedded Framework** as the desktop web runtime.
- ~~Prefer a .NET-friendly CEF host first, such as CefSharp.~~ Superseded 2026-09-20: the frontend must run on Windows and Linux and CefSharp is Windows-only. Use a native C++ CEF host (CEF Views) on both platforms; CefGlue is the .NET fallback. See the UI implementation plan, section 6.1.
- Use a custom local scheme such as `capframex://app/index.html` or `app://capframex/index.html`.
- Disable arbitrary remote navigation by default. External links open in the system browser.

### Frontend

- **Angular** for the application frontend.
- **Angular Material / CDK** for controls, overlays, dialogs, menus, keyboard behavior, virtual scrolling, and accessibility primitives.
- **RxJS** for live streams from capture, sensors, and overlay state.
- **TypeScript** with strict mode.
- **Webpack/Vite/Angular CLI build output** as static assets hosted by the CEF shell.
- **Service worker only if needed** for asset caching/offline shell behavior. Avoid it in early phases unless there is a clear startup benefit.

### Visualization

- Keep the existing statistical and charting domain logic in .NET where practical.
- Use web-native charting for the new UI:
  - Candidate for high-density frametime plots: Canvas/WebGL-based renderer.
  - Candidate for normal UI charts: lightweight chart library with explicit performance testing.
- Do not pick charting by appearance alone. Benchmark with large capture files and multi-run comparisons before committing.

### Backend

- Move toward a modern .NET backend boundary for new code.
- Preserve existing proven CapFrameX services during migration:
  - PresentMon capture integration
  - record parsing/storage
  - statistics calculations
  - sensor reporting
  - RTSS overlay integration
  - native interop projects
- If the first phase must stay in the current .NET Framework process, keep the bridge compatible with that constraint and avoid adopting frontend choices that force an immediate backend rewrite.

### Native and third-party libraries

- Continue using native DLLs for vendor/hardware integration where required.
- Treat plugins/services as explicit backend modules, not frontend dependencies.
- OpenSSL, protobuf, telemetry, QR, search, or other web libraries should only be added when a CapFrameX feature needs them. Do not copy the NVIDIA dependency set wholesale.

## 3. Why This Matches CapFrameX

CapFrameX has two very different workloads:

- A rich, frequently changing application UI for capture setup, analysis, comparison, history, sensors, and settings.
- Low-level and performance-sensitive backend work that depends on Windows APIs, native libraries, hardware vendors, PresentMon, RTSS, and file I/O.

CEF plus Angular separates those workloads cleanly. The frontend can evolve like a modern product UI, while the backend remains a controlled native/.NET runtime optimized for capture correctness and low overhead.

## 4. High-Level Project Structure

Decided 2026-09-20. The 2.0 code lives in four top-level folders; the earlier `CapFrameX.Next.*`
and `CapFrameX.CefHost` / `CapFrameX.CefBridge` / `CapFrameX.WebUI` proposals are dropped.

```
CapFrameX.UI/                  Angular workspace (src/), native CEF host for Windows + Linux (host/), e2e tests
CapFrameX.Service.Shared/      platform-neutral service core, net10.0, no OS-specific API:
                               Contracts (DTOs = the API contract, source of the generated TypeScript types),
                               Core (domain + platform ports), Application, Records, Analysis, Data, Api (library),
                               conformance tests
CapFrameX.Service.Windows/     elevated Windows host + Capture (PresentMon), Telemetry (PawnIO), Input, Overlay,
                               Pmd, Platform, installer
CapFrameX.Service.Linux/       Linux host (user process) + Capture (Vulkan layer), Telemetry (kernel + NVML),
                               Input, Overlay, Platform, native/, tools/, packaging/
external/CapFrameX.OSD         OSD submodule (shared / windows / linux inside), prebuilt fallback
source/                        1.x application and the libraries both generations reference in place
                               (Statistics.NetStandard, Data.Session, SystemInfo.NetStandard, OSD.Integration, native wrappers)
dev-plans/, overlay-templates/, images/, version/
```

Both services reference `CapFrameX.Service.Shared`; they never reference each other. Today's
`CapFrameX.Service/` and `capframex-linux/` folders are dissolved into this layout. Full tree,
migration steps and the rules that keep the shared core platform-neutral:
`CapFrameX_2.0_Service_Architecture_DevPlan.md`, section 2. Diagram:
`dev-plans/architecture/cx2-client-service-architecture.html`.

## 5. Bridge Design

### Requirements

- The bridge must be typed, versioned, cancellable, and observable.
- It must support request/response commands and push events.
- It must not expose arbitrary filesystem or process APIs to JavaScript.
- It must be testable without launching CEF.

### Command examples

```ts
capture.start(request: StartCaptureRequest): Promise<CaptureSessionInfo>
capture.stop(): Promise<CaptureResult>
capture.getStatus(): Promise<CaptureStatus>

records.list(filter: RecordFilter): Promise<RecordSummary[]>
records.load(id: string): Promise<RecordDetails>
records.delete(id: string): Promise<void>

comparisons.list(): Promise<ComparisonSetSummary[]>
comparisons.get(id: string): Promise<ComparisonSetDetails>
comparisons.create(request: CreateComparisonSetRequest): Promise<ComparisonSetDetails>
comparisons.update(id: string, request: UpdateComparisonSetRequest): Promise<ComparisonSetDetails>
comparisons.duplicate(id: string, name: string): Promise<ComparisonSetDetails>
comparisons.delete(id: string): Promise<void>

settings.get(): Promise<AppSettingsDto>
settings.update(patch: AppSettingsPatch): Promise<AppSettingsDto>

overlay.getProfiles(): Promise<OverlayProfile[]>
overlay.updateProfile(profile: OverlayProfile): Promise<void>
```

### Event examples

```ts
capture.statusChanged
capture.frameMetrics
sensors.snapshot
sensors.deviceChanged
records.importCompleted
overlay.stateChanged
app.updateAvailable
```

### Transport candidates

1. **CEF JavaScript binding / message router**
   - Best fit for desktop-local privileged commands.
   - Requires careful async dispatch and serialization.

2. **Local HTTP + WebSocket**
   - Reuses existing webservice concepts.
   - Easy to debug and test.
   - Needs strict localhost binding and request validation.

3. **Hybrid**
   - JS binding for privileged commands.
   - WebSocket for high-frequency telemetry and live capture streams.

Recommended: start with the hybrid model if it can be implemented without adding avoidable complexity. Use HTTP/WebSocket for testability and streaming, and a narrow CEF binding for host-specific actions such as window controls, file dialogs, and external link handling.

## 6. UI Application Model

### Main views

- Dashboard
- Capture
- Analysis
- Comparison
- Record Library
- Sensors
- Overlay
- System Info
- Settings

### UX goals

- Dense, operational layout rather than a marketing-style landing page.
- Fast navigation between capture, records, and analysis.
- First-class keyboard and game-benchmark workflows.
- Clear live/recorded state distinction.
- Responsive enough for laptop screens, but optimized primarily for desktop.

### Persistent comparison sets (v2.0 requirement)

Users must be able to save complex comparison setups as named sets and restore them after an application or service restart. A saved set is a first-class backend resource, not temporary Angular state or browser local storage.

Functional scope:

- Create a comparison from selected records and save it under a user-defined name and optional description.
- List, open, update, rename, duplicate, and delete saved sets.
- Preserve the ordered record membership and allow the same record to participate in multiple sets.
- Preserve comparison-specific presentation state needed to reconstruct the workspace, including selected metrics, primary and secondary label contexts, grouping and sort settings, active chart/view, range selection, and per-record label, color, and visibility overrides where supported.
- Track whether the open comparison differs from its saved revision and provide explicit `Save` and `Save As` actions. Do not silently overwrite a named set.
- Remember the last opened saved set and restore its saved revision when the Comparison view is opened after restart.

Persistence model:

```text
ComparisonSet (1) --> (N) ComparisonSetItem (N) --> (1) Record
```

- Store sets in the service-owned SQLite database so every desktop frontend observes the same data and portable-mode/database backup rules apply consistently.
- Reference records by stable service IDs rather than filenames or frontend object identity.
- Use a separate ordered membership entity. The existing `Suite -> Session` ownership relationship must not be reused directly because it would re-parent a session and prevent one record from belonging to multiple comparison sets.
- Store a schema version and timestamps with each set. Updates must be transactional.
- Persist references and user configuration, not calculated statistics, chart series, or copied capture data; derive those again from the current record data when the set is opened.
- Keep a set loadable when referenced records are missing or have been deleted. Return unresolved items explicitly so the UI can warn the user and let them remove or replace those entries.

### Angular module boundaries

```
app/
|-- core/                 shell, routing, bridge client, logging
|-- shared/               UI primitives and formatting helpers
|-- features/
|   |-- dashboard/
|   |-- capture/
|   |-- analysis/
|   |-- records/
|   |-- sensors/
|   |-- overlay/
|   |-- settings/
|-- data-access/          typed API clients and RxJS stores
|-- visualization/        frametime plots and comparison charts
```

Prefer feature-local state first. Introduce a global state library only when cross-feature state becomes painful and measurable.

## 7. Migration Strategy

### Phase 0 - Proof of architecture

Deliver a small CEF host that loads a local Angular build and can call one backend command.

Acceptance criteria:

- CEF window opens reliably on Windows x64.
- Angular app loads from packaged local assets.
- Frontend can call `app.getVersion`.
- Backend can push a simple periodic event to the frontend.
- External navigation is blocked or redirected to the default browser.
- App can be packaged in a local build output folder.

### Phase 1 - Read-only CapFrameX shell

Build a read-only UI around existing CapFrameX data.

Scope:

- record library
- record details
- basic analysis summary
- system info
- app settings read path

Acceptance criteria:

- Existing CapFrameX capture records can be listed and opened.
- Statistics match the current WPF app for selected fixture records.
- Large record loading is profiled and does not freeze the UI.
- The bridge has unit tests for serialization and error handling.

### Phase 2 - Live capture and sensor streaming

Expose live status and streaming metrics.

Scope:

- capture start/stop
- capture status
- live FPS/frametime stream
- sensor snapshots
- error/status notifications

Acceptance criteria:

- Capture lifecycle works from Angular UI.
- Live telemetry stays responsive during capture.
- Backend protects against invalid concurrent capture commands.
- UI clearly reports capture failures and permission/tooling issues.

### Phase 3 - Overlay configuration

Move overlay profile management into the new UI.

Scope:

- list overlay profiles
- edit overlay entries
- preview layout where practical
- persist overlay configuration
- RTSS status and validation

Acceptance criteria:

- Existing overlay configs round-trip without data loss.
- Invalid configs are blocked before persistence.
- The current overlay pipeline remains compatible.

### Phase 4 - Full analysis parity

Replace the main WPF analysis workflow.

Scope:

- comparison views
- persistent named comparison sets
- comparison-set library with save, save-as, rename, duplicate, delete, and reopen workflows
- percentile charts
- sensor correlation
- aggregation tables
- export flows

Acceptance criteria:

- Results match current CapFrameX calculations.
- Chart performance is acceptable for large real-world captures.
- Export formats remain compatible.
- A saved comparison survives a full frontend and service restart and reopens with the same record order and comparison-specific presentation state.
- A record can belong to multiple saved comparison sets without being duplicated or moved in record storage.
- Missing record references are reported per item without preventing the remaining set from loading.
- Set persistence is covered by a database migration and remains backward compatible through an explicit schema version.

### Phase 5 - Productization

Harden runtime, packaging, diagnostics, and update behavior.

Scope:

- installer integration
- CEF cache/location policy
- crash reporting/log bundling
- GPU process handling
- settings migration
- accessibility pass
- localization pipeline

Acceptance criteria:

- Clean install and upgrade paths are tested.
- Logs identify frontend, bridge, backend, and CEF failures separately.
- CEF subprocesses shut down cleanly.
- App works without internet access.

## 8. Packaging Model

Target layout (Windows shown; Linux has the same parts with `CapFrameX.Service.Linux`, the layer
`.so` files and no `hook/`):

```
CapFrameX/
|-- CapFrameX.exe                      native CEF host (from CapFrameX.UI/host), unelevated
|-- CEF/
|   |-- libcef.dll
|   |-- chrome_*.pak
|   |-- icudtl.dat
|   |-- locales/
|-- www/                               Angular production bundle (from CapFrameX.UI)
|   |-- index.html
|   |-- main.*.js, polyfills.*.js, chunk-*.js
|   |-- assets/
|-- service/
|   |-- CapFrameX.Service.Windows.exe  elevated, started through the scheduled task
|   |-- CapFrameX.Service.*.dll        shared core (CapFrameX.Service.Shared) + Windows modules
|   |-- PresentMon/, native wrappers (ADLX, IGCL, Hwinfo), cfx_osd_core.dll
|   |-- hook/, hook/x86/, vulkan/, vulkan/x86/
```

Build pipeline:

- restore NuGet packages
- build `CapFrameX.Service.Shared` + the platform service (`CapFrameX.Service.Windows.sln` / `CapFrameX.Service.Linux.sln`)
- build the native CEF host (`CapFrameX.UI/host`, CMake)
- install frontend packages
- run frontend tests/lint
- build Angular production bundle
- copy bundle into host output
- copy CEF runtime
- run smoke test that launches host and verifies app bootstrap

## 9. Security Rules

- Default to local files and localhost only.
- Block arbitrary remote content in the CEF surface.
- Allowlist any required NVIDIA/CapFrameX web endpoints explicitly.
- Disable Node-style filesystem access in the frontend; there should be none with CEF.
- Validate every bridge request on the backend.
- Avoid exposing raw paths unless required for user-facing workflows.
- Use a permission boundary for destructive actions: delete record, overwrite settings, reset overlay, start capture with elevated requirements.

## 10. Performance Rules

- Keep capture and sensor loops independent from UI frame rate.
- Never send high-frequency telemetry through Angular change detection one event at a time.
- Batch or sample live streams before UI rendering.
- Use Web Workers for expensive frontend transforms if needed.
- Keep chart rendering off Angular's hot path.
- Measure cold start, record open time, capture start latency, and chart interaction latency.

Initial budgets:

- cold app shell visible: under 2 seconds on a typical development machine
- record list visible: under 1 second after shell load
- capture command dispatch: under 100 ms excluding external tool startup
- live UI update cadence: 10-20 Hz unless a specific view needs more
- backend capture overhead: no measurable regression versus current app

## 11. Testing Strategy

### Backend tests

- bridge command dispatch
- DTO serialization compatibility
- capture lifecycle state machine
- record loading and statistics parity
- comparison-set repository CRUD, ordered membership, transactional updates, and missing-record handling
- settings migration

### Frontend tests

- bridge client contract tests
- feature component tests for critical workflows
- chart data adapter tests
- comparison-set hydration, dirty-state, save/save-as, and restart restoration tests
- accessibility smoke checks for dialogs, menus, and keyboard navigation

### Integration tests

- launch host
- load Angular shell
- call `app.getVersion`
- list fixture records
- open fixture record
- save a multi-record comparison set, restart the service/frontend, and reopen the same set
- start/stop mocked capture
- validate event stream

### Manual validation

- high-DPI displays
- multi-monitor behavior
- overlay and RTSS installed/missing cases
- offline startup
- driver update / PresentMon missing / sensor provider missing error paths

## 12. Key Risks

| Risk | Impact | Mitigation |
| --- | --- | --- |
| CEF packaging size | Larger installer | Measure early; compare CEF runtime options; avoid Electron |
| Angular chart performance | Poor analysis UX | Prototype charting with large captures before full migration |
| Bridge grows unsafe | Security and maintenance risk | Typed allowlisted commands only; no generic eval/file APIs |
| .NET Framework constraints | Slows new architecture | Keep bridge compatible initially; plan backend modernization separately |
| WPF parity takes too long | Long migration period | Ship read-only and capture milestones before full parity |
| CEF lifecycle bugs | Shutdown/crash issues | Dedicated host tests and strict subprocess cleanup |

## 13. Open Decisions

- Use CefSharp or a custom CEF host?
- Keep the first backend inside the existing CapFrameX process or create a new host process immediately?
- Use local HTTP/WebSocket, CEF message router, or hybrid transport?
- Which charting renderer can handle CapFrameX-scale data best?
- Does the new app replace WPF all at once, or ship as a parallel preview first?
- Which settings and record formats become stable external contracts?

## 14. Recommended First Implementation Slice

Create a small vertical slice:

1. `CapFrameX.CefHost` launches a CEF window.
2. `CapFrameX.WebUI` builds an Angular app into `www/`.
3. The Angular app shows app version, build type, and a record count.
4. The backend exposes `app.getVersion` and `records.list`.
5. One fixture record can be opened and summarized.
6. Add a smoke test that verifies the host can load `index.html` and complete the first bridge call.

This slice proves the architecture without touching capture, overlay, or sensor logic.

## 15. Definition of Done for Architecture Adoption

The CEF/Angular direction is accepted only when:

- one packaged prototype runs without a development server;
- one real CapFrameX record is loaded through the bridge;
- statistics match the current implementation for that record;
- CEF shutdown is clean;
- installer/runtime size is known;
- startup performance is measured;
- the team has a clear answer for CefSharp vs custom CEF host.

Until then, this remains an architecture prototype, not a committed rewrite.

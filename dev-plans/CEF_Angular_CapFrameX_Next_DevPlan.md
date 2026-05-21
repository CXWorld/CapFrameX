# CapFrameX Next CEF/Angular Architecture - Development Plan

> **Goal:** Build the next CapFrameX desktop application around a native Windows backend with a CEF-hosted Angular frontend, following the architecture pattern observed in the NVIDIA App: a Chromium Embedded Framework shell, Angular/Angular Material web UI bundles, a typed native bridge, and native plugin/service DLLs for hardware-facing work.

> **Decision:** CapFrameX should not move to Electron. Electron would duplicate Chromium plus Node.js and increase runtime size and attack surface without solving the hard parts of CapFrameX: capture orchestration, native sensor integrations, overlay control, driver/tool interop, and low-overhead real-time data streaming. Use CEF for the UI runtime and keep performance-critical and privileged work in .NET/native components.

## Implementation Status

Last updated: 2026-05-21

The first backend/frontend bridge slice is implemented in `CapFrameX.Service` and `CapFrameX.UI`:

- `CapFrameX.Service.Contracts` contains shared DTOs for app metadata, health, capabilities, capture status, records, and bridge events.
- `CapFrameX.Service.Api` exposes typed localhost HTTP endpoints and `/api/events` as a server-sent event stream.
- The Angular app consumes the service through a typed API service and an `EventSource` bridge client.
- The bridge currently covers health/version/capabilities/status/records placeholders plus heartbeat events.
- High-frequency capture and sensor streaming is not implemented yet and should be designed before wiring live frametime or sensor data to Angular.

Detailed progress is tracked in `dev-plans/CapFrameX_Service_Redesign_Development_Log.md`.

## 1. Target Architecture

```
CapFrameX.Next.exe
|-- Native desktop host
|   |-- CEF bootstrap and lifecycle
|   |-- app://capframex local scheme
|   |-- window management, DPI, tray, single-instance handling
|   |-- bridge registration and permissions
|
|-- CapFrameX backend services
|   |-- capture lifecycle
|   |-- PresentMon integration
|   |-- sensor polling and aggregation
|   |-- record/session storage
|   |-- settings and profile management
|   |-- overlay/RTSS integration
|
|-- Angular frontend
|   |-- dashboard
|   |-- capture setup
|   |-- analysis views
|   |-- sensor/overlay configuration
|   |-- settings
|
|-- Bridge layer
|   |-- request/response API for commands
|   |-- event stream for live capture/sensor data
|   |-- typed contracts shared with frontend
```

Core principle: the frontend renders and orchestrates workflows; it does not own capture, sensor, file, overlay, or driver-facing logic.

## 2. Technology Stack

### Desktop shell

- **CEF / Chromium Embedded Framework** as the desktop web runtime.
- Prefer a .NET-friendly CEF host first, such as CefSharp, if it satisfies performance, DPI, message-pump, sandbox, and packaging requirements.
- Keep a custom native CEF host as a fallback option only if CefSharp blocks critical requirements.
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

Recommended target structure:

```
CapFrameX.sln
|-- source/
|   |-- CapFrameX.Next.Host/             native/.NET desktop host with CEF
|   |-- CapFrameX.Next.Bridge/           typed bridge contracts and dispatch
|   |-- CapFrameX.Next.Services/         app-facing orchestration services
|   |-- CapFrameX.Next.Contracts/        DTOs shared across backend modules
|   |-- CapFrameX.Next.Web/              Angular workspace
|   |-- CapFrameX.Next.Web.Generated/    generated TypeScript contracts
|   |-- existing CapFrameX projects reused during migration
|
|-- dev-plans/
|-- overlay-templates/
|-- images/
```

Alternative for the first milestone:

```
source/
|-- CapFrameX.CefHost/
|-- CapFrameX.CefBridge/
|-- CapFrameX.WebUI/
```

Use the smaller naming set if this begins as an experiment inside the existing repository rather than a full product split.

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
- percentile charts
- sensor correlation
- aggregation tables
- export flows

Acceptance criteria:

- Results match current CapFrameX calculations.
- Chart performance is acceptable for large real-world captures.
- Export formats remain compatible.

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

Target layout:

```
CapFrameX/
|-- CapFrameX.Next.exe
|-- CEF/
|   |-- libcef.dll
|   |-- chrome_*.pak
|   |-- icudtl.dat
|   |-- locales/
|-- www/
|   |-- index.html
|   |-- runtime.*.js
|   |-- polyfills.*.js
|   |-- main.*.js
|   |-- assets/
|-- plugins/
|   |-- native/backend DLLs where needed
```

Build pipeline:

- restore NuGet packages
- build backend/host x64
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
- settings migration

### Frontend tests

- bridge client contract tests
- feature component tests for critical workflows
- chart data adapter tests
- accessibility smoke checks for dialogs, menus, and keyboard navigation

### Integration tests

- launch host
- load Angular shell
- call `app.getVersion`
- list fixture records
- open fixture record
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

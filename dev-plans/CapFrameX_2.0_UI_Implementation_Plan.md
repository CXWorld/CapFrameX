# CapFrameX 2.0 UI - Implementation Plan

Last updated: 2026-09-20

> **Scope:** the concrete, startable work plan for the CapFrameX 2.0 frontend and the backend slices
> it needs. It refines `CEF_Angular_CapFrameX_Next_DevPlan.md` (architecture, still authoritative
> for the CEF decision, bridge rules, security and performance rules; its CefSharp preference is
> replaced by section 6.1) and is driven by one visual
> reference:
>
> **Central UI mockup:** `dev-plans/mockups/capframex_redesign_mockup.html`
>
> Every view in 2.0 follows the layout, density, typography and component vocabulary of that
> mockup. Where a legacy feature has no counterpart in the mockup, section 4 defines how it is
> fitted into the mockup's pattern instead of inventing a second pattern.
>
> The overlay (in-game OSD) has its own plan: `CapFrameX_2.0_Overlay_DevPlan.md`.
>
> **Platform requirement (2026-09-20): the frontend runs on Windows and on Linux.** One Angular
> code base, one desktop shell code base, identical look on both. Platform differences live in the
> service's capability providers and in a narrow host bridge - never in feature code. Section 6
> holds the consequences; it replaces the "CefSharp first" preference of the architecture plan,
> because CefSharp is Windows-only.
>
> **Further decisions of 2026-09-20** - each has its own plan; where this document still says
> `CapFrameX.Service/...`, read the new location:
> - **Folders:** `CapFrameX.UI` (Angular app + CEF host under `host/`), `CapFrameX.Service.Windows`,
>   `CapFrameX.Service.Linux`, plus the platform-neutral `CapFrameX.Service.Shared` that both
>   services build on (D9). All backend packages of section 5 (B1-B4, Records, Analysis, Api, Data)
>   land in `CapFrameX.Service.Shared`. See `CapFrameX_2.0_Service_Architecture_DevPlan.md`.
> - **Two separately implemented services**, the Windows one always elevated:
>   `CapFrameX_2.0_Windows_Service_DevPlan.md`, `CapFrameX_2.0_Linux_Service_DevPlan.md`,
>   `CapFrameX_2.0_Linux_Telemetry_Validation_Plan.md`.
> - **`CapFrameX.OSD` is reused on Linux:** `CapFrameX_2.0_OSD_CrossPlatform_DevPlan.md`.
> - **Lightweight modes** (issue #396): section 4a.

## 0. Pre-flight - before the first feature commit

| # | Task | Why |
|---|------|-----|
| P1 | Merge `release/1.9.1` into `release/2.0.0` | 2.0.0 branched at `d7059daa` (2026-05-21) and is **364 commits behind** 1.9.1 (19 ahead). The .NET 10 migration, the hook-free OSD, the DXGI hook / Vulkan layer integration, the updater and the current `CLAUDE.md` exist only on 1.9.1. The 19 commits on 2.0.0 touch `CapFrameX.Service/`, `CapFrameX.UI/` and `dev-plans/` almost exclusively, so conflicts should be small. The overlay plan depends on this merge. |
| P2 | Fix the `CapFrameX.UI` build (see 2.1) | The workspace cannot currently run `ng build`/`ng test` cleanly: `src/assets`, `src/favicon.ico`, `tsconfig.spec.json` and all karma dependencies are missing. |
| P3 | Decide D1-D4 in section 9 | They change the shape of M1. Recommendations are given; none of them blocks M0. |

## 1. Where we are (verified 2026-09-20)

### Frontend - `CapFrameX.UI`
- Angular 18, standalone, zone.js, legacy `browser` (webpack) builder, plain CSS. 12 source files.
- One component (`app.component.ts`, inline template/styles) showing health, version, capabilities.
  Dark-only, hard-coded hex colours, no tokens, no icons, no sidebar.
- `app.routes.ts` is empty and there is no `<router-outlet>`.
- `ApiService`: `getHealth/getVersion/getCapabilities/getCaptureStatus/getRecords`, base URL
  hard-coded to `http://localhost:1337/api`. No interceptor, no error surface.
- `BridgeEventService`: `EventSource` on `/api/events`, no reconnect/backoff.
- `NamedPipeService`: stub, unused. `power-measurement.model.ts`: unused.
- `echarts`, `ngx-echarts`, `uplot` are declared but never imported.
- `src-tauri/`: default Tauri 2 scaffold from the December 2025 commit with two stub commands,
  missing icons/capabilities, and a `frontendDist` that does not match the builder output.
  Superseded by the CEF decision; kept only as the documented fallback - see 6.1 and D1.
- No tests, no lint.

### Backend - `CapFrameX.Service` (own `CapFrameX.Service.sln`, `net10.0`, not in the root solution)
- `Service.Api`: Kestrel on `localhost:1337`, Windows-service capable, CORS allowlist, **no
  authentication**. Endpoints: `/api/health`, `/api/app/version`, `/api/capabilities`,
  `/api/capture/status` (placeholder), `/api/records` (always empty), `/api/events` (SSE, heartbeat
  only). Two `TODO`s in `Program.cs`: nothing from Data, Infrastructure, Capture or Monitoring is
  registered in the host.
- `Service.Contracts`: 10 DTO records. No TypeScript generation - the Angular DTOs are hand-copied.
- `Service.Data`: EF Core 9 + SQLite (`%LOCALAPPDATA%\CapFrameX\capframex.db`), entities
  `Suite -> Session -> SessionRun`, one migration, repositories, seed data. Frame data is modelled
  as JSON text columns (`CaptureDataJson`, `SensorDataJson`, ...). **No importer for legacy capture
  files.** No mapping between entities and contract DTOs.
- `Service.Application`: empty. `Service.Infrastructure`: `InMemoryEventBus` (not thread-safe) and
  pipe servers, unregistered. The Core `IEventBus` and the Api `BridgeEventStream` are unconnected.
- **No statistics code exists in the service.**
- `Service.Capture`: real PresentMon 2.4.0 port (`PresentMonCaptureService`, Rx `FrameDataStream`),
  27 tests, not referenced by the Api.
- `Service.Monitoring`: full LibreHardwareMonitor port, not consumed by the Api.
- `Service.Input`: complete hotkey manager with 37 tests.
- Tests: xunit for Capture/Data/Input/Monitoring. None for Api/Contracts.

### Linux - `capframex-linux/` (separate code base, .NET 8)
- C daemon (game detection, process monitor, IPC server on a Unix socket under
  `~/.config/capframex/`), Vulkan capture layer `VK_LAYER_capframex_capture` (C, supports
  `VK_EXT_present_timing`), and an **Avalonia** GUI (`CapFrameX.App` / `.Core` / `.Shared`) with its
  own statistics, sysfs/hwmon hardware readers and a global hotkey service.
- Binary IPC protocol, 19+ message types (`CapFrameX.Shared/IPC/Messages.cs`, `daemon/common.h`).
- `OVERLAY_DEV_PLAN.md`: planned in-game overlay inside the Vulkan layer (ImGui, MangoHud-style).
- Consequence of the platform requirement: the Angular frontend **replaces the Avalonia GUI**, which
  is removed. The daemon is absorbed by `CapFrameX.Service.Linux`, and the capture layer is replaced
  by the Linux build of the `CapFrameX.OSD` Vulkan layer (Linux service plan, OSD plan).

### Reusable legacy code (no rewrite needed)
- `source/CapFrameX.Statistics.NetStandard` - `netstandard2.0`; `FrametimeStatisticProvider`
  (percentiles, x% low average/integral, adaptive STDEV, stuttering count/time percentage, low-FPS
  time percentage), `FrametimeAnalyzer`, `SessionExtensions` (time windows, L-shape quantiles,
  distribution points, PC latency points, animation error), filters. Depends only on
  `CapFrameX.Data.Session`, `CapFrameX.Extensions.NetStandard`, MathNet.
- `source/CapFrameX.Data.Session` - `netstandard2.0`; the capture-file object model
  (`Session`, `SessionRun`, `SessionCaptureData`, `SessionSensorData(2)`, `SessionInfo`),
  Newtonsoft-serialisable.
- Fixtures: `source/CapFrameX.Test/TestRecordFiles`.

`CapFrameX.Data` (RecordManager, directory observer) is .NET Framework 4.7.2 on this branch and
WPF-entangled; it is a reference for behaviour, not a dependency.

## 2. Mockup anatomy -> design system

### 2.1 What the mockup file is
An HTML fragment (styles + markup, no `<html>`), authored against a host design system: it uses
`--color-*` / `--border-radius-*` custom properties that are **not defined in the file**, and
Tabler icon classes (`ti ti-camera`, ...). Opened standalone it therefore renders unstyled. WP-F1
turns it into a self-contained reference page by adding the token layer below; that page is the
visual-regression baseline.

### 2.2 Layout skeleton
```
grid-template-columns: 52px | 188px | minmax(0, 1fr)      min-height 560px, base font 13px

+------+------------------+--------------------------------------------------+
| rail | context list     | workspace                                        |
| 52px | 188px            |                                                  |
|      |                  |  header: title 17px/500 + subtitle 12px  [Export]|
| CX   | [search 30px]    |  chips: hardware / feature pills (11px, 999px)   |
| (36) | RECENT (11px)    |  tabs: underline 2px, 12px                       |
| ...  | card (act)       |  chart: 150px                                    |
|      |   title 12px/500 |  stats: 4 tiles, gap 8                           |
|      |   meta  11px     |  bottom: 1.3fr | 1fr cards                       |
|      |   sparkline 20px |                                                  |
| [set]| card ...         |                                                  |
+------+------------------+--------------------------------------------------+
```
- **Rail**: logo tile 32px, nav buttons 36px with 18px icons, 2px gap, settings pinned to the
  bottom. Active = info background + info text. Order in the mockup: Capture, Analysis, Overlay,
  Comparison, Aggregation, Sensor, Report, Cloud | Settings.
- **Context list**: search field, section label (upper-case look, 11px, letter-spacing .06em),
  cards with 0.5px border, radius md, padding 9x10. Active card = info border + info background +
  a 20px frametime sparkline.
- **Workspace**: 18px padding, 14px vertical rhythm.
- Hairlines are `0.5px`; keep them (they render as 1 device pixel at 200 % and as a thin line at
  100 % in Chromium - verify in CEF at 100/125/150 %, fall back to `1px` with a lighter colour if
  they disappear).

Deliberate extensions of the mockup (documented so they are not mistaken for drift):
1. The context list is **resizable, 188-360px**, default 220px, persisted per user. 188px is the
   mockup's minimum; real record names and the multi-select aggregation workflow need more.
2. A **status bar** (24px) below the grid: service connection, capture state, active process,
   elevation state, update indicator. The mockup has no place for global state.
3. **Dark theme.** The mockup shows light; CapFrameX is used next to games. Both themes ship, the
   default follows the system.

### 2.3 Token layer
The mockup's variable names map 1:1 onto `--cx-*` tokens. Values below are the proposal for
WP-F1; they are derived from the mockup's hard-coded colours (`#185FA5`, `#D85A30`, `#FAECE7`,
`#993C1D`, `#1D9E75`).

| Mockup variable | Token | Light | Dark |
|---|---|---|---|
| `--color-background-primary` | `--cx-bg` | `#FFFFFF` | `#1A1A19` |
| `--color-background-secondary` | `--cx-bg-subtle` | `#F5F5F2` | `#242422` |
| `--color-background-info` | `--cx-bg-accent` | `#E6F1FB` | `#0F2C47` |
| `--color-text-primary` | `--cx-text` | `#1A1A19` | `#ECEBE6` |
| `--color-text-secondary` | `--cx-text-muted` | `#5F5E5A` | `#A8A79F` |
| `--color-text-tertiary` | `--cx-text-faint` | `#8E8D87` | `#77766F` |
| `--color-text-info` | `--cx-text-accent` | `#185FA5` | `#85B7EB` |
| `--color-border-tertiary` | `--cx-line` | `rgb(0 0 0 / .10)` | `rgb(255 255 255 / .10)` |
| `--color-border-secondary` | `--cx-line-strong` | `rgb(0 0 0 / .22)` | `rgb(255 255 255 / .22)` |
| `--color-border-info` | `--cx-line-accent` | `#85B7EB` | `#2F6FB0` |
| `--border-radius-md` / `-lg` | `--cx-radius` / `--cx-radius-lg` | `8px` / `12px` | same |
| (hard-coded) series | `--cx-series-1` | `#185FA5` | `#4C9BE8` |
| (hard-coded) spike | `--cx-alert`, `--cx-alert-bg`, `--cx-alert-text` | `#D85A30`, `#FAECE7`, `#993C1D` | `#F0997B`, `#4A1F12`, `#F5C4B3` |
| (hard-coded) good | `--cx-good` | `#1D9E75` | `#5DCAA5` |

**Font:** the mockup inherits the host font, which would become Segoe UI on Windows and whatever
the distribution ships on Linux - different metrics, different line breaks, screenshot tests that
can never pass on both. Bundle one UI face (Inter, OFL, variable, `woff2`, tabular figures enabled
for all numeric values) and use it on both platforms; `--cx-font: "Inter", system-ui, sans-serif`.

Additional tokens the mockup implies: type scale `11 / 12 / 13 / 17 / 19 / 20px`, weights 400/500
only, spacing `2 / 6 / 8 / 10 / 12 / 14 / 18px`, control height `30px`, rail button `36px`.
Comparison needs a categorical series palette (`--cx-series-1..8`); define it in WP-F1 and check it
for colour-vision deficiency in both themes. Tokens live in `src/styles/tokens.css`; charts read
them through a `ThemeService` so canvas renderers follow theme switches.

### 2.4 Component inventory (from the mockup)
| Component | Mockup class | Notes |
|---|---|---|
| `cx-shell` | `.cx-mock` | grid, rail, resizable list slot, workspace slot, status bar |
| `cx-rail`, `cx-rail-button` | `.cx-side` | router links, tooltip + `aria-label`, keyboard `Ctrl+1..9` |
| `cx-search-field` | `.cx-search` | debounced, `/` focuses |
| `cx-list-section` | `.cx-lbl` | Recent / by game / by date grouping |
| `cx-record-card` | `.cx-cap` | title, meta, optional sparkline, selected/multi-selected states |
| `cx-sparkline` | inline `<svg>` | SVG path from <= 64 precomputed points (see 5.2) |
| `cx-page-header` | `.cx-head` | title, subtitle, action slot |
| `cx-button` | `.cx-btn` | 30px, icon + label; variants default / primary / danger |
| `cx-chip` | `.cx-chip` | hardware + feature pills (ReBAR, HAGS, FG, ...) |
| `cx-tabs` | `.cx-tabs` | underline tabs, roving tabindex |
| `cx-stat-tile` | `.cx-stat` | label, value, unit; configurable metric |
| `cx-card` | `.cx-bcard` | titled secondary-background card |
| `cx-ring` | inline `<svg>` | percentage ring (frame pacing) |
| `cx-chart` | inline `<svg>` | canvas chart host, see 2.5 |
| `cx-chart-annotation` | spike marker | point + dashed drop line + label pill |

**UI kit decision:** own components on the token layer + **Angular CDK only** (overlay, menu,
dialog, a11y, virtual scroll, drag-drop). Angular Material's visual language does not match the
mockup and theming it down to this look costs more than the ~15 components above. This narrows the
"Angular Material / CDK" line of the architecture plan to CDK.

**Icons:** Tabler Icons (MIT), as in the mockup. Ship an SVG sprite with only the used glyphs - no
webfont, no CDN (offline rule, CSP).

### 2.5 Charts
- **uPlot** for every time/line series: frame times, FPS, L-shape, sensor overlays, live graphs.
  Canvas, ~45 kB, handles 10^5-10^6 points, which is the CapFrameX range (10 min at 500 fps =
  300k points per series).
- **ECharts** (lazy chunk) only where its layout engine pays off: Comparison bar charts, Report
  exports, distribution histogram if uPlot bars prove insufficient.
- Charts are created outside Angular's change detection; data arrives as typed arrays.
- Gate (M1): open the largest real capture available plus an 8-run comparison; pan/zoom must stay
  >= 50 fps, first paint < 150 ms after data arrival. Record the numbers in the dev log. If uPlot
  fails the gate, evaluate a WebGL renderer before building more chart features.

## 3. Frontend architecture

- **Upgrade first** (WP-F0): Angular to the current stable major, `application` builder (esbuild),
  **zoneless + signals**, SCSS off - plain CSS with custom properties is enough. Doing the upgrade
  while the app has 12 files costs an hour; later it costs a week.
- Structure:
```
src/app/
  core/            shell, routing, theme, hotkeys, error surface, host bridge (CEF actions)
  data-access/     generated DTOs + API clients, SSE/WS clients, signal stores per feature
  ui/              the cx-* component kit from 2.4 (no feature knowledge)
  visualization/   cx-chart (uPlot host), adapters, annotation layer, theme bridge
  features/        capture/ analysis/ overlay/ comparison/ aggregation/ sensor/ report/ cloud/ settings/
src/styles/        tokens.css, base.css
```
- Every feature is a lazy route; rail order = route order. `features/*` may import `ui`,
  `visualization`, `data-access`; never each other.
- State: signal stores per feature (`RecordLibraryStore`, `AnalysisStore`, ...). No global state
  library (architecture plan, section 6).
- Environment: `API_BASE_URL` and the session token are injected at start-up (`window.__CX__` set
  by the host, or `environment.ts` under `ng serve`) - remove the hard-coded URL.
- HTTP interceptor: adds the token header, maps problem-details errors onto one toast/error surface.
- `BridgeEventService`: exponential backoff reconnect, `Last-Event-ID` resume, connection state in
  the status bar.
- Delete `NamedPipeService`, `power-measurement.model.ts`; PMD data comes through the service.

## 4. Views - how every feature fits the mockup pattern

The pattern is always **rail -> context list -> workspace**. The middle column changes meaning per
view; the workspace always starts with `cx-page-header` + chips + tabs.

| Rail item | Context list | Workspace (tabs) | Legacy source | Milestone |
|---|---|---|---|---|
| **Analysis** | record library: search, Recent / Game / Date grouping, virtual scroll | **exactly the mockup**: Frame times, FPS, L-shape, Distribution; 4 stat tiles; Frame pacing + PC latency cards; Export | `DataView`, `DataViewModel` | **M1** |
| **Capture** | running processes (filtered, ignore list), selected target on top | capture settings (duration, delay, hotkey, run history/aggregation), live frametime graph, live stat tiles, logger | `CaptureView` | M2 |
| **Sensor** | sensor groups (CPU, GPU, RAM, ...) with enable toggles | live values table, per-record sensor charts and statistics | `SensorView` | M2 |
| **Overlay** | preset / profile list | editor + live preview - see overlay plan | `OverlayView` | M3 |
| **Comparison** | record library with multi-select + "Saved sets" section | bar chart / frame times / L-shape / table; Save / Save As (persistent comparison sets, architecture plan section 6) | `ComparisonView` | M4 |
| **Aggregation** | record library, multi-select | run table with outlier flags, aggregate result, save as new record | `AggregationView` | M4 |
| **Report** | record library, multi-select | metric table, column picker, copy / CSV / clipboard export | `ReportView` | M4 |
| **Cloud** | own uploads / downloads | upload, download by id, share link | `CloudView` | M5 |
| **Settings** (bottom) | categories | forms (General, Capture, Analysis, Hotkeys, Paths, Updates, Appearance) | `ColorbarView` options | grows from M1 |

Legacy views **not** on the mockup rail:
- **Synchronization** (`SynchronizationView`: display times, input lag approximation) -> tabs inside
  Analysis ("Display times", "Input lag"), not a rail item.
- **PMD** (`PmdView`) -> tab group inside Sensor, shown only when the `pmd` capability is available.
- **StateView** -> the status bar. **ControlView** (record tree, record info editing) -> the context
  list + a record detail drawer (comment, custom CPU/GPU/RAM strings, game name).

Analysis details the mockup implies but does not draw:
- **Range selection**: drag on the chart = zoom; a "Set as analysis range" action recomputes all
  tiles and cards for `[start, end]` (legacy cut/range slider). Range is part of the URL state.
- **Stat tiles are configurable** (context menu -> metric picker over `EMetric`). Mockup defaults:
  `Average`, `P95`, `OnePercentLowAverage` ("1% low"), `ZerodotOnePercentLowAverage` ("0.1% low").
  The P1/P0.1 *percentile* variants stay selectable - they are different numbers and users compare
  against both.
- **Frame pacing card**: `smooth % = 100 - stuttering time % - low-FPS time %` from
  `GetStutteringTimePercentage` / `GetLowFPSTimePercentage` (stuttering factor and low-FPS threshold
  from settings, legacy defaults). "n spikes" = stutter event count; the chart annotation marks the
  largest one.
- **PC latency card**: mean of `MsPCLatency`. Shown as "n/a" with a reason when the column is
  missing (old captures, unsupported swap chains). The mockup's caption "Click-to-photon,
  end-to-end" overstates what PresentMon measures - no peripheral latency is included. Ship it as
  "PresentMon PC latency (input to display)".
- **Chips**: CPU, GPU, RAM, then feature flags from `SessionInfo` (ReBAR, HAGS, Game Mode,
  presentation mode, resolution, API, driver) - overflow collapses into "+n".
- **Export**: menu - PNG of the current chart, CSV of the run, JSON record, copy stats.
- Multi-run records: run selector next to the tabs; aggregated view is the default.

## 4a. App modes - "lightweight" is UI configuration

Source: [issue #396](https://github.com/CXWorld/CapFrameX/issues/396). 2.0 ships lightweight modes;
they are **a reduced UI over the same application**, not a second build and not a second code path.
CPU and memory savings come from the service's on-demand policy (architecture plan, section 8):
what a mode does not show, nothing subscribes to, and what nothing subscribes to is neither
evaluated nor loaded.

| Mode | Rail | Window | Purpose |
|---|---|---|---|
| **Full** | all items | 1280x800 default, three-column layout of the mockup | benchmarking and analysis |
| **Capture** | Capture, Settings | compact single-column window (~420x560): target process, hotkey, duration/delay, state + countdown, live FPS/frametime sparkline, last runs with average / 1 % low, "open in Analysis" (switches to Full) | FrameView/FRAPS-style capturing |
| **Overlay** | Overlay, Sensor, Settings | compact window (~480x640): profile list, on/off, accent/scale quick controls, module toggles, preview | in-game overlay only |
| **No UI** | - | host not running; tray icon + hotkeys of the service | the lightest state: capture and overlay keep working |

Rules:
- A mode is a **persisted UI profile**: `{ railItems, windowLayout, startRoute }`. Switching is a
  menu entry in the rail footer and takes effect immediately - routes of hidden items are simply not
  navigable; nothing is uninstalled or disabled in the service.
- Start parameter `--mode=full|capture|overlay` for the host (shortcuts, autostart); last used mode
  is the default. Service autostart at logon never opens a window.
- The compact layouts reuse the `ui/` kit and the feature components of the full views (the Capture
  mode window is the Capture view's control column without the context list); no mode-specific
  feature code. Each compact layout gets a static mockup in `dev-plans/mockups/` first.
- Lazy routes already keep unused feature bundles from loading. Additionally, every view takes its
  live-data subscriptions in `ngOnInit`/route activation and drops them on deactivation, so an open
  Overlay-mode window never causes record indexing or analysis work.
- Status bar shows the active mode; capability reasons still apply (6.3).
- Work packages: **F9** mode profiles + switcher + start parameter (M2, with the Capture view),
  **F10** compact Capture layout (M2), **F11** compact Overlay layout (M3). Acceptance includes the
  service-side budgets of the architecture plan 8.3 measured with each mode open.

## 5. Backend work the UI needs

**Where this code lives:** everything in this section is platform-neutral and goes into
`CapFrameX.Service.Shared/src/` - `Service.Contracts`, `Service.Core`, `Service.Application`,
`Service.Records`, `Service.Analysis`, `Service.Data`, and `Service.Api` as a class library that both
hosts map. Tests go to `CapFrameX.Service.Shared/tests/`. "`Program.cs`" below means the two
composition roots, `CapFrameX.Service.Windows` and `CapFrameX.Service.Linux`; what is wired there is
identical apart from the platform modules. Nothing in this section may use an OS-specific API.

### 5.1 Host wiring (WP-B1)
- Register Data (DbContext, repositories, `DatabaseInitializer` without seed in Release),
  Infrastructure and the new Application services in `Program.cs`; delete template `Worker.cs`.
- One event path: Application publishes domain events -> an adapter forwards them to
  `BridgeEventStream`. Replace `InMemoryEventBus` with a thread-safe implementation or drop the
  Core bus in favour of `BridgeEventStream` + channels. Do not keep two unconnected buses.
- **Authentication** (currently none, and the service is meant to run as an auto-start Windows
  service): random 256-bit token generated per service start. With the service running as a child
  process of the desktop host (D6) the host generates the token and passes it to the service on
  the command line/stdin and to the UI via `__CX__` - no file needed, identical on both platforms.
  Only a stand-alone service writes it to a per-user file (Windows: ACL-restricted under
  `%LOCALAPPDATA%\CapFrameX\`; Linux: `$XDG_RUNTIME_DIR/capframex/token`, mode `0600`). Every request
  needs `X-CapFrameX-Token`; requests without it or with a foreign `Origin` get 401/403. A custom
  header also forces a CORS preflight, which closes the CSRF hole where any web page can `POST` to
  `localhost:1337` (start capture, delete record). SSE/WebSocket take the token as a query
  parameter (they cannot set headers).
- Problem-details (`application/problem+json`) for every error.
- Add `CapFrameX.Service.Api.Tests` (in `CapFrameX.Service.Shared/tests/`, part of the conformance
  suite that runs against both hosts) with `WebApplicationFactory`: every endpoint, the token rule,
  the SSE framing.

### 5.2 Record library (WP-B2)
**Recommendation (D3): legacy capture files stay the source of truth; SQLite is an index.**
The legacy JSON records are what users have, what the WPF app reads, and what 1.9.x keeps writing.
Copying frame arrays into `CaptureDataJson` doubles storage and creates a sync problem.

- Reference `CapFrameX.Data.Session` from a new `CapFrameX.Service.Records` project
  (`CapFrameX.Service.Shared/src/`). Reader for
  legacy JSON; CSV (PresentMon/OCAT) import as a second step, ported from `RecordManager`.
- Migration `AddRecordSource`: `Session` gains `SourceFilePath`, `SourceFileSize`,
  `SourceModifiedUtc`, `ContentHash`, `IndexVersion`; `SessionRun.CaptureDataJson` and friends
  become nullable and stay empty for file-backed records. Per `Session` also store
  `SparklineJson` - <= 64 min/max-decimated frametime points for `cx-sparkline` - and the duration.
- `RecordIndexer` (hosted service): initial scan of the observed directory (path from the legacy
  `AppSettings.json`, default `Documents\CapFrameX\Captures`, portable mode respected), then
  `FileSystemWatcher` with debounce; publishes `records.changed`. Index metrics with the statistics
  adapter from 5.3 so list metrics and analysis metrics cannot disagree.
- `DatabaseInitializer.SeedSampleDataAsync` becomes Development-only.
- All paths go through one `IAppPaths` service: Windows keeps today's locations
  (`%appdata%\CapFrameX\Configuration`, `%LOCALAPPDATA%\CapFrameX\capframex.db`,
  `Documents\CapFrameX\Captures`); Linux follows XDG (`$XDG_CONFIG_HOME/capframex`,
  `$XDG_DATA_HOME/capframex`, captures where the Avalonia app writes them today - verify and keep).
  No path concatenation with `\`, no case-insensitive path comparison outside `IAppPaths`.
- `DELETE /api/records/{id}` uses the platform trash (Windows recycle bin; freedesktop trash spec on
  Linux), never a hard delete. **Done**: `IFileTrash` in `Service.Core`, `WindowsFileTrash` over
  `SHFileOperation` and `XdgFileTrash` over the freedesktop layout.
- `FileSystemWatcher` on Linux is inotify-backed: watch directories, not files, handle
  `IN_Q_OVERFLOW` by falling back to a rescan, and document the `max_user_watches` limit.
- Records written by the Linux Avalonia app must load: add fixtures from `capframex-linux` to the
  reader tests and confirm the format equals the Windows JSON format (or add a second reader).
- Replace `EntityFrameworkCore.InMemory` in `Service.Data.Tests` with SQLite in-memory - the
  in-memory provider ignores relational constraints and the indexes this schema relies on.

**As implemented (2026-09-20)** - two deliberate departures from the list above:

- **No `ContentHash`.** Change detection is size plus last write time. Hashing every file means
  reading every byte of a folder that runs to hundreds of megabytes, on every scan, to answer a
  question the file system already answers; the version column covers the only case a hash would
  add, which is the projection changing rather than the file.
- **The index stored no metrics at first.** `AverageFps`, `P1Fps` and `P99Fps` stayed null until
  B3, whose adapter over `CapFrameX.Statistics.NetStandard` owns their definitions and the parity
  tests that pin them. B3 fills them through that adapter; computing them separately here is
  exactly how the list and the analysis would come to disagree.

Still open from this section: CSV import, the Linux fixtures, and the `Service.Data.Tests` provider
swap.

### 5.3 Analysis (WP-B3)
- New `CapFrameX.Service.Analysis` (`CapFrameX.Service.Shared/src/`) referencing
  `CapFrameX.Statistics.NetStandard`. No statistics
  are reimplemented; a thin adapter maps `Session` + options onto DTOs.
- **Parity tests**: for each fixture record, the adapter's numbers equal the legacy
  `FrametimeStatisticProvider` results bit-for-bit (same code path, so this guards the option
  mapping - outlier removal, range, stuttering factor - rather than the math).
- LRU cache of parsed sessions (size-bounded) so tab switches and range changes do not re-read files.

**As implemented (2026-09-20)** - `CapFrameX.Service.Analysis`, with three decisions worth keeping:

- **Three GPU metrics need the GPU-active column**, not the frame times: inside the provider they
  share a branch with `Average`, `P1` and the 1% low average, so feeding them frame times returns
  the plain metric under a GPU label.
- **Only `None` and `DeciPercentile` are offered** as outlier methods. The provider declares three
  more and implements none of them, and a setting that does nothing is worse than one that is not
  there.
- **Latency is read from the capture data** rather than through `GetPcLatencyPointTimeWindow`,
  whose sensor-data guard hides the latency of every capture recorded without hardware monitoring.

The cache is bounded by **frame count**, not entry count, and keyed by the file's size and
modification time so a rewritten capture cannot be served from an old parse.

The record list metrics from 5.2 are filled by `RecordIndex` **through this adapter**
(`AddRecordMetrics`, index version 2), which is what keeps the list and the open record from
disagreeing.

### 5.4 API surface for M1
```
GET    /api/records?search=&game=&from=&to=&sort=&skip=&take=   -> RecordsPage { items[], total }
GET    /api/records/{id}                                        -> RecordDetailDto (SessionInfo, runs[], chips)
PATCH  /api/records/{id}                                        -> comment, custom hardware strings, game name
DELETE /api/records/{id}                                        -> moves the file to the recycle bin
GET    /api/records/{id}/series?run=&kinds=frametimes,fps,...   -> SeriesResponse (columnar)
GET    /api/records/{id}/analysis?run=&start=&end=&outliers=&metrics=
                                                                -> AnalysisDto
GET    /api/settings            PATCH /api/settings             -> AppSettingsDto (analysis subset first)
```
- `RecordSummaryDto` gains `durationSeconds`, `sparkline: number[]`, `hasPcLatency`.
- `AnalysisDto`: `metrics: { key, label, value, unit }[]`, `framePacing { smoothPct, stutterPct,
  lowFpsPct, spikeCount, worstSpike { time, ms } }`, `pcLatency { averageMs, p99Ms } | null`,
  `lShape: Point[]`, `distribution: Point[]`, `thresholds`.
- `SeriesResponse` is **columnar**: `{ time: number[], frametimes: number[], ... }`. If the M1 chart
  gate shows JSON parsing on the hot path, add `Accept: application/octet-stream` returning
  little-endian `Float32` columns behind the same client method - do not build it speculatively.
- New SSE events: `records.changed { added[], removed[], updated[] }`, `settings.changed`.

**As implemented (2026-09-20)** - `GET /api/records`, `GET /api/records/{id}`, `PATCH`, `DELETE`,
`/series` and `/analysis` are in place. Four decisions behind them:

- **The detail reads the capture file**, because the runs and the full machine description are only
  in it. It carries the summary as well, so a deep link needs no list request first. A record whose
  file has gone answers 409, not an empty view.
- **A patch is a patch**: a field left out is left alone, an empty string clears it, and a patch
  that changes nothing does not rewrite the file - which would wake the watcher and re-index a
  record for no reason.
- **An edit is written into the capture file**, and the index re-reads that one record immediately
  rather than waiting for the watcher, so the next request answers with the edit.
- **`records.changed` carries counts**, not the identities the sketch above shows. The frontend
  reloads the list on it either way, and counts are what the indexer already knows.

The list takes `search`, `game`, `from`, `to`, `sort`, `skip` and `take`; `GET /api/records/games`
returns the games the index holds, which is what the filter chips are built from. `sort` is one
parameter with a leading `-` for descending, defaulting to `-created`.

Still open here: the settings endpoints and `settings.changed`.

### 5.5 Contract generation (WP-B4)
- Emit the OpenAPI document at build time from `CapFrameX.Service.Shared/src/CapFrameX.Service.Api`
  - one document for both platforms (`Microsoft.AspNetCore.OpenApi` is already referenced)
  and generate `CapFrameX.UI/src/app/data-access/generated/` with `openapi-typescript`
  (`npm run gen:api`). CI fails when the generated output differs from what is committed.
- SSE/WebSocket payload types are part of the same document (listed as schemas) so events are
  typed too. Delete the hand-written `bridge.models.ts` afterwards.

### 5.6 Later milestones (outline)
- **M2 capture/sensors**: reference `Service.Capture` and `Service.Monitoring` from the host through
  capability providers (unavailable -> capability state + reason, never a start-up failure);
  `CaptureOrchestrator` state machine (idle -> armed -> delay -> capturing -> processing), hotkeys
  via `Service.Input`, record writing in the **legacy JSON format** so 1.9.x and 2.0 stay
  interchangeable. Live data over a **WebSocket** `/api/stream`: binary batches, 10-20 Hz, frame
  metrics + sensor snapshot; SSE stays for low-frequency events. The port of `OnlineMetricService`
  supplies live averages/lows/stuttering.
- **M4 comparison sets**: `ComparisonSet` / `ComparisonSetItem` exactly as specified in the
  architecture plan, section 6.

## 6. Desktop shell and platform support (Windows + Linux)

The UI is developed and tested in a normal browser against the service (`ng serve` + service in
Development) on both operating systems. Nothing in M0-M1 depends on the shell, so the shell runs as
a parallel track.

### 6.1 Shell choice
CefSharp, the architecture plan's first preference, is Windows-only and therefore out. Candidates:

| Option | Windows | Linux | Assessment |
|---|---|---|---|
| **Native CEF host (C++, CEF Views, CMake)** | CEF | CEF (X11; Wayland through Ozone) | **Recommended.** One engine and one version on both platforms, so the mockup's hairlines, 11px type and the canvas charts render identically and one set of screenshot tests is valid for both. CEF Views supplies cross-platform windowing, so the host is a `cefsimple`-sized program plus the bridge of 6.2. It matches the NVIDIA-App pattern the architecture plan is modelled on, and C++/CMake is already in-house (OSD core, Linux daemon, Vulkan layers). |
| CefGlue (.NET binding) | CEF | CEF | Same engine benefits with a C# host, but a community binding that trails CEF releases, and its maintained variant is tied to Avalonia/WPF hosting. Fallback if a C++ host is not wanted. |
| Tauri 2 (scaffold exists in `src-tauri/`) | WebView2 (Chromium) | **WebKitGTK** | Two different engines: every layout, font and canvas-performance question is answered twice, and WebKitGTK has a history of rendering problems on NVIDIA + Wayland - exactly the hardware of this audience. Small installers are the only real advantage. Second fallback, only if CEF packaging on Linux fails WP-H1. |
| Electron | Chromium | Chromium | Rejected in the architecture plan (bundled Node.js, attack surface); the requirement does not change that reasoning. |

### 6.2 Host work packages
- **WP-H1 (CEF spike, ~1 week, both platforms):** `CapFrameX.Host` (C++/CMake, CEF binary
  distribution pinned by version + hash), custom scheme `app://capframex/` serving the Angular
  production bundle from disk, remote navigation blocked, external links to the system browser,
  `__CX__` (base URL + token) injected before page scripts. Starts the service as a child process
  and shuts it down with the window (D6).
  Measure on Windows 11 **and** on Linux (one Mesa/AMD machine, one NVIDIA machine; X11 and
  Wayland sessions): cold start, idle RAM, package size, fractional scaling at 100/125/150/200 %,
  GPU-process behaviour, clean subprocess shutdown. These numbers answer section 15 of the
  architecture plan.
- **WP-H2 (host bridge):** one interface, two implementations, exposed to the page as
  `window.cxHost` through the CEF message router; under `ng serve` a browser stub implements the
  same TypeScript interface (`core/host/`).

  | Bridge call | Windows | Linux |
  |---|---|---|
  | window minimise / maximise / close, title-bar drag region | CEF Views | CEF Views |
  | open file / folder, save file | CEF file dialog (native) | CEF file dialog (GTK / portal) |
  | reveal record in file manager | `explorer /select,` | `org.freedesktop.FileManager1.ShowItems` over D-Bus, fallback `xdg-open` on the folder |
  | open external URL | `ShellExecute` | `xdg-open` |
  | single instance + activate | named mutex + message | Unix socket in `$XDG_RUNTIME_DIR` |
  | tray icon | `Shell_NotifyIcon` | StatusNotifierItem; **optional** - stock GNOME has no tray, so no feature may depend on it |
  | autostart | Run key / scheduled task (elevation) | `~/.config/autostart/*.desktop` |

  Anything else - records, settings, capture, sensors - goes through the service API, not the bridge.
- **WP-H3:** remove `src-tauri/`, `@tauri-apps/*`, the `tauri` script and the `tauri://` CORS origin
  once WP-H1 is accepted on both platforms. Until then leave them untouched; they do not affect the
  web build and remain the documented fallback.

### 6.3 Frontend rules that keep one code base
- **Capability-driven UI.** `GET /api/capabilities` decides what is shown. A rail item or tab whose
  capability is `unavailable` stays visible but disabled with the reason from the service (no
  silent gaps between platforms); purely platform-bound surfaces - RTSS, PMD hardware, the in-game
  hook settings - are hidden where the capability can never exist. The frontend contains **no**
  platform checks outside `core/host/`.
- Bundled font (2.3), bundled icons, no system-font or system-colour dependence; theme detection
  through `prefers-color-scheme`, which CEF maps from both platforms.
- Paths are opaque strings from the service; the UI never parses, joins or normalises them.
- Hotkey display names come from the service (key names and available modifiers differ).
- Title bar: custom-drawn in the page on both platforms (drag region + window buttons from the
  bridge), so the shell looks the same under Windows, GNOME and KDE. Keep the native frame as a
  setting for Linux tiling window managers.

### 6.4 Services per platform
Superseded on 2026-09-20 by the decision to implement **two services separately** over a shared
core: see `CapFrameX_2.0_Service_Architecture_DevPlan.md` (ports, contract conformance, folder
layout), `CapFrameX_2.0_Windows_Service_DevPlan.md` (always elevated, PresentMon, PawnIO) and
`CapFrameX_2.0_Linux_Service_DevPlan.md` (presents from the Vulkan layer, kernel-based telemetry
pending validation). For the frontend only this matters: both services expose the **same API**, and
differences surface exclusively as capabilities with reasons (6.3).

M1 (records + analysis) lives entirely in `CapFrameX.Service.Shared` and is accepted on both
platforms before any capture work starts.

### 6.5 Build, test and packaging on both platforms
- CI matrix `windows-latest` + `ubuntu-latest` from M0: `ng build`, unit tests, service tests,
  Playwright suite (the screenshot baseline is shared - same engine, same bundled font).
- Line endings and case: `.gitattributes` with `eol=lf` for `CapFrameX.UI/`; imports must match
  file-name case exactly (a wrong case builds on Windows and fails on Linux).
- One solution per service (`CapFrameX.Service.Windows.sln`, `CapFrameX.Service.Linux.sln`), each
  including `CapFrameX.Service.Shared`; the Ubuntu runner never sees a Windows-only project.
- Packaging: Windows - the WiX installer gains host + service + CEF runtime. Linux - **AppImage**
  and **.deb** first (self-contained .NET service, CEF runtime, host); the daemon and the Vulkan
  layer keep their existing install script because an implicit layer needs a manifest in a system
  or user Vulkan layer directory. Flatpak is deferred: sandboxing conflicts with the layer manifest,
  the daemon socket and sensor access.
- Manual matrix per milestone: Windows 11; Ubuntu LTS (GNOME/Wayland), Fedora (GNOME/Wayland), one
  KDE/X11 system; AMD/Mesa and NVIDIA proprietary.

## 7. Milestones and work packages

### M0 - Foundation (UI shell looks like the mockup, with fake data)
| WP | Deliverable | Acceptance |
|---|---|---|
| F0 | Angular upgrade, `application` builder, zoneless, ESLint, test runner, missing files fixed, `environment` injection | `npm run build`, `npm test`, `npm run lint` green; bundle initial < 300 kB gz |
| F1 | `tokens.css` (light/dark), self-contained mockup reference page, Tabler SVG sprite | reference page renders the mockup standalone in both themes |
| F2 | `ui/` kit from 2.4 with a kit showcase route (`/dev/kit`, dev builds only) | every component has a spec; axe check passes |
| F3 | `cx-shell`: rail, resizable list slot, workspace, status bar, lazy routes for all nine views (placeholders) | keyboard navigation through rail and list; route per rail item |
| F4 | Analysis view assembled from the kit with **fixture data**, static chart | Playwright screenshot diff against the mockup reference page <= agreed threshold at 1280x800, light theme |
| B1 | In `CapFrameX.Service.Shared`: `Service.Api` as library, host wiring in both composition roots, token auth, problem-details, `Service.Api.Tests` | all endpoints covered; unauthenticated request -> 401 |
| B4 | OpenAPI -> TypeScript generation + CI drift check | hand-written DTOs deleted |
| X1 | CI matrix Windows + Ubuntu for UI and service, `.gitattributes`, service solution builds on Linux (6.5), `IAppPaths` | every M0 acceptance above holds on **both** runners |
| H1 | CEF host spike on both platforms (6.2) - parallel track, not on the M0 critical path | measurements logged; go/no-go for the native CEF host |

### M1 - Analysis on real data (read-only vertical slice)
| WP | Deliverable | Acceptance |
|---|---|---|
| B2 | `Service.Records` in `CapFrameX.Service.Shared`: legacy JSON reader, indexer, migration, `/api/records*` | a folder of real captures appears in the list within seconds; add/remove a file -> list updates through SSE |
| B3 | `Service.Analysis` in `CapFrameX.Service.Shared`: parity tests, `/analysis`, `/series` | parity tests green on all fixtures |
| F5 | `RecordLibraryStore`, virtual-scrolled list, search, grouping, sparkline | 5k records scroll at 60 fps |
| F6 | `cx-chart` on uPlot: frame times, FPS, L-shape, distribution; zoom, range, spike annotation, theme bridge | chart gate from 2.5 met and logged |
| F7 | Stat tiles (configurable), frame pacing card, PC latency card, chips, run selector, export menu | numbers equal the WPF app for the fixture set |
| F8 | Settings: appearance, observed directory, analysis options | changes persist and recompute the open analysis |

M1 is the point where the architecture plan's "read-only CapFrameX shell" (Phase 1) is reached.
It is accepted on Windows **and** Linux - it needs no platform provider (6.4); on Linux the fixture
set includes records written by the `capframex-linux` app.

### M2 - Capture + Sensor (live), app modes F9/F10 - Windows service W6-W8, Linux service L1-L2   ·   M3 - Overlay, compact Overlay layout F11 (overlay plan, OSD plan)
### M4 - Comparison (persistent sets), Aggregation, Report, Synchronization tabs
### M5 - Cloud, PMD tabs, updater surface, installer/packaging with the CEF host, accessibility and localisation pass

Each of M2-M5 gets its own work-package table when the preceding milestone is accepted; the view
contracts in section 4 and the backend outline in 5.6 fix their scope.

## 8. First ten tasks (in order)

1. P1 - merge `release/1.9.1`; then A1 of the architecture plan - create `CapFrameX.Service.Shared`,
   `CapFrameX.Service.Windows`, `CapFrameX.Service.Linux` and move today's `CapFrameX.Service/`
   projects with `git mv`, before any new backend code is written.
2. F0 - Angular upgrade + builder + zoneless + lint/test scaffolding; fix the missing files.
3. F1 - tokens + standalone mockup reference page + icon sprite.
4. B1 - register Data/Infrastructure/Application in `Program.cs`, token auth, `Service.Api.Tests`;
   X1 - CI on Windows + Ubuntu, so nothing Windows-only slips in from the first commit.
5. F2 - UI kit (start with rail button, record card, stat tile, tabs, chip, button, card).
6. F3 - shell + routes + status bar wired to `/api/health` and the SSE state.
7. F4 - Analysis view on fixture data + screenshot test.
8. B4 - OpenAPI/TypeScript generation.
9. B2 - Records project + indexer + `/api/records`.
10. B3 + F6 - analysis endpoint with parity tests, uPlot chart, chart gate measurement.

Tasks 2-3 and 4 are independent (frontend/backend) and can run in parallel; so can 5-7 and 8-9.
The CEF host spike (H1) runs beside all of them.

## 9. Decisions

| # | Decision | Recommendation | Status |
|---|---|---|---|
| D1 | Shell for Windows + Linux | **Native C++ CEF host (CEF Views)** - same engine on both platforms; CefSharp is out (Windows-only); CefGlue, then Tauri (WebView2 / WebKitGTK) as fallbacks. See 6.1. WP-H1 measurements confirm or overturn it. | open - confirm |
| D2 | UI kit: Angular Material vs. own kit + CDK | Own kit + CDK (2.4) | open - confirm |
| D3 | Record storage: frame data in SQLite vs. files + index | Files + SQLite index (5.2) | open - confirm |
| D4 | Default theme | follow system; both themes ship | open - confirm |
| D5 | Chart library | uPlot primary, ECharts lazy for bar/report charts; M1 gate decides | decided by measurement |
| D6 | Service hosting | **Decided 2026-09-20:** the Windows service always runs with administrator rights and is a **console application started headless** in the user's session - not an SCM Windows service. It is launched through a highest-run-level scheduled task (`conhost.exe --headless ...`), so there is no UAC prompt per start and no console window (Windows plan 2.1-2.2). The CEF host stays unelevated. Linux: console application as unprivileged user process, optional `systemd --user` unit. | decided |
| D7 | 2.0 ships next to the WPF app as a preview or replaces it | Parallel preview until M4 parity; record format stays shared | open |
| D8 | Avalonia GUI in `capframex-linux` | **Decided 2026-09-20:** removed completely, after salvaging protocol, hardware and record-import knowledge (Linux plan section 2). The daemon is absorbed by the Linux service; the old capture layer is replaced by the Linux build of the `CapFrameX.OSD` Vulkan layer. | decided |
| D9 | Home of the platform-neutral service core | **Decided 2026-09-20:** fourth top-level folder `CapFrameX.Service.Shared` next to `CapFrameX.UI`, `CapFrameX.Service.Windows`, `CapFrameX.Service.Linux` (architecture plan section 2) | decided |
| D10 | Portable 2D rasteriser for the Linux OSD | Blend2D first, decided by spike X1 of the OSD plan | decided by measurement |

## 10. Risks specific to this plan

| Risk | Mitigation |
|---|---|
| The 1.9.1 merge drags on | Do it first, before any 2.0 feature work widens the diff |
| Mockup covers one view; the other eight get designed ad hoc | Section 4 fixes the pattern; each new view gets a static mockup in `dev-plans/mockups/` *before* implementation, reviewed against 2.2-2.4 |
| Hairline borders / 11px text at 100 % DPI in CEF | Check in WP-H1; token-level fallback |
| Linux becomes a second-class target that breaks silently | CI on both runners from M0 (X1); every milestone is accepted on both platforms; capability reasons instead of hidden features |
| CEF on Linux: Wayland/fractional scaling/NVIDIA GPU-process problems, large packages | WP-H1 measures exactly these before any shell code is built on it; X11 (XWayland) as the safe default, Wayland via Ozone opt-in until verified |
| Two statistics implementations (service vs. `capframex-linux` `CapFrameX.Core`) disagree | the service adapter (5.3) is the only one the new UI uses; add the Linux app's fixtures to the parity tests to surface differences early |
| Statistics drift between list, analysis and legacy app | single adapter (5.3) + parity tests + indexer uses the same adapter |
| Unauthenticated localhost API shipped as a SYSTEM service | token + origin check in B1, before any mutating endpoint exists |
| uPlot insufficient for large multi-run comparisons | M1 gate with real files; WebGL fallback evaluated only if the gate fails |

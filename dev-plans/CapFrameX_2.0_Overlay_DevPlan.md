# CapFrameX 2.0 Overlay - Development Plan

Last updated: 2026-09-20

> **Goal:** give CapFrameX 2.0 a modular, tile-based benchmark overlay and an editor for it in the
> new Angular UI (rail item "Overlay", milestone M3 of `CapFrameX_2.0_UI_Implementation_Plan.md`).
>
> **Design basis:** [TroyMetrics/Benchmark-Overlays](https://github.com/TroyMetrics/Benchmark-Overlays)
> (RTSS OverlayEditor layouts, v1.15 at the time of writing). Its module structure, information
> hierarchy and state signalling are the reference for the CapFrameX overlay design.
>
> **Render target:** the CapFrameX OSD core (`cfx_osd_core`, repo `CXWorld/CapFrameX.OSD`), which
> already drives all three delivery paths - hook-free DirectComposition window, DXGI hook
> (D3D11/D3D12), Vulkan implicit layer - from one JSON widget tree. **Not** RTSS OverlayEditor:
> CapFrameX does not ship `.ovl` layouts, and the RTSS text path stays what it is today.
>
> **Platforms:** design, template schema, presets, editor **and the renderer code base**
> (`CapFrameX.OSD/shared`: scene model, widgets, Vulkan compositor) are shared between Windows and
> Linux - see `CapFrameX_2.0_OSD_CrossPlatform_DevPlan.md` and section 9.

## 0. Licensing - read before copying anything

The TroyMetrics repository has **no license file** (GitHub reports `license: none`); the README
states "All original overlay design (c) TroyMetrics". Without a license, default copyright applies:
the layouts, sprites, icons and screenshots may not be copied, modified or redistributed.

Consequences for this plan:
- We take **design principles** (modular tiles, two-tone colouring, information hierarchy, the state
  indicators listed in 1.3) - ideas are not protected. We do **not** copy `.ovl` files, sprite
  sheets, the GPU/fan artwork, icon bitmaps or pixel-exact layouts, and we do not trace them.
- All icons, gauges and art are drawn new (vector, in our own icon font / geometry widgets).
- **Action O0.1:** contact TroyMetrics before the first public build. Ask for (a) permission to
  ship a preset explicitly modelled on his overlay, credited by name, and (b) whether he wants to
  author an official CapFrameX preset. A CapFrameX-native version removes his biggest setup pain
  (MSI Afterburner beta + HWiNFO shared memory + RTSS on `C:\` + manual font install), so there is
  a real case for collaboration. Until there is a written answer, presets carry CapFrameX names
  and are visually distinguishable.
- The font **Adderley Bold** is SIL OFL according to the README. OFL permits bundling and embedding
  but requires shipping the license text and forbids selling the font on its own. **Action O0.2:**
  verify the OFL claim at the font's original source before bundling; otherwise pick another OFL
  condensed bold face (e.g. Barlow Condensed, Oswald, Saira Condensed).

## 1. Design analysis of the reference

Analysed from `Benchmark_Overlays_v1.7_Preview.jpg`, `Color_Mod_2-Tone_Preview.png` and the README.

### 1.1 Visual language
- **Tile grid**: two columns of rectangular tiles on a near-black, slightly translucent background
  (~`#0A0A0A` at ~85-90 % alpha), separated by a gap through which the game shows. No rounded
  corners, no borders, no shadows. One full-width banner tile at the bottom.
- **Two-tone colour**: one saturated accent + off-white (`~#E0E0E0`). Everything that is not
  off-white is the accent: hero number, labels, icons, bar fills, rules. Tracks/unfilled parts are
  the accent at ~20 % alpha. The whole preset is recoloured by changing **one** value.
- **Typography**: one condensed bold face, upper-case labels, generous letter-spacing on labels,
  units set smaller than values and baseline-aligned. Three sizes carry the hierarchy: hero (FPS),
  value, label.
- **Alternating emphasis** within icon rows: accent / white / white / accent - keeps a dense column
  scannable.

### 1.2 Modules
| Module | Content | Composition |
|---|---|---|
| Device header | GPU name / CPU name | centred text + 2px accent rule under the tile; flashes "GPU THROTTLE" / "CPU THROTTLE" instead of the name while throttling |
| Art tile | stylised GPU with fans | decoration only |
| Framerate | current FPS (hero, accent), unit "FPS", frame-gen indicator; below: `LOW` \| `AVG` | two sub-columns split by a vertical accent divider |
| GPU stats | usage %, power W, temperature, core clock | icon rows: ring gauge, bolt, thermometer (fire icon >= 83 C), needle speed gauge |
| Frametime | scrolling graph 0-50 ms, current value in a pill centred on the graph, label below | graph flashes red when frametime > 2x sliding average; white spike bar for frames >= 66.66 ms; with frame generation on, the graph switches to display-side times |
| VRAM / RAM | absolute value above, horizontal bar, `LABEL nn%` below | bar with dim track |
| Latency | `LAT a \| b MS` - GPU render latency and frame pipeline latency | Reflex markers preferred, PresentMon fallback |
| Timer | elapsed `h:mm:ss` | single centred value |
| CPU cores | per-core usage bar chart, adapts to 4-24 cores | bars with dim full-height tracks |
| CPU stats | usage %, power, temperature, clock, (second clock: e.g. RAM/effective) | same icon rows as GPU |
| Banner | free text ("ULTRA SETTINGS") | full width, off-white or accent |
| Power detector | 12VHPWR per-pin current, total, balance, status Normal/Warning/Danger/NA | optional module, supported GPUs only |

### 1.3 State signalling (the part worth adopting most)
Thermal throttle text swap, high-temperature icon swap, stutter flash, spike marker, frame-gen
indicator, FG-aware frametime source, power status colours, "sensor not available" states. The
overlay communicates *events*, not only numbers.

### 1.4 Presets in the reference
26 presets = 1 layout x { 4K, 1080p } x colour variants + 2 "Color Mod 2-Tone" (hue 0-100 from a
sensor value) + 2 animated rainbow + 2 power-detector-only. In CapFrameX this collapses to
**layout presets x one accent parameter x a scale factor** - colour variants are not separate files.

## 2. What CapFrameX can do better than the reference

| Reference limitation | CapFrameX |
|---|---|
| Needs MSI Afterburner beta, HWiNFO shared memory, RTSS on `C:\`, manual font install | zero setup: own sensor stack (`Service.Monitoring`), own PresentMon capture, fonts loaded from the app folder |
| Designed at 4K/300 % zoom, separate 1080p presets | resolution-independent units + user scale + automatic scale from the swap chain / monitor height |
| Core-count variants picked via an override sensor | bar count from the real topology; P/E-core and CCD grouping possible |
| Frametime with FG "may lag ~3 s due to PresentMon limitations" | own feed with `displaytimes` series already in the OSD core; FG state known from hook evidence / PresentMon frame type |
| Latency via RTSS functions | `MsPCLatency` + GPU latency from the capture service, animation error and stutter % already computed by `OnlineMetricService` |
| Colours via sensor-value hack | accent is a first-class theme parameter, editable live |
| Capture state not represented | run timer, capture countdown, run history and aggregation state are CapFrameX-native modules |

## 3. Preconditions

1. **Merge `release/1.9.1` into `release/2.0.0`** (UI plan, P1). On 2.0.0,
   `source/CapFrameX.OSD.Integration` contains only `bin/obj`; the OSD submodule, the prebuilt
   binaries, the hook and the Vulkan layer staging exist only on 1.9.1.
2. The OSD core work (section 5) happens in the **`CapFrameX.OSD` repo** and reaches this repo as
   submodule bump + prebuilt binaries, following the existing procedure in `CLAUDE.md`.
3. Live sensor and frame data in the service (UI plan, M2). The design work (O1) and the core work
   (O2) do not depend on it - the OSD demo host and the preview run on synthetic metrics.

## 4. CapFrameX overlay design

### 4.1 Principles
- Tiles, two-tone, one condensed face, no chrome - as in 1.1.
- Same module vocabulary as 1.2, plus CapFrameX modules: **Capture** (state, countdown, run n/m,
  last-run result), **Stutter / animation error**, **Run history**, **Display** (monitor refresh rate /
  VRR - an existing CapFrameX overlay entry users named as a reason to use it, issue #396).
- Accent defaults to the CapFrameX brand colour; the analysis UI's tokens are *not* reused - an
  overlay sits on arbitrary game imagery and needs far higher contrast than an app surface.
- Every module has a defined "not available" rendering (dim dash, never an empty tile or `0`).
- Legibility budget: smallest text >= 14 px at 1080p scale 1.0; contrast of off-white on tile
  >= 12:1; the accent must pass 4.5:1 on the tile - the editor warns when a chosen accent does not.

### 4.2 Presets shipped with 2.0
| Preset | Layout | Purpose |
|---|---|---|
| **CX Benchmark** | two columns, all modules of 1.2 (without art tile and power detector) | the reference-class full overlay |
| **CX Compact** | one column: FPS/LOW/AVG, frametime, GPU row, CPU row | 1080p / small screens / streaming |
| **CX Minimal** | single tile: FPS + frametime graph | always-on |
| **CX Capture** | FPS, frametime, capture module, run history | benchmarking workflow |
| **CX Classic** | today's row list | migration path for existing `OverlayEntryConfiguration_*.json` users |

Deliverable of O1: static mockups of all five in `dev-plans/mockups/overlay/` (HTML/SVG, accent as a
CSS variable so variants are one-line changes), reviewed before any core work starts.

### 4.3 Data mapping
All keys are fed by CapFrameX itself; no HWiNFO/RTSS dependency.

| Module value | Source |
|---|---|
| FPS, average, 1 % low, stutter %, animation error, PC latency | `OnlineMetricService` (port, UI plan 5.6) |
| Frametime / display-time series | PresentMon feed -> the core's batched frame push (`frametimes` / `displaytimes` series) |
| GPU render latency, pipeline latency | PresentMon 2.x latency columns (GPU latency, PC latency, render/display latency - confirm the exact column set against the pinned PresentMon build); Reflex markers only if the capture path gains them - not planned for 2.0 |
| Frame generation on/off | hook evidence (`HookTargetEvidenceProbe`) + PresentMon frame type |
| GPU/CPU usage, power, temperature, clocks, VRAM, RAM, per-core load | `Service.Monitoring` |
| Thermal throttle flags | Monitoring: Intel package/ring throttle bits, AMD PROCHOT, NVIDIA perf-cap reason (thermal), AMD/Intel GPU equivalents - **gap, verify per vendor in O2** |
| Device names | `SystemInfo`; user-overridable short names ("R9 9950X3D") |
| Timer, capture state, run n/m | `CaptureOrchestrator` |
| 12VHPWR per-pin current | only via vendor-specific sensors on a few cards, or PMD/Benchlab totals - **out of scope for 2.0**, module slot reserved |

## 5. OSD core gap analysis

Current core (`CapFrameX.OSD/src/core`, verified 2026-09-20): widgets `panel` (vertical stack,
rounded background), `row`, `text`, `metric` (thresholds good/warn/bad), `chart` (raw-D3D11
polyline, series `frametimes`/`displaytimes`), `gauge` (arc), `icon` (glyph from an **installed**
icon font), `spacer`; theme with panel/text/accent/good/warn/bad colours, one font family, pad, gap,
radius; anchor + margins + scale; per-frame `Animator`; preview into a child HWND
(`cfx_osd_preview_*`); two scene sources - JSON template with keyed metrics, and the entry list
used by CapFrameX today.

| # | Needed for the tile design | Gap | Work |
|---|---|---|---|
| G1 | two-column tile grid, tiles spanning columns, fixed gaps, square corners | `panel` is a vertical stack only | new `grid` container: columns, per-child `colSpan`, `gap`, equal-height rows; `panel` gets `radius`, `padding`, `background` overrides (radius 0) |
| G2 | horizontal composition inside a tile (LOW \| AVG, icon + value + unit) | `row` exists but is tied to the label/value column model | new `hstack` with alignment + `divider` widget (vertical/horizontal rule, accent) |
| G3 | hero number with small baseline-aligned unit and an indicator slot | `metric` renders label + value in one style | `metric` options: `valueSize`, `unitSize`, `labelPosition: none/below/left`, `indicatorKey` |
| G4 | VRAM/RAM bars | none | `bar` widget: value key, max key or constant, track alpha, height |
| G5 | per-core bar chart with dynamic bar count | none | `barchart` widget: indexed key family (`cpuCoreLoad[i]`), count from a metric, optional grouping gaps |
| G6 | ring gauge and needle speed gauge | `gauge` draws one arc style | `gauge.style: ring / needle`, `min`/`max` (user thresholds for clock gauges), stroke width |
| G7 | pill with the current value on the frametime graph; spike bar; stutter flash | `chart` has title/unit only | `chart` options: `valuePill`, `spikeThresholdMs` (default 66.66), `stutterFactor` (default 2.0, sliding average) -> flash colour via `Animator`; fixed `min`/`max` already exist |
| G8 | FG-aware series switch | series is static per widget | `series: auto` - `displaytimes` while metric `frameGenActive` = 1, else `frametimes` |
| G9 | state-driven text/icon/colour (throttle text, fire icon, blinking FG icon, n/a rendering) | only numeric thresholds on `metric` | small declarative `states` list on any widget: `{ when: { key, op, value }, set: { text, glyph, color, blink } }`; first match wins. No expression language |
| G10 | bundled fonts (text + icon font) that also work inside an injected game process | fonts must be installed system-wide | DirectWrite custom font collection from files/memory next to the DLL; the CPU text raster path (`CpuTextRaster`) must use the same collection. Icon set: own vector glyphs compiled into `cfx-osd-icons.ttf` |
| G11 | one-value recolouring, optional hue animation | accent exists; tracks/dim variants and hue cycling do not | theme: `accent`, `onTile`, `tile`, `trackAlpha`; derived colours computed in the core; `accentCycleSeconds` (0 = off) through the `Animator` |
| G12 | text with placeholders (`{gpuName}`, banner text) | `text` is static; `set_metric_text` exists | `text.key` binding to a text metric |
| G13 | timer formatting | none | `metric.format: "time"` |
| G14 | template versioning | `"version": 1` | templates of this plan are `"version": 2`; v1 and the entry-list scene keep working unchanged |
| G15 | preview inside the Angular/CEF UI | preview needs a parent HWND (built for WPF `HwndHost`) | see 6.2 - built on the existing CPU frame producer (`cfx_osd_create_cpu`), identical on both platforms (OSD plan section 6) |

Rules for the core work: no per-frame allocations in the new widgets, bars/gauges are Direct2D
geometry (not sprites), the dense line chart stays on the raw-D3D11 layer, and every new widget
works in all three delivery paths - the in-game hook has no guaranteed Direct2D path in every
route, so each widget needs the same fallback treatment the existing ones have (check
`hook_poc/README.md` before designing G4-G6).

### 5.1 Template sketch (version 2)
```json
{
  "version": 2,
  "anchor": "top_left", "marginX": 24, "marginY": 24, "scale": "auto",
  "theme": { "tile": "#0A0A0AE0", "onTile": "#E2E2E2FF", "accent": "#3DA9FCFF",
             "trackAlpha": 0.2, "fontFamily": "CX Condensed", "iconFont": "cfx-osd-icons",
             "gap": 8, "pad": 14, "radius": 0 },
  "root": { "type": "grid", "columns": 2, "children": [
    { "type": "panel", "underline": "accent", "children": [
      { "type": "text", "key": "gpuName", "size": 34,
        "states": [ { "when": { "key": "gpuThrottle", "op": "==", "value": 1 },
                      "set": { "text": "GPU THROTTLE", "color": "bad", "blink": true } } ] } ] },
    { "type": "panel", "children": [
      { "type": "metric", "key": "fps", "valueSize": 96, "unit": "FPS", "unitSize": 28,
        "color": "accent", "labelPosition": "none", "indicatorKey": "frameGenActive" },
      { "type": "hstack", "children": [
        { "type": "metric", "key": "fps1pctLow", "label": "LOW", "labelPosition": "below" },
        { "type": "divider" },
        { "type": "metric", "key": "fpsAvg", "label": "AVG", "labelPosition": "below" } ] } ] },
    { "type": "panel", "children": [
      { "type": "chart", "series": "auto", "min": 0, "max": 50, "valuePill": true,
        "spikeThresholdMs": 66.66, "stutterFactor": 2.0 },
      { "type": "text", "text": "FRAMETIME", "color": "accent" } ] },
    { "type": "panel", "children": [
      { "type": "bar", "key": "vramUsed", "maxKey": "vramTotal" } ] },
    { "type": "panel", "children": [
      { "type": "barchart", "keyFamily": "cpuCoreLoad", "countKey": "cpuCoreCount" } ] },
    { "type": "panel", "colSpan": 2, "children": [ { "type": "text", "key": "bannerText", "size": 44 } ] }
  ] }
}
```

## 6. Editor in the Angular UI (rail item "Overlay")

### 6.1 Layout - follows the central UI mockup
- **Context list**: presets ("Shipped") and the user's profiles ("My overlays"); card = name +
  small static thumbnail. Profile slots 0/1/2 of the legacy app map onto three pinned profiles so
  the existing profile hotkeys keep working.
- **Workspace**: `cx-page-header` (profile name, Duplicate / Export / Import), chips (active
  delivery path: hook-free / in-game hook / Vulkan layer, target process), tabs:
  **Design** (preview + accent picker + scale + position/anchor + banner text + short device
  names), **Modules** (toggle, reorder via CDK drag-drop, per-module options such as chart range,
  gauge min/max, temperature threshold), **Behaviour** (hotkeys, show only while capturing, refresh
  rate, delivery-path preferences), **Classic** (the legacy entry list for `CX Classic`).
- Edits apply live to a running overlay (debounced) and are saved explicitly (Save / Revert) -
  same dirty-state model as comparison sets.

### 6.2 Preview
The existing preview renders into a child HWND. Options:

| Option | Assessment |
|---|---|
| **A. Offscreen render in the core -> PNG/BGRA over the service** (new C API next to `cfx_osd_preview_*`) | **Recommended.** Pixel-identical to the real overlay, works in a browser under `ng serve`, no window-embedding problems in CEF. 10-15 Hz is enough for an editor. |
| B. Native child HWND placed over a placeholder rectangle in the CEF host | pixel-identical and 60 Hz, but fragile (z-order, scrolling, DPI, popups over it) and unavailable in browser development |
| C. HTML/CSS twin renderer | fast to build, but a second renderer drifts from the real one - rejected as the editor preview; it is fine for the O1 *design mockups* |

API: `POST /api/overlay/preview` (template JSON, size, background: checkerboard / dark / light /
sample screenshot) -> `image/png`; synthetic metrics with a "stress" toggle (throttle, stutter, FG
on) so every state of 1.3 can be seen without provoking it in a game.

### 6.3 Service API
```
GET/PUT/DELETE /api/overlay/profiles[/{id}]     profile = preset id + overrides (accent, scale, modules, texts)
GET            /api/overlay/presets             shipped templates (read-only)
POST           /api/overlay/preview             see 6.2
GET            /api/overlay/state               delivery path, target pid, hook status, last error
POST           /api/overlay/active              { profileId, visible }
SSE            overlay.stateChanged
```
Endpoints, profile model (`OverlayProfiles` in `Service.Application`) and the preview endpoint live
in `CapFrameX.Service.Shared`; applying a profile goes through the `IOverlayBackend` port, implemented
in `CapFrameX.Service.Windows` and `CapFrameX.Service.Linux`.

Profiles are stored as files next to the legacy overlay configuration
(`%appdata%\CapFrameX\Configuration\`, portable mode respected), not in SQLite - users share and
back them up as files. A profile stores **overrides against a preset**, not a full template copy, so
preset updates reach existing profiles.

## 7. Phases

| Phase | Scope | Acceptance |
|---|---|---|
| **O0** Legal + font | O0.1 contact TroyMetrics; O0.2 verify font license or pick replacement; icon glyph list | written answer filed in `dev-plans/`; font + license text in the repo |
| **O1** Design | static mockups of the five presets (4K and 1080p scale, three accents, all states of 1.3, n/a states); module spec sheet (sizes in design units, type scale, icon set) | review sign-off; contrast checks pass; mockups in `dev-plans/mockups/overlay/` |
| **O2** Core widgets (= OSD plan X6, written once against `Canvas` for both platforms) | G1-G14 in `CapFrameX.OSD/shared/core`; demo host shows `CX Benchmark` on synthetic metrics; v1 templates and entry-list scenes render unchanged | golden-image tests per widget; frame cost of `CX Benchmark` in the hook-free path within the current budget (measure before/after, log in the OSD repo); all three delivery paths verified in one D3D11, one D3D12 (FG on/off) and one Vulkan title |
| **O3** Data + service | metric keys of 4.3 published by the service; throttle flags per vendor; profile storage + API (6.3); offscreen preview (G15, option A) | `CX Benchmark` live in a game with real data; unavailable sensors show the n/a state |
| **O4** Editor | Overlay view per 6.1 | preset -> customise accent/modules/scale -> save -> restart -> restored; live apply < 200 ms; keyboard-only operable |
| **O5** Migration + polish | legacy `OverlayEntryConfiguration_*.json` -> `CX Classic` profiles; auto-scale by resolution; docs; optional hue cycle | existing users see their overlay unchanged after upgrade; round-trip without data loss |

O1 can start immediately and in parallel with UI milestones M0/M1. O2 can start after the 1.9.1
merge and O1 sign-off; it is independent of the Angular work. O3/O4 land with UI milestone M3.

## 8. Risks

| Risk | Mitigation |
|---|---|
| Design too close to an unlicensed work | O0.1 first; own artwork; CapFrameX names; keep the correspondence |
| New widgets behave differently in the in-game routes (D3D12 generic route, Vulkan layer) than in the hook-free window | every widget verified in all three paths in O2, not at the end |
| Richer overlay costs frame time inside the game | geometry cached per layout, redraw only on value change at sensor cadence, chart stays on the raw-D3D11 layer; measure in O2 |
| Custom font loading inside foreign processes (sandboxed/anti-cheat titles) | load from memory, no system font installation, fall back to Segoe UI + built-in glyph geometry |
| Throttle/FG detection differs per vendor | capability per signal; module shows n/a instead of guessing |
| Preview drifts from reality | single renderer (6.2 option A) |

## 9. Linux

Superseded approach: an earlier revision of this section planned a separate ImGui renderer inside
the old `capframex-linux` capture layer. **Requirement 2026-09-20:** the `CapFrameX.OSD` code,
above all its Vulkan layer, is reused for Linux. Details in
`CapFrameX_2.0_OSD_CrossPlatform_DevPlan.md`; what it means for this plan:

- **One renderer code base.** Scene model, widgets, layout, JSON templates, animation and the
  Vulkan compositor are shared between Windows and Linux (`CapFrameX.OSD/shared`). Linux differs in
  two places only: the 2D rasteriser behind the `Canvas` interface (portable CPU rasteriser instead
  of Direct2D) and the IPC shim (memfd + Unix socket instead of named mappings).
- **The gap list of section 5 is implemented once**, against `Canvas`, in `shared/core` - phase O2
  of this plan is phase X6 of the OSD plan and delivers the widgets to both platforms at the same
  time. There is no O2-L.
- **Same templates, presets, fonts, metric keys, profiles** on both platforms; profiles are
  file-compatible and live under `$XDG_CONFIG_HOME/capframex/` on Linux.
- **Same preview.** The offscreen preview of 6.2 (option A) is built on the core's CPU frame
  producer and therefore works identically on Linux; no platform-specific preview capability.
- **Delivery paths on Linux:** the Vulkan layer only - which, through Proton, also covers D3D9-12
  titles (their Vulkan calls reach the Linux loader). No hook-free desktop window and no OpenGL
  path in 2.0.
- Modules whose data source does not exist on a platform render their n/a state or are removed by
  the editor based on capabilities (no PC-latency module on Linux; throttle text only where the
  telemetry validation finds a signal).
- `capframex-linux/OVERLAY_DEV_PLAN.md` is obsolete and is archived with the Avalonia removal.

Remaining platform risk: text metrics differ slightly between Direct2D and the portable rasteriser.
Layout always uses the metrics of the active backend and only bundled fonts, and the golden-image
tolerance is fixed in OSD phase X1.

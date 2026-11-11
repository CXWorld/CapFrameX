# Hook-free overlay: repaint and present only what changed

Dev plan and session prompt, written 2026-11-09. Start a new session in `E:\Code\CapFrameX` with:

> Read `dev-plans/HookFree_Partial_Repaint_DevPlan.md` completely and carry it out, starting with
> section 5 "Before you change anything".

Everything below is addressed to that session. It has none of the earlier conversation, so this file
holds what was measured, what was agreed with the user, where the code is and how to measure. Line
numbers are those of 2026-11-09 (OSD repo `main` at `ece67a6` plus uncommitted changes, CapFrameX
`release/1.9.2` at `9855474a`); look for the function when a number is off.

**Status (2026-11-10):** Phases 0 to 2 are implemented, tested and committed in both repositories
(OSD `e80ab37`); one capture ran in the game (section 16.8), the measurement series of section 16.6
is still to do. Section 16 at the end says what was
built, what the probe answered, what the repaints cost now and how to run the measurement. Sections
1 to 15 are the plan as it was written and describe the state before.

## 1. The task

The hook-free overlay is a topmost window CapFrameX draws itself, without injecting into the game.
While it shows a tile design, the game's display times become jagged. In Doom: The Dark Ages (RTX
5090, FSR frame generation, GPU at 97 %) the user's FUSION design raised the share of display
intervals above 1.25 times the median from 0.95 % with the overlay off to between 4.7 % and 9.2 %.
The measured cause: whenever the overlay draws and presents, the game frame in flight takes longer
on the GPU.

The user's bar:

- Fewer spikes is not enough. Ideally they disappear entirely.
- The tile overlay is the benchmark, not the classic rows.
- Drawing on the CPU (WARP) removes most of the effect, but a repaint of the whole FUSION panel then
  takes about 8 ms instead of 1.4 ms on the GPU. The user does not accept that as the fix.

Agreed with the user on 2026-11-09, nothing of it is implemented:

1. **Repaint only when something visible changed, at most once per OSD refresh.** Before changing
   behavior, add counters to the core's 10 s log line that show how many repaints each feed and each
   reason causes. That split is not known today.
2. **Keep what is already drawn, redraw only the tiles that changed, and present only those
   rectangles, drawing on the CPU.** The root swapchain has to change for that: it is
   `FLIP_DISCARD`, which allows no partial presents.
3. Proposed, **not agreed**: choose the default device (hardware or WARP) by measurement. Do not
   start this without the user's go (section 11).

Measure every step with the FUSION design, several runs per variant, against "overlay off" in the
same session. Two runs of one variant differed by a factor of two to four on 2026-11-09, so a single
run proves nothing.

## 2. When it is done

- **Display times.** FUSION visible in the hook-free window, at least 5 runs of 20 s per variant,
  interleaved with at least 5 "overlay off" runs of the same session: the variant's mean of
  `>1.25x`, `stdev`, `P1` and `slips/s` (columns of the compare script, section 12) lies inside the
  minimum..maximum range of the off runs, and `--tick` shows no lock to the overlay's 100 ms tick
  (share in a 20 ms slot and strongest tick position like the off runs).
- **Repaints.** The core's 10 s line shows at most one content repaint per OSD refresh for FUSION,
  idle and during a capture, what caused each repaint, and what one costs.
- **Picture.** A partial repaint leaves exactly the pixels a full repaint would (test), and no tile
  shows a value older than one OSD refresh. Hide and show, resize, zoom, design switch, pixel orbit,
  full-width designs, classic rows and the Hybrid starters still work.
- **Builds and tests.** All five native trees build (core, `hook_poc` x64 and x86, `vk_layer` x64
  and x86: they compile the shared widget and scene code), CTest is green, the CapFrameX unit tests
  are green.
- If the display times do not reach the off range after Phase 2, say so with the numbers and name
  what remains. Section 4 explains why the device choice may be that remainder.

## 3. What is known (measured on 2026-11-09)

Doom: The Dark Ages, RTX 5090, FSR frame generation, 20 s per capture, hook-free renderer, overlay
visible during the capture. Display interval median 7.56 to 7.60 ms, GPU time of a real frame
(median of `GpuActive`, generated frames carry none) 15.05 ms. Columns as the compare script prints
them: `stdev` and `p99` of `MsBetweenDisplayChange` in ms, `>1.25x` share of display intervals above
1.25 times the median, `P1` 1 % percentile FPS, `slips/s` frames per second whose GPU time exceeds
the median by more than 1.2 ms.

Classic rows (21:08 to 21:10):

| Variant | Capture comment | stdev | p99 | >1.25x | P1 | slips/s |
|---|---|---|---|---|---|---|
| hardware device | `Hook-free A baseline` | 0.92 | 10.92 | 3.12 % | 91.6 | 5.15 |
| hardware, GPU priority -7 | `Hook-free B GPU priority -7` | 0.95 | 10.88 | 3.39 % | 92.1 | 4.70 |
| WARP | `Hook-free C WARP` | 0.71 | 10.35 | 1.67 % | 95.9 | 2.55 |
| overlay off | `Hook-free D overlay off` | 0.73 | 10.39 | 1.62 % | 96.3 | 2.60 |

FUSION tile design (21:14 to 21:17):

| Variant | Capture comment | stdev | p99 | >1.25x | P1 | slips/s |
|---|---|---|---|---|---|---|
| hardware, run 1 | `FUSION E1 hardware` | 1.48 | 11.63 | 9.17 % | 85.7 | 10.60 |
| hardware, run 2 | `FUSION E2 hardware` | 0.97 | 10.95 | 4.68 % | 91.2 | 8.91 |
| WARP, run 1 | `FUSION W1 WARP` | 0.66 | 9.73 | 1.10 % | 102.7 | 2.50 |
| WARP, run 2 | `FUSION W2 WARP` | 1.12 | 11.22 | 4.37 % | 89.1 | 6.35 |
| overlay off | `FUSION session, overlay off` | 0.61 | 9.27 | 0.95 % | 107.9 | 1.75 |

The two off runs differ (1.62 % and 0.95 %, P1 96.3 and 107.9), so numbers only compare within one
session. The FUSION session has a single off run, so its noise range is unknown.

Further findings:

- **Cost per overlay tick, hardware device, classic rows.** A game frame hit by a root repaint took
  about 1.9 ms more GPU time, one hit by a chart-only tick about 0.5 ms, although the overlay's own
  GPU work for a chart update is 0.02 ms. Without overlay the same analysis shows about 0.1 ms. So a
  second GPU client that presents on a saturated adapter costs the game about 0.5 ms per present,
  whatever it draws.
- **GPU priority -7** leaves the slips and starves the overlay: its own work then waits up to 260 ms.
- **WARP** does the overlay's drawing on the CPU. Only DWM's composition of the overlay is left on
  the GPU. With classic rows WARP is indistinguishable from overlay off.
- **Own drawing cost.** Classic rows: a root repaint 0.65 ms on the GPU, 2.7 ms on WARP; a chart
  update 0.02 ms and 0.4 ms. FUSION: a root repaint about 1.4 ms on the GPU, about 8 ms on WARP, the
  longest tick about 10 ms.
- **Repaints per 10 s** (`repaints=` of the core's `tick 10s` line): classic rows exactly 10, also
  during a capture. FUSION 33 to 40 idle and 41 to 59 during a capture.
- **Why FUSION repaints more often.** The design feed is not bound to `OSDRefreshPeriod` (1000 ms
  here). `OverlayDesignRuntimePublisher` publishes on every telemetry snapshot (the sensor poll,
  which switches to `SensorLoggingRefreshPeriod`, 500 ms here, while a capture logs sensors) and on
  its own 500 ms heartbeat whenever the metrics JSON differs. The entry feed (once per OSD refresh)
  marks the panel dirty too. Every one of these sets `contentDirty_` in the core without comparing
  anything, and every `contentDirty_` repaints the whole root.
- **What a repaint redraws.** FUSION is 98 widgets (45 panels, 24 bars, 14 metrics, 10 texts, 3
  images, 1 chart, 1 heatmap) on 476 x 1020 px = 486 k px. The pixels that can change (digits of the
  14 metrics, the 24 bars with 22.4 k px, the heatmap with 46.9 k px) add up to about 106 k px, 22 %.
  This is an estimate from the design's frames, not a measurement.
- **Tick lock** (`--tick` of the compare script): in `FUSION W2 WARP` 45 % of the frames with GPU
  excess fall into one 20 ms slot of a 99.98 ms period, the strongest tick position shows 1.78 ms
  mean excess, the median position 1.01 ms. Overlay off: 33 %, 0.54 ms, 0.39 ms.
- **MPO.** The game stays on its own plane (`LayerIndex` 1, `Hardware: Independent Flip`) while DWM
  composes the overlay into the desktop plane.
- **The chart** of FUSION runs in a separate small child swapchain (`SteppedChartLayer`) at 10 Hz.
  Its lines never repaint the root. FUSION's chart has no value badge (`currentValue` false).

## 4. What is not known

Treat these as hypotheses. Each has a measurement in the phases below.

1. **The split of FUSION's repaints** over telemetry snapshots, the heartbeat, the entry feed and
   animations. Phase 0 measures it. If the animator restarts on every value change, a part of the 33
   to 59 repaints are animation frames, not feed updates.
2. **That the slips scale with the number of root presents.** Expected from the cost per tick, never
   measured with a tile design at a lower repaint rate.
3. **Why WARP is not reliably clean with FUSION.** `FUSION W2 WARP` had as many slips as the
   hardware device for its first 13 s (8 to 13 per second), then almost none. One hypothesis: every
   full-frame present of a WARP swapchain makes DWM upload the whole 476 x 1020 x 4 = 1.9 MB buffer
   on the game's adapter, so a partial present would shrink exactly that. Another: DWM reassigned
   planes after the overlay had just been recreated (CapFrameX `CLAUDE.md`, "MPO check for users":
   after an overlay appears DWM may compose the game for a while). Neither is proven.
4. **Whether the hardware device can reach "off" at all.** Classic rows on the hardware device have
   one root repaint per second and still 5.15 slips/s against 2.60, because the chart layer presents
   ten times per second on the game's adapter. Steps 1 and 2 do not touch that. So measure hardware
   and WARP after every phase: the likely outcome is that steps 1 and 2 make WARP both clean and
   cheap, which is the argument for step 3.
5. **How DXGI treats partial presents here.** Whether `Present1` with dirty rectangles works on a
   composition swapchain, on WARP, and whether the next back buffer is up to date outside the dirty
   rectangles. Phase 2 starts with a probe.
6. **The cost of a partial repaint.** About a fifth of 8 ms by area share. Not measured.

## 5. Before you change anything

1. Read the OSD repo's `E:\Code\CapFrameX.OSD\CLAUDE.md`, at least "Render stack" (line 50), "Data
   flow" (157), "Performance model" (331), "Debug/diagnostics" (357) and "Hook-free stall
   diagnostics" (415). Two statements in it are stale: "There are no tests in this repository yet"
   (CTest targets exist, section 13) and the "hard 1 Hz present ceiling" (the code has
   `kHookFreeMaxPresentHz = 10.0`, `RenderPacing.h:12`). CapFrameX's `CLAUDE.md` is loaded
   automatically; "Hook-free stall diagnostics", "MPO warning" and "Overlay renderer selection"
   matter here.
2. Check the branch pairing: CapFrameX `release/1.9.2` goes with OSD `main`.
3. Run `git -C E:\Code\CapFrameX.OSD status --short`. The working tree is dirty on purpose:
   - `CapFrameX.OSD/vk_layer/src/layer.cpp`, `layer_log.cpp`, `osd_feed.cpp/.h`,
     `vk_compositor.cpp`, `vk_layer_priv.h`, `CapFrameX.OSD/hook_poc/src/overlay_frametime_shm.cpp/.h`:
     the fix for the frame generation collapse when the Vulkan layer hides the overlay at a capture
     start (the layer keeps its semaphore chain; debug switch `vkNoHideChain` restores the old
     behavior). The user confirmed it in the game. It is in neither a commit nor the prebuilts.
   - `CapFrameX.OSD/src/core/GraphicsDevice.cpp/.h`, `OsdInstance.cpp/.h`, `ScrollChart.cpp/.h` and
     the untracked `GpuSpanTimer.h`: the debug switches `hookFreeWarp` and `hookFreeGpuPriority` and
     the `rootGpu=` / `chartGpu=` fields of the 10 s line. This plan builds on them.

   Never stash, reset, check out or clean these files. Your work touches `OsdInstance.cpp/.h` and
   `GraphicsDevice.cpp/.h` as well, so **ask the user first whether to commit the existing changes**
   (recommended: one commit for the layer fix, one for the hook-free diagnostics), so the new work
   stays separable. Run `git diff` to see what belongs to which.
4. Build the unchanged core and run CTest (section 6), so you know the baseline is green.

## 6. Workspace

### Repositories

| | Path | Branch | State on 2026-11-09 |
|---|---|---|---|
| CapFrameX | `E:\Code\CapFrameX` | `release/1.9.2` | clean except this plan and its script (untracked) |
| OSD (private) | `E:\Code\CapFrameX.OSD` | `main` | dirty, see section 5 |

Native sources: `E:\Code\CapFrameX.OSD\CapFrameX.OSD\src\core\`. Test apps:
`E:\Code\CapFrameX.OSD\CapFrameX.OSD\apps\`.

### Build

```
cd E:\Code\CapFrameX.OSD\CapFrameX.OSD
cmake --preset vs2026
cmake --build build --config RelWithDebInfo --target cfx_osd_core --parallel
cmake --build build --config RelWithDebInfo --parallel
ctest --test-dir build -C RelWithDebInfo
```

Output: `CapFrameX.OSD\build\bin\RelWithDebInfo` (`cfx_osd_core.dll`, `cfx_osd_demo.exe`, the test
executables). The hook and Vulkan layer trees and their x86 variants: OSD `CLAUDE.md`, "Hook-based
overlay" (492) and "Vulkan overlay layer" (618). Build them before you call the work done, but do
not install the layers (see the rules).

The CapFrameX app stages the core from that build folder into its output
(`source\CapFrameX\CapFrameX.csproj` 120-125), whatever its age. The app was run from the Debug
output on 2026-11-09: `source\CapFrameX\bin\x64\Debug\net10.0-windows\`. After a core build, with
CapFrameX closed, either build the app
(`msbuild source\CapFrameX\CapFrameX.csproj /p:Configuration=Debug /p:Platform=x64 /p:VisualStudioVersion=18.0`)
or copy `cfx_osd_core.dll` and its `.pdb` into that folder. Compare the SHA-256 of the build output
and the app output before every measurement; a stale core has cost hours before. On 2026-11-09 both
were `4FF522C14C3FEDF0…`; the Release app output (`5009DF18…`, 2026-11-01) and the prebuilt
(`C324F637…`, 2026-11-07) are older and lack the debug switches.

CapFrameX runs elevated and the capframex MCP server only answers while it runs. Ask the user to
start it and to be in the game before each measurement block.

### Logs

- Core log: `%TEMP%\cfx-osd-logs\cfx_osd.log`, or `cfx_osd_<pid>.log` when another process loaded
  the core first (on 2026-11-09 the hook-free log was `cfx_osd_30648.log`). The header names the
  process and PID; check it. `[diag]` lines need the verbose switch. The file is held open, so
  directory listings show a stale size; read it through a shared handle:

  ```powershell
  $fs = [IO.File]::Open($log, 'Open', 'Read', 'ReadWrite,Delete'); $sr = New-Object IO.StreamReader($fs); $text = $sr.ReadToEnd(); $sr.Close()
  ```
- `[create] hook-free device: hardware|WARP (CPU), GPU thread priority N` tells which device an
  instance uses (`OsdInstance.cpp:300`).
- `[diag] tick 10s: hostCalls=… chartTicks=… repaints=… rebuilds=… holds=… snaps=… slowTicks=…(max …ms)
  hostGaps=… topmost=… setWindowPos=… playT=… lag=… delay=… wave=… frames=… rootGpu=N(avg …ms max …ms)
  chartGpu=N(avg …ms max …ms)` is the line Phase 0 extends (`finishTick`, `OsdInstance.cpp:1765`).
- `CapFrameX.log`: `HookFree OSD: …` (managed render loop) and `HookFree feed: …`
  (`HookFreeFeedDiagnostics`, one summary per 10 s).

### Debug switches

`%APPDATA%\CapFrameX\Configuration\OsdDebug.json`, read when a hook-free instance is created
(`OsdInstance::create`, line 286; the struct is `OsdDebugConfig`, line 58). Content on 2026-11-09:
`{"presentStats": true, "verboseLog": true}`.

- `verboseLog`: the `[diag]` lines (same as "Extended OSD logging" on the Overlay tab).
- `hookFreeWarp` (bool): create the hook-free device as WARP.
- `hookFreeGpuPriority` (-7..7): GPU thread priority of the hardware device. -7 starves the overlay.
- `hookFreeHz`: chart tick ceiling. `hideMode`: forces a hide method.

A hide is a soft hide and keeps the instance, so a changed switch only applies after the instance
was recreated: select another renderer and hook-free again (then re-activate the tile profile,
because selecting renderer Off clears it) or restart CapFrameX. Confirm with the `[create]` line.

### Scratch material (git-ignored, on this PC only)

`E:\Code\CapFrameX\temp\hookfree-partial-repaint\`:

- `fusion_design.json`: the user's active profile "FUSION - RTX 5090 + Core Ultra 9 285K" as saved
  (710 kB, bound to this PC's sensors). Root panel: canvas layout, 338 x 725.4 design units, on
  screen 476 x 1020 (scale 1.408). Do not commit it. The same profile comes from the capframex MCP
  tools `cfx_list_tile_profiles` and `cfx_get_tile_profile`. The unbound starters are in
  `E:\Code\CapFrameX\overlay-templates\` (`RTX-50-Core-Ultra-FUSION-ARL.json` and two more).
- `fusion_census.py`, `fusion_area.py`, `fusion_digits.py`: widget counts and the area estimates of
  section 3 (they read `fusion_design.json` beside them).
- `demo_smoke.ps1 [-DebugJson '{…}'] [-Seconds 25]`: runs `cfx_osd_demo.exe` with a private
  `OsdDebug.json` (it points `APPDATA` at `demo-appdata\` beside it for the demo process), stops it
  after the given time and prints the `[create]` and `tick 10s` lines of its log, which it finds by
  the PID in the header: `cfx_osd.log` when no other process holds the core log, else
  `cfx_osd_<pid>.log`. Run on 2026-11-09 with the defaults: `hook-free device: hardware`, and per
  10 s `repaints=100 chartTicks=100 rootGpu=100(avg 0.34..0.42ms)`, because the demo sets its
  metrics in every loop iteration, so every 10 Hz tick repaints the root.
  The demo takes `<maxFrames> [preview|entries]` and no design file; an optional design argument
  would let you drive FUSION without CapFrameX and the game. Its loop is not paced (about 4.3
  million iterations per second, one busy core), so a positive `maxFrames` ends it within a second:
  leave it at 0 and stop the process, as the script does. ESC held anywhere ends it too.

Python is `C:\Python314\python.exe`.

## 7. Code map

OSD repo, `CapFrameX.OSD\src\core\`:

| Where | What |
|---|---|
| `OsdInstance.cpp:1432` `tick` | The hook-free frame. Gate at 1497-1515: without animation a tick only continues when `contentDirty_`, `structureDirty_` or new chart samples exist. Then `snapshotSharedData`, a scene rebuild or `growDynamicValueCapacity` + `relayout` (1540-1568), the chart layer (1682-1689), the root decision (1696-1698), Direct2D draw (1710-1716), `gfx_.present(0)` (1743). |
| `OsdInstance.cpp:1696` | `chartsInRoot = hasTimedCharts_ && !steppedCharts && !marqueeActive_`, `repaintRoot = !mainFramePresented_ \|\| contentChanged \|\| rebuild \|\| layoutGrew \|\| animationWasActive \|\| chartsInRoot \|\| scene_.hasChartBadges`. This is the condition steps 1 and 2 change. |
| `OsdInstance.cpp:700` `snapshotSharedData` | Under `feedMutex_`: returns `contentDirty_`, and copies `shared_` into `data_` only when it is set. A real difference must therefore always set it. |
| `OsdInstance.cpp:527`, `533` | `setMetric`, `setMetricText`: set `contentDirty_` without comparing. |
| `OsdInstance.cpp:947` `commitEntriesLocked` | The entry feed (`updateEntries` 975, `updateEntriesV2` 996, `endEntries` 1021): sets `contentDirty_`. |
| `OsdInstance.cpp:1339` `applyDesignJson`, `1393` `setMetricsJson` | The design feed: template and/or metrics JSON; sets `contentDirty_`. |
| Other dirty sources | `setGraphSource` 560, `requestLayoutRebuild` 780, `setFontFamily` 788, `requestFullWidthRelayout` 842, `setHidden` 1241 (show), `setPosition` 1306, `clearTemplate` 1379, `loadDefaultTemplate` 1418. `applyBackgroundAlpha` 765 changes theme alphas every tick from `bgAlpha_`; find out how a changed alpha reaches a repaint today. |
| `OsdInstance.cpp:1292` `presentTransparentFrame` | The hide by one empty frame; sets `mainFramePresented_ = false`, so the next visible tick repaints everything. |
| `OsdInstance.cpp:1765` `finishTick` | Counts into `tickStats_` and prints the 10 s line. `tickStats_` belongs to the tick thread; the feed functions run on other threads under `feedMutex_`. |
| `OsdInstance.cpp:1206` `syncScrollCharts` | Decides where charts are drawn. The default is the stepped child layer (`steppedChartLayer_.configure`, 1229); charts inside foreground layers (`hasInlineCharts`) stay in the root. |
| `OsdInstance.cpp:2000` `bgProduceFrame`, `1815` `renderExternal` | The in-game producers (hook, Vulkan layer). They repaint on `textPeriodMs_` whatever changed. Out of scope; they must keep working. |
| `Diagnostics.h:239` `TickStatistics` | `hostCalls`, `chartTicks`, `repaints`, `rebuilds`, `holds`, `snaps`, `slowTicks`, … Also `PeriodicTrigger`, `RateLimiter`, `PhaseTimer`. Pure logic, tested by `apps/diagnostics_test`. |
| `Widget.h:129` `Widget` | `id`, `RectF bounds` (set in `arrange`), `measure`, `arrange`, `drawChartLayer`, `draw`. No dirty concept. Classes: `PanelWidget` 149 (`children`, `clipChildren`, `drawBackground`), `ClassicRowsWidget` 178, `TextWidget` 187, `MetricWidget` 201 (`formatPrimary`, `formatSecondary`, private `colorFor`, `disp_`), `HeatmapWidget` 261, `BarWidget` 280, `RowWidget` 307, `ChartWidget` 417, `GaugeWidget` 508, `IconWidget` 534, `ImageWidget` 545. |
| `Widget.h:97` `updateDisplayValue` | Value smoothing: `k = dt * 12`, clamped to 1. `draw` mutates this state. With draws at least 84 ms apart the displayed value equals the target, which is always the case in the hook-free window at its 10 Hz ceiling. |
| `Widget.h:108` `RenderContext` | `r`, `chart`, `gfx`, `theme`, `data`, `dt`, `valueDt`, `smoothValues`, chart window fields. |
| `Widget.cpp` draw functions | `PanelWidget::draw` 320, `TextWidget::draw` 358, `MetricWidget::draw` 423, `BarWidget::draw` 560, `HeatmapWidget::draw` 689, `RowWidget::draw` 986, `ChartWidget::buildChartPoints` 1121 (sets `lastMin_`/`lastMax_`), `ChartWidget::draw` 1354, `GaugeWidget::draw` 1453. |
| `Scene.h:12`, `Scene.cpp:118` | `hasInlineCharts`, `hasChartBadges` (any chart with `currentValue`), `hasMultiLineCharts`, `hasCountingCharts`. |
| `Renderer.h:28` `Renderer` | `begin`, `end`, `clear`, `pushOpacity`, `popLayer`, `d2d()` (an `ID2D1RenderTarget*`), draw helpers, cached text layouts. No clip helper yet. |
| `GraphicsDevice.cpp` | `createCommon(bool useWarp)` 26. `fillSwapDesc`: B8G8R8A8, `BufferCount` 2, `DXGI_SCALING_STRETCH`, `DXGI_SWAP_EFFECT_FLIP_DISCARD`. `create`: DirectComposition device from the DXGI device, target for the HWND, root visual whose content is `CreateSwapChainForComposition` (premultiplied alpha). `createTargets`: buffer 0 becomes `backRtv_` and the Direct2D target bitmap, once. `resize`, `present` (`Present(syncInterval, 0)`). `fillSwapDesc` is shared with `createForChildWindow` (388) and `createCompositionSwapChain` (408, the chart layer and the marquee). |
| `ScrollChart.h:20` `SteppedChartLayer` | The chart child visual with its own small composition swapchain. |
| `CpuTextRaster.h:27` | Direct2D on a CPU bitmap; the tests draw scenes with it. |
| `GpuSpanTimer.h` (untracked) | Times a span of the overlay's own GPU work for `rootGpu=` and `chartGpu=`. |
| `RenderPacing.h` | `kHookFreeMaxPresentHz = 10.0`, `HookFreeRepaintIntervalSeconds`, `ConsumeRepaintSlots`. |
| `osd_c_api.cpp` | `cfx_osd_set_metric` 113, `cfx_osd_update_entries` 197, `cfx_osd_tick` 249, `cfx_osd_set_metrics_json` 340, `cfx_osd_apply_design_json` 345. |

CapFrameX repo:

| Where | What |
|---|---|
| `source\CapFrameX.OSD.Integration\OsdOverlayBridge.cs` 218-237 | `_designSub = _designRuntime.Snapshots.Subscribe(snapshot => { UpdateFrameFeedRequirements(…); ApplyDesignSnapshot(snapshot); })`: every design snapshot goes to the core at once. |
| same file, `OnEntries` (about 340) | Runs once per OSD refresh: `ApplyDesignSnapshot(_designRuntime?.Current)`, then `_osd.UpdateEntries(list)` (also with a design loaded), `ApplyDiagnosticsSwitch`, feed diagnostics. |
| same file, `ApplyDesignSnapshot` | `newDesign = !_customDesignLoaded \|\| snapshot.DesignRevision != _lastDesignRevision`; returns when neither the design nor `MetricsRevision` changed; else `_osd.ApplyDesignJson(newDesign ? template : null, metricsJson)`. A snapshot without a design clears the template. |
| `source\CapFrameX.OSD.Integration\OverlayDesignRuntimePublisher.cs` | `MetricsRevision` 23, the 500 ms heartbeat 68 and 112, telemetry subscription 162, `OnTelemetry` 251, `UpdateSnapshot` 274. It also feeds the hook and the Vulkan layer; leave its cadence alone. |
| `source\CapFrameX.OSD.Integration\HookFreeFeedDiagnostics.cs` | The `HookFree feed:` 10 s summary in `CapFrameX.log`. |

Settings on this PC: `OSDRefreshPeriod` 1000, `SensorLoggingRefreshPeriod` 500, `UseSensorLogging`
true, `MetricInterval` 5. Keep them during measurements so the baselines compare.

## 8. Phase 0: counters and a baseline

No behavior change. The goal is a table "FUSION repaints per 10 s by source and by reason", idle and
during a capture.

1. **Dirty marks per source.** Count where `contentDirty_` and `structureDirty_` are set: metric
   (`setMetric`/`setMetricText`), entries (`commitEntriesLocked`), design metrics (`applyDesignJson`
   without a template, `setMetricsJson`), design template, other. These run on feed threads, so keep
   the counters under `feedMutex_` and take them over into the summary under the lock.
2. **Repaint reasons.** Count per root repaint which terms of `repaintRoot` were true (first frame,
   content, rebuild, layout grew, animation, charts in root, badges), plus the repaints whose only
   reason was content. Find out what restarts `animator_` (`Animator.h/.cpp`).
3. **Cost per repaint on the wall clock.** `rootGpu=` comes from GPU timestamps. Add the average and
   maximum of the draw phase and the present phase of repaint ticks (the `tickTimer_` marks
   `kPhaseDraw` and `kPhasePresent`), so hardware and WARP compare on one clock.
4. Append the new fields to the `tick 10s` line (keep the existing ones in place) and describe them
   in the OSD `CLAUDE.md`, "Hook-free stall diagnostics". Put counting logic that is pure into
   `Diagnostics.h` and test it in `apps/diagnostics_test`.
5. Optional, cheap: let the bridge count `ApplyDesignJson` calls by trigger (snapshot subscription or
   `OnEntries`) and by kind (template or metrics only) in the `HookFree feed:` summary.
6. **Baseline** (section 12): FUSION idle for a minute, then 5 rounds of off / hardware / WARP. Note
   the split from the log and the script's table in this file or a results file beside it.

## 9. Phase 1: no repaint without a visible change, one content repaint per OSD refresh

### 9.1 One metrics update per OSD refresh (CapFrameX, `OsdOverlayBridge`)

Let the snapshot subscription apply only what changes the structure at once (a new or changed
template, a cleared design) and leave metrics-only snapshots to `OnEntries`, which already applies
`_designRuntime.Current` once per OSD refresh. The publisher, the heatmap averages
(`OverlayMovingAverages`) and the in-game paths stay as they are. The visible consequence is the
agreed one: tile values in the hook-free window change once per `OSDRefreshPeriod`, as the classic
rows always did, instead of up to five times per second.

If the rule cannot be tested through the bridge, extract it as a pure helper (the repo does this
throughout, for example `HookFreeHideMethods.Choose`) and test that in `CapFrameX.Test`.

A minimum content interval inside the core would do the same for every host. It is the alternative
if the bridge change turns out awkward; do not build both.

### 9.2 Drawn-state signatures (OSD core)

Comparing raw values at the feed helps little: sensor readings change in the last digit on every
poll, and the heartbeat already filters identical JSON. What matters is whether a tile would draw
differently. Give every widget kind a signature of what it draws, computed from the target values
in `data_` without calling `draw` (which mutates smoothing state):

- `MetricWidget`: the formatted primary and secondary strings and the resolved color.
- `BarWidget`: the fill extent in device pixels, the value string, the missing flag, the color.
- `HeatmapWidget`: per cell the quantized color or the missing flag.
- `RowWidget`, `ClassicRowsWidget`: the cell strings, colors and bar extents.
- `GaugeWidget`: the sweep in device pixels and the text.
- `ChartWidget`: the scale labels (min/max or grid line labels) and, with `currentValue`, the badge
  text. Evaluate it after the chart layer rendered, because `buildChartPoints` sets `lastMin_` and
  `lastMax_`.
- Static kinds (panel, image, icon, spacer, a text without a source): nothing.

Requirements:

- `draw` and the signature must share the code that turns a value into what is drawn (the formatted
  string, the pixel extent, the color), so they cannot drift apart. Refactor `draw` to use the same
  helper; do not copy the logic.
- A stored signature describes what is on the screen. Update it only when the tile was painted and
  the present succeeded.
- A header-only hasher avoids the CMake problem of section 13.
- The signature assumes the displayed value equals the target. That holds at the 10 Hz ceiling
  (`updateDisplayValue`), but check what `hookFreeHz` allows; if draws can come closer than 84 ms,
  a smoothed widget whose value has not arrived must stay dirty.
- The in-game producers must not be affected: they do not skip repaints.

### 9.3 The root decision

In `tick`, after the snapshot and the layout, walk the tree when the content changed and collect the
rectangles (`Widget::bounds`) of the tiles whose signature differs. Replace `contentChanged` in
`repaintRoot` by "at least one tile differs". Keep the unconditional `scene_.hasChartBadges` repaint
in this phase (a badge changes on almost every tick; Phase 2 turns it into a small update). Count
ticks with changed content, skipped repaints, changed tiles and their area in the 10 s line.

Expected: FUSION shows at most 10 repaints per 10 s, idle and during a capture. Then measure
(section 12). This tests hypothesis 2 of section 4.

## 10. Phase 2: redraw only the changed tiles, present only their rectangles

### 10.1 Probe first

Before any refactoring, write a small test app (or a mode of an existing one) that answers, for a
hardware and a WARP device:

1. Does `Present1` with dirty rectangles succeed on a composition swapchain created with
   `DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL`, `BufferCount` 2? (`FLIP_DISCARD` does not support partial
   presents according to the DXGI documentation; confirm it with the HRESULT.)
2. After a partial present, what does buffer 0 hold outside the dirty rectangles: the previous frame
   (DXGI brought it up to date) or the frame before it? Read it back through a staging texture over
   several frames. The documentation suggests the first for flip-model swapchains, but this was
   never verified here, least of all for composition swapchains and WARP.
3. Does `IDCompositionSurface` (`IDCompositionDevice::CreateSurface`, `BeginDraw(updateRect)`,
   `EndDraw`, `Commit`) work on both devices? It is a retained surface with native partial updates
   and no swapchain, at the price of a bigger change to `GraphicsDevice` (draws go to an atlas
   surface with an offset; the raw D3D11 chart path for charts in the root needs a solution).

Write the answers into this file. They decide between the options:

- **A (preferred if the probe allows it):** root swapchain `FLIP_SEQUENTIAL` and `Present1` with the
  dirty rectangles. If the back buffer is not up to date outside them, either redraw the dirty
  rectangles of this and the previous frame, or copy the previous frame's rectangles from the front
  buffer first; pass only this frame's rectangles to `Present1`.
- **B:** `IDCompositionSurface`, if A fails on a composition swapchain or on WARP.
- **C (weakest):** draw with `CpuTextRaster` and upload the dirty rectangles into the hardware
  swapchain. The overlay then still is a GPU client that presents on the game's adapter, which
  section 3 measured at about 0.5 ms per present.

Give the root its own swapchain description. `fillSwapDesc` also serves the chart layer and the
child window path; leave those as they are.

### 10.2 Experiment before the redraw work: a partial present of a full redraw

Behind a debug switch (for example `hookFreePartialPresent`), keep the full redraw and pass the
changed tiles' rectangles of Phase 1 to `Present1`. Rendering is deterministic, so the pixels outside
them equal the previous frame. Measure in the game on WARP and on the hardware device. This isolates
the size of the present from the cost of drawing and tests hypothesis 3. If it changes nothing on
WARP, report to the user before you continue: the partial redraw then only saves CapFrameX's own CPU
time, which is still the user's second complaint (8 ms per repaint), but it will not move the
display times.

### 10.3 Clipped redraw

For a partial repaint: `renderer_.begin()`, then per dirty rectangle (snapped outward to device
pixels) `PushAxisAlignedClip(rect, D2D1_ANTIALIAS_MODE_ALIASED)` on `renderer_.d2d()`, clear to
transparent (a Direct2D clear is limited to an axis-aligned clip), draw, `PopAxisAlignedClip`. Add
the clip helpers to `Renderer`. Draw through a traversal that skips every subtree whose bounds miss
the rectangle; panels that intersect it draw their background and descend. Check that no widget
draws outside its `bounds` (glow, shadows, overhanging images such as the Anime starters' character
image, text overhang); where one does, its dirty rectangle must cover what it draws.

A full repaint and a full present stay necessary for: the first frame and after
`presentTransparentFrame` (`!mainFramePresented_`), a scene rebuild, `layoutGrew`, the intro fade and
every running animation (`pushOpacity`), charts in the root, a resize, a zoom or scale change, a
changed background opacity or theme, and a failed or lost device. `Present1` with zero dirty
rectangles presents the whole frame.

Replace the unconditional `hasChartBadges` repaint by the chart's signature from 9.2: a badge then
costs a small update of its own rectangle.

Extend the 10 s line: full and partial repaints, the average dirty area, and the wall-clock cost of
each kind. Expected on WARP: a FUSION update well under 2 ms instead of 8 ms. Then measure.

## 11. Phase 3: the default device (not agreed, ask first)

If steps 1 and 2 make WARP clean and cheap, the open decision is whether the hook-free window uses
WARP by default. It needs measurements the earlier session did not make: FUSION and classic rows over
several runs each, a CPU-limited game (WARP costs CPU time on CapFrameX's render thread), an AMD and
an Intel GPU, and a fallback when WARP creation fails. Today `hookFreeWarp` is only a debug switch.
Present the results of Phases 1 and 2 to the user and let them decide.

## 12. Measuring

### Setup

- Doom: The Dark Ages (`DOOMTheDarkAges.exe`), frame generation on, the scene the user chooses with
  the GPU near its limit. The user stands still and does not touch the PC during a block; the game
  keeps the foreground.
- The capframex MCP tools are deferred; load them with ToolSearch. Useful: `cfx_ping`,
  `cfx_get_osd_options`, `cfx_set_osd_options` (`renderer` Rtss/InGame/HookFree/Off,
  `isOverlayActive`, `autoDisableOverlay`), `cfx_list_tile_profiles`, `cfx_activate_tile_profile`,
  `cfx_list_processes`, `cfx_start_capture` (`processName`, `captureSeconds`, `comment`),
  `cfx_wait_for_capture_completion`, `cfx_set_record_comment`.
- `autoDisableOverlay` must be false, or the capture hides the overlay it is meant to measure.
- "Overlay off" is `isOverlayActive: false`. Selecting renderer Off also works but clears the active
  tile design.
- After every show, renderer switch or device switch wait at least 20 s before a capture: DWM may
  compose the game for a while after an overlay appears.

### Runs

20 s per capture, at least 5 per variant, interleaved (off, hardware, WARP, off, hardware, WARP, …),
because the load drifts within a session. Name the variants by the capture comment, for example
`P1 off 3`, `P1 FUSION hw 3`, `P1 FUSION warp 3` (P0 for the baseline). Captures land in the
configured capture folder, at present `C:\Users\CapFrameX\Documents\CapFrameX\Captures\The Witcher 3`
(whatever its name says).

### Evaluation

`dev-plans\HookFree_Partial_Repaint_compare.py` (its docstring explains every column):

```
python dev-plans\HookFree_Partial_Repaint_compare.py --list
python dev-plans\HookFree_Partial_Repaint_compare.py --pattern "CapFrameX-DOOMTheDarkAges.exe-2026-11-1*.json" "P1 off *" "P1 FUSION hw *" "P1 FUSION warp *" --tick
```

It prints one row per run and, per variant, the mean, minimum and maximum over its runs. `--tick`
adds the present modes and layers of each run (discard runs in which the game left its own plane)
and the lock of the GPU excess to the overlay's 100 ms tick. Run without arguments it reproduces the
tables of section 3 from the captures of 2026-11-09, which is how the script was checked.

For every block also read the `tick 10s` lines of the core log over the same minutes: repaints, their
reasons, their cost.

Derived timeline, should you need more than the script: the present start of frame i is
`TimeInSeconds[i+1] * 1000 - MsInPresentAPI[i]`, its display time that plus `MsUntilDisplayed[i]`.
With frame generation only the first frame of a pair carries `GpuActive`.

## 13. Tests

CTest (from `CapFrameX.OSD\CapFrameX.OSD\CMakeLists.txt`, 226-295): `design`, `chart_gpu_raster`,
`chart_gpu_raster_warp`, `chart_raster`, `row_alignment`, `graph_source`, `framerate_graph`,
`value_smoothing`, `replay_buffer`, `chart_geometry`, `hook_free_pacing`, `overlay_position`,
`overlay_position_live`, `diagnostics`, `color_management`, `hdr_composite`.

- `apps/design_test/main.cpp` compiles the core sources with friend access (`DesignTestAccess`:
  `scene`, `data`, `bindRenderer(instance, CpuTextRaster&)`), loads designs from inline JSON
  (`loadSceneFromJson`) and checks with `require(bool, const char*)`. New tests belong here.
- **CMake caveat:** `raster_test`, `row_alignment_test`, `chart_perf_bench`, `framerate_graph_test`,
  `chart_gpu_raster_test` and `chart_raster_test` list individual `.cpp` files. A new `.cpp` that
  `Widget.cpp` or `Scene.cpp` needs has to be added to each of them; a header avoids that.

Tests to add:

1. **Signature soundness.** For every widget kind and for whole designs: feed random values to every
   key the design reads, render on `CpuTextRaster`; whenever a tile's signature is unchanged between
   two value sets, its pixels inside its bounds are identical. This is the test that no visible
   change is ever skipped.
2. **Partial equals full.** Over a random sequence of value sets, a chain of partial repaints leaves
   the same bitmap as a full repaint of the last set, byte for byte.
3. **Full repaint cases** of 10.3 each force one.
4. The counters' pure logic in `diagnostics_test`.
5. CapFrameX: the bridge's cadence rule (9.1). Run
   `vstest.console source\CapFrameX.Test\bin\x64\Release\net10.0-windows\CapFrameX.Test.dll /Platform:x64 /TestCaseFilter:TestCategory!=Integration`.

Inline designs keep the committed tests small. For a local run over the real FUSION design, read a
design file named by an argument or environment variable; do not commit the 710 kB profile.

## 14. Documentation, memory, prebuilts

- OSD `CLAUDE.md`: "Render stack" (retained root, partial present, the full repaint cases),
  "Performance model" (the measured cost per present and the corrected 10 Hz ceiling), "Hook-free
  stall diagnostics" (the new fields), and the stale "no tests" line.
- CapFrameX `CLAUDE.md`: the cadence of the hook-free design feed (a short section or an addition to
  "Hook-free stall diagnostics").
- Memory `hookfree-warp-removes-spikes` in
  `C:\Users\CapFrameX\.claude\projects\E--Code-CapFrameX\memory\`: results per phase.
- Record measured results and the probe's answers in this file as you go, so a later session can
  continue from it.
- `external\CapFrameX.OSD-prebuilt\` is not part of this work. Tell the user at the end that the
  release needs new prebuilts (a clean build, the README entry with the source revision and hashes)
  and that the Vulkan layer fix of section 5 is still missing there as well.

## 15. Rules

- Chat with the user in German. Every artifact is English: code, comments, log texts, documents,
  commit messages, memory.
- Commit or push only when the user asks. Commit messages are one short line, the user is the only
  author, no `Co-Authored-By` trailer, one commit per topic.
- Never register the Vulkan layer in HKCU and never re-register it in HKLM. A rebuilt layer is
  tested by replacing the DLLs in `C:\Program Files\CapFrameX\vulkan` (and `vulkan\x86`), which
  needs the user's approval each time. This work does not need it.
- A game that kept running across CapFrameX restarts keeps its first injected hook; irrelevant for
  the hook-free window, relevant if you compare with the in-game renderer.
- Report measurements as they are, including runs that contradict the expectation, and keep "known"
  and "assumed" apart as this file does. Do not claim an effect from a single run.
- Code style of the repo: backing fields grouped at the top of a class, XML doc policy per file
  (complete docs where a file has them), translations only from CxLang.
- Keep an on-screen task list and stop for the user at the decisions this plan names: committing the
  existing OSD changes (section 5), the result of the experiment in 10.2, and Phase 3.

## 16. What was done (2026-11-09) and what is open

The user's decisions: the existing OSD changes were committed first (`82eaeaf` the Vulkan layer
fix, `25659fc` the hook-free diagnostics; local, not pushed), and the game is measured once, at the
end, with switches that turn each phase off (section 16.6) instead of after every phase. The user
also asked that the work hold for every tile design, not only FUSION: the mechanism is per widget
kind and the tests run over every starter design.

### 16.1 The probe (section 10.1)

`apps/partial_present_probe` (OSD repo, built with the core, not a CTest), RTX 5090 and WARP,
Windows 11 build 26300. It shows a 256 x 128 topmost window with the hook-free window's styles,
reads the back buffer through a staging copy and the screen through GDI. Both devices answered
alike:

1. `FLIP_DISCARD`: `Present1` with a dirty rectangle returns `0x887A0001` (`DXGI_ERROR_INVALID_CALL`).
2. `FLIP_SEQUENTIAL`, 2 buffers: `Present1` with one and with two dirty rectangles succeeds. DXGI
   does not bring the next back buffer up to date: before every frame it held the frame before the
   one on screen. The screen was right all the same, also where the presented buffer was stale
   outside its rectangles: DWM took only the rectangles from it. The code does not rely on that.
3. With the back buffer brought up to date first (what the previous present changed is copied from
   buffer 1, the whole buffer after a whole frame), the back buffer held the frame on screen before
   every partial frame, and the screen was right.
4. `IDCompositionSurface` (`CreateSurface`, `BeginDraw` with an update rectangle, `EndDraw`,
   `Commit`) works too; the update lands at an atlas offset such as (275, 17).

So option A was built, with the copy of answer 3 (`GraphicsDevice::syncBackBuffer`).

### 16.2 What was built

OSD repo (`CapFrameX.OSD\src\core`; details in its CLAUDE.md, "Hook-free root: retained surface,
partial repaint" and "Hook-free stall diagnostics"):

- **Phase 0.** `Diagnostics.h`: `DirtySource`/`DirtyMarkCounts`, `RepaintReason`,
  `RepaintStatistics`. Every content mark goes through `OsdInstance::markContentLocked`. The
  `tick 10s` line gained `marks=`, `content=N(unchanged M)`, `reasons=`, `contentOnly=`, `full=`,
  `partial=` (wall-clock draw and present, tiles, rectangles, pixels) and `mode=`. Finding: the
  animator only runs the 0.35 s intro fade after the first scene build; a value change never
  restarts it, so none of FUSION's repaints were animation frames.
- **Phase 1.** `TileSignature.h` and `Widget::contentSignature` / `drawnSignature` for metric, bar,
  heatmap, row, gauge and chart, built from the same helper `draw()` paints from. `TileRepaint.h`
  finds the changed tiles; `RenderPacing.h` (`PlanRootRepaint`) decides. Content that changes no
  tile repaints nothing.
- **Phase 2.** The root swap chain is `FLIP_SEQUENTIAL` (`createRootSwapChain`); `syncBackBuffer`
  and `presentRegion` in `GraphicsDevice`. `Renderer` records what a tile paints
  (`Widget::ink`) and what it paints from its values (`Renderer::ValueScope`, `Widget::valueInk`);
  a changed tile clears its `valueInk` and whatever reaches into that rectangle is drawn again
  under a pixel clip. The value badge of a chart is a tile change like any other, and charts whose
  lines share the root are repainted as tiles with every chart tick instead of with the whole
  root.
- Not in the plan, found on the way: scaled images are copied from a placed copy
  (`Renderer::placedImage`), because a scaled `DrawBitmap` came out one step lighter or darker
  under another clip (the four Slick starters failed the chain test); `measureText` caches its
  results; an inline chart builds its lines once per repaint and before its frame.
- `OsdDebug.json` `"hookFreeRepaint"`: `"legacy"` (the former rule and `FLIP_DISCARD`), `"skip"`
  (Phase 1 alone), `"partialPresent"` (the experiment of 10.2), `"partial"` (default). Read at
  creation and at every show; the root swap chain is replaced when the mode needs the other kind.

CapFrameX repo:

- **9.1.** `HookFreeDesignFeed.Decide` (pure) and `OsdOverlayBridge.ApplyDesignSnapshot`: a design
  that is new, changed or removed is applied at once, values alone with `OnEntries` (once per
  `OSDRefreshPeriod`) and at a show. `"hookFreeRepaint": "legacy"` also restores the former cadence
  (`AppliesEverySnapshot`). The `HookFree feed: 10s` summary ends with `design=[...]`.

### 16.3 Tests

- OSD CTest: 15 of 16 pass. `chart_gpu_raster` fails ("retracing a line must not accumulate
  opacity") as it did on the unchanged tree before this work; the WARP variant passes.
- `design` (`apps/design_test/partial_repaint.h`): chains of random value sets, painted partially
  on one surface and fully on another, compared after every step. Six inline designs (every widget
  kind with overlapping tiles, a label wider than its tile, a clipped panel, a centred value;
  a grid; an inline chart; scaled images; linked classic rows; a full-width canvas), each on the CPU
  raster with 15 % and with 100 % of the readings changing, on WARP and on the hardware device.
  With `CFX_OSD_TEST_DESIGNS=E:\Code\CapFrameX\overlay-templates;...\fusion_design.json` 114
  chains ran: all 29 starter templates that the native loader accepts and the FUSION profile, on
  all three surfaces. CPU raster and WARP: identical bytes in every step. Hardware (RTX 5090):
  identical except for single antialiased corner pixels of rounded panels (Anime Cherry Blossom 1
  pixel by 3 of 255, BLUELINE up to 2 pixels by 5); the test allows 8 levels on 32 pixels for the
  hardware device only. Two negative controls were run by hand: culling by bounds instead of the
  recorded ink failed the chains at the overhanging label; a signature without its colour is caught
  by the added "colour change alone names the tile" check.
- `diagnostics` (statistics, dirty rectangles), `hook_free_pacing` (`PlanRootRepaint`: every case
  of section 10.3 forces a whole repaint).
- All five native trees build: core, `hook_poc` x64 and x86, `vk_layer` x64 and x86.
- CapFrameX: `HookFreeDesignFeedTest`. Unit test run: see the session's report.

### 16.4 What a repaint costs now (no game involved)

FUSION profile (`fusion_design.json`) at the size it has in the game (476 x 1022).

`cfx_osd_design_test --repaint-bench fusion_design.json 1.408 <percent>` paints 300 random value
sets and waits for the rasterization (median, ms; the CPU-heavy paths vary between runs with the
cores Windows gives the process):

| Readings that move per step | Device | Whole root | Changed tiles | Tiles | Rectangles | Share of the surface |
|---|---|---|---|---|---|---|
| 90 % | hardware | 2.0 | 1.35 | 30 | 17 | 19 % |
| 90 % | WARP | 6.1 to 9.3 | 2.6 to 2.9 | 30 | 17 | 19 % |
| 30 % | hardware | 2.0 | 0.73 | 11 | 9 | 12 % |
| 30 % | WARP | 6.6 | 1.13 | 11 | 9 | 12 % |
| 10 % | WARP | 6.3 | 0.62 | 4 | 4 | 8 % |

The plan expected "well under 2 ms" on WARP. With nearly every reading moving it is 2.7 ms: the
cost follows the number of changed tiles (about 0.085 ms each on WARP, 0.04 ms on the hardware
device) more than their area. Merging the rectangles more or less made no difference (30 tiles as
30 rectangles or as 4 took the same time).

`cfx_osd_demo <seconds> design <file> ...` (driven by `temp\hookfree-partial-repaint\demo_design.ps1`)
runs the real window: values change once per second and are fed every 500 ms, as the design feed
did before. `tick 10s` at zoom 141 %, 90 % of the readings moving:

| Device | Mode | Repaints per 10 s | Content ticks (no tile changed) | rootGpu avg | Dirty pixels |
|---|---|---|---|---|---|
| hardware | legacy | 20 | 20 (0) | 3.0 to 3.3 ms | whole root (489 k) |
| hardware | partial | 10 | 20 (10) | 0.75 to 0.8 ms | 95 k = 19 % |
| WARP | legacy | 20 | 20 (0) | 7.2 to 8.7 ms | whole root (489 k) |
| WARP | partial | 10 | 20 (10) | 1.5 to 2.1 ms | 95 k = 19 % |

`rootGpu` on WARP is its rasterization time. The wall-clock `draw` phase of the render thread is
another matter: in the window a repaint comes once per second on a thread that slept in between,
and it took 3.2 to 6.8 ms for the whole root and 2.2 to 5.8 ms for the changed tiles, differing by
a factor of two between runs of one configuration (with the process priority raised as well). So
with nearly every reading moving the partial repaint saves the render thread about a quarter of
its time, far less than it saves the rasterizer; with few readings moving it is 0.5 ms.

### 16.5 What is not known yet

Nothing was measured in the game. Hypotheses 2 to 4 of section 4 are open: whether the slips scale
with the root presents, why WARP was not reliably clean with FUSION, and whether the hardware
device can reach "off" at all (the chart layer still presents ten times per second on the game's
adapter; this work did not touch it). The split of FUSION's repaints by feed in the game (Phase 0,
item 6) comes out of the `legacy` runs below.

### 16.6 Measuring in the game

The app output must hold the new core: build the app (section 6) and compare the SHA-256 of
`CapFrameX.OSD\build\bin\RelWithDebInfo\cfx_osd_core.dll` with the one beside `CapFrameX.exe`.

`OsdDebug.json` keys: `verboseLog` true, `hookFreeWarp` true or false, `hookFreeRepaint`.

- The repaint mode switches at a show: write the file, set `isOverlayActive` false and true again.
  The core logs `[show] OsdDebug.json: hookFreeRepaint=...` and, when the swap chain kind changes,
  `[repaint] root swap chain is now ...`; `mode=` in the next `tick 10s` line confirms it.
- The device switches only with a new instance: select another renderer and hook-free again, then
  re-activate the tile profile. `[create] hook-free device: ..., repaint ...` confirms it.
- Wait 20 s after every show before a capture (section 12).

Variants, interleaved, at least 5 runs each: `off`; `hw legacy` (the baseline of section 3,
`P0`); `hw partial`; `warp legacy`; `warp skip` (Phase 1); `warp partialPresent` (10.2);
`warp partial`. `hw skip` and `hw partialPresent` only if time allows: on the hardware device the
number of presents is expected to count, not their size. Read the `tick 10s` lines of the same
minutes for repaints, reasons and cost, and evaluate with the compare script (section 12).

Then report to the user: whether Phase 1 and 2 bring WARP into the range of the off runs, what the
hardware device still costs, and with that the question of Phase 3.

### 16.7 Left for the release

- Committed on 2026-11-10. OSD (`main`): `c1ad7f8` the probe, `e80ab37` the partial repaint with
  its tests, `a3ad233` the demo's design mode, `08d249f` CLAUDE.md. CapFrameX (`release/1.9.2`):
  the design feed cadence with its test, the prebuilts, CLAUDE.md, this plan.
- `external\CapFrameX.OSD-prebuilt\` was updated on 2026-11-10 at the user's request, before the
  measurement in the game: core, both hooks and both Vulkan layers from a clean build of the OSD
  working tree that became `e80ab37` (so they contain the layer fix `82eaeaf` too), and the two
  layer DLLs replaced the installed ones in `C:\Program Files\CapFrameX\vulkan` and `vulkan\x86`
  (the former ones are in `temp\vulkan-layer-backup-2026-11-10`). The README entry names the
  commit. Controls and Interop are unchanged. The app's Debug output holds the same binaries.

### 16.8 First run in the game (2026-11-10, one capture)

`CapFrameX-DOOMTheDarkAges.exe-2026-11-10T074235.json`: hook-free, hardware device, `partial`,
FUSION shown throughout, 20 s, game on `Hardware: Independent Flip`. One run, another session than
the runs of section 3: it shows where the remaining spikes come from, not how large the gain is.

| | this run | FUSION hw, 2026-11-09 (2 runs) | overlay off, 2026-11-09 (4 runs) |
|---|---|---|---|
| display intervals > 1.25 x median | 15 | 122, 239 | 8, 10, 25, 43 |
| real frames with > 1.5 ms GPU excess | 17 | 149, 195 | 8, 11, 20, 45 |
| display interval stdev (ms) | 0.42 | 0.97 to 1.48 | 0.61 (section 3) |

GPU excess is `GpuActive` of a real frame minus the median of the 40 real frames around it.

- The overlay did what the logs of the bridge and the core say: 10 design updates per 10 s on
  refreshes (30 to 36 snapshots waited), 10 root repaints, all partial (about 32 tiles, 10
  rectangles, 17 % of the surface), `rootGpu` 0.39 to 0.41 ms, `chartGpu` 0.02 to 0.04 ms.
- The game is GPU-bound: `GpuActive` is 99 to 100 % of the time between two real frames in every
  second of the capture. Whatever takes GPU time moves the next frame pair on the display by the
  same amount.
- Every overlay present still costs the frame in flight. Largest excess of the frames within -3 to
  +25 ms of a tick (period 99.96 ms): the 1 Hz root repaint 0.3 to 1.5 ms (20 of 20, mean 0.86),
  a chart tick 0.3 to 0.8 ms (105 of 179; 66 below 0.3), between ticks 0.05 ms on average (190 of
  199 below 0.3). FUSION on 2026-11-09: 0.7 to 3.1 ms per tick position. With the overlay off the
  tick positions and the gaps between them read the same (0.2 to 0.6 ms).
- No root repaint caused a frame with more than 1.5 ms excess. Of the 17 such frames, 10 are not
  near a tick (1.8 to 2.1 ms, as in the overlay-off runs, which hold 8 to 45 of them), 7 follow a
  chart tick by 0 to 11 ms with 2.5 to 3.3 ms (five of them on one 100 ms grid to within 1.2 ms),
  all in the last 5 s. Not known: whether those 7 are the game's own slips with the tick cost on
  top, or slips a chart present triggers. No 1 s pattern in the slips (fold at 995 to 1005 ms:
  24 % in a 60 ms slot, random frames reach 35 %).

So the root repaint is no longer a source of spikes on the hardware device in this run; what is
left of the overlay is the cost of a present itself, ten times per second from the chart layer,
which this work did not change. The runs of section 16.6 are still needed for every statement
about the size of the gain.

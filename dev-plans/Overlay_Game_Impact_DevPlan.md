# Overlay: what it still costs the game, and how to remove it

Dev plan and session prompt, written 2026-11-13. Start a new session in `E:\Code\CapFrameX` with:

> Read `dev-plans/Overlay_Game_Impact_DevPlan.md` completely and carry it out, starting with
> section 5 "Before you change anything".

Everything below is addressed to that session. It has none of the earlier conversation, so this file
holds what was found, how sure each finding is, where the code is and how to measure. Line numbers
are those of 2026-11-13 (OSD repo `main` at `23e73ae`, CapFrameX `release/1.9.3` at `056a42bd`, both
clean); look for the function when a number is off.

**Status (2026-11-13):** nothing is implemented. The findings come from a code audit of both
repositories. Nothing was built, run or measured for it; every size that is not quoted from an
earlier measurement is derived from the code.

## 1. The task

The overlay must not cost the game frame rate or lows (OSD `CLAUDE.md`, "Performance model"). The
big costs are gone: no second GPU device in the game process, no renderer teardown on a hide, a
retained root surface with partial repaints in the hook-free window. The audit looked for what is
left, in three places:

- **A. In-game renderers** (DXGI hook, Vulkan layer, the core's producer thread inside the game):
  periodic work that still runs on the game's present thread, work the producer repeats although
  nothing changed, and locks both threads share.
- **B. Hook-free window**: the chart layer presents ten times per second on the game's adapter.
  This is measured and was left open by `HookFree_Partial_Repaint_DevPlan.md`.
- **C. `CapFrameX.exe`**: CPU time and allocations per PresentMon row and per overlay refresh, and
  recurring scans of the game process. They cost CPU beside the game, not frames directly, so they
  matter in CPU-bound games and on CPUs with few cores.

The packages are independent of each other unless section 7 names a dependency. Package C lives in
the CapFrameX repo alone and can run in a session of its own.

## 2. When it is done

- **Present thread.** In a steady state (overlay visible, no resize, no design switch) the hooked
  Present and `vkQueuePresentKHR` do no JSON parsing, no display configuration query, no process
  snapshot and no whole-panel copy for a picture that did not change. `presentStats` shows it: the
  once-per-second and once-per-snapshot peaks of the Phase 0 baseline (section 8) are gone from the
  maximum and the outlier counts of `hookTotal`. Report the numbers; this plan sets no absolute
  threshold, because none was measured.
- **Producer thread.** With the default 1 s OSD refresh it rasters the panel about once per
  second, not five times, and its CPU time per 10 s is lower than the baseline for classic rows, a
  tile design and the Performance Strip, on D3D11, D3D12 and Vulkan.
- **Picture.** Unchanged: a skipped raster never hides a visible change (test), the chart motion
  suite passes for all three APIs, hide and show, resize, zoom, design switch, HDR, pixel orbit and
  full-width designs still work.
- **Game.** Captures in the games the user names (section 12), several runs per variant against
  "overlay off": lows inside the range of the off runs, as they were for D3D11 after the work of
  2026-07. A single run proves nothing.
- **CapFrameX.exe.** `scripts\Measure-CpuLoad.ps1` with `-BaselineReport`: no scenario costs more
  cycles than before, and the scenarios a package targets cost less. Report the differences.
- **Builds and tests.** All five native trees build (core, `hook_poc` x64 and x86, `vk_layer` x64
  and x86), CTest is green in each, the CapFrameX unit tests are green.
- Whatever does not reach its goal is reported with numbers and with what remains.

## 3. What is measured (earlier sessions, not repeated here)

- **Hook-free, cost per present** (Doom: The Dark Ages, RTX 5090, FSR frame generation, GPU
  saturated, 2026-11-09/10): a game frame hit by a chart-only tick takes 0.3 to 0.8 ms more GPU
  time, one hit by the 1 Hz root repaint 0.3 to 1.5 ms after the partial repaint. With classic rows
  a WARP device was indistinguishable from "overlay off" although its chart layer presented ten
  times per second as well; with FUSION one WARP run was clean and one was not. Source:
  `dev-plans/HookFree_Partial_Repaint_DevPlan.md`, sections 3 and 16.8.
- **D3D11 hook** (Tomb Raider, about 435 FPS, 2026-07-08): with the game-device-only path the lows
  equal RTSS. The game was GPU-bound and spent 1.38 ms blocked in its own Present, which masked
  every CPU cost of the hook before it. So that acceptance says nothing about CPU-bound games.
- **Dormant hook** (harness, 2026-07-08): the re-arm scan with `CreateToolhelp32Snapshot` every 2 s
  cost one present 3.2 ms (p99.9). The code is unchanged (A4).
- **Vulkan layer** (harness, RTX 5090, about 18k FPS): `hookTotal` p50 14.9 us, p99 33 us.
- **Software raster of a whole panel**: 2.7 ms for classic rows and about 8 ms for FUSION on WARP
  (the partial repaint plan, section 3). The in-game producer uses Direct2D's software target on a
  WIC bitmap, another rasterizer, so take this as an order of magnitude only.
- **Chart rebuild** (`chart_perf_bench`): a 1920 px counted-framerate strip about 70 us including
  the coverage raster. This does not include the copies for the CPU hosts (A5).

## 4. What the audit read, and what is only estimated

Every finding below carries one of these marks:

- **[verified]**: the author of this plan read the named lines on 2026-11-13.
- **[audit]**: reported by a reading pass with file and line, not read again. Check it before you
  build on it.
- **[measured]**: from section 3.
- **[estimated]**: computed from sizes and rates in the code. Never report an estimate as a result.

One audit claim was dropped as unproven: a heap allocation per sample in the `std::deque` of the
frame series. MSVC's deque keeps one 16-byte element per block (`FrameSample` is 16 bytes,
`ReplayBuffer.h:11-16`), but whether a push in the steady state allocates was not shown. The poor
locality of that layout stands (A7).

## 5. Before you change anything

1. Read the OSD repo's `E:\Code\CapFrameX.OSD\CLAUDE.md`: "Performance model", "Debug/diagnostics",
   "Hook-based overlay", "In-game chart motion", "Vulkan overlay layer", "Hook-free root". Three
   statements in it are stale (fix them with B4):
   - "the text layer ... reaches the game device as IMMUTABLE ring textures via free-threaded
     `CreateTexture2D(initData)`": the producer now only fills a CPU slot and the present thread
     maps a dynamic texture (`GraphicsDevice.cpp:236-329`).
   - "`SetMaximumFrameLatency(1)`" as load-bearing for the hook-free window: the hook-free device
     deliberately does not set it (`GraphicsDevice.cpp:43-47`).
   - `hookFreeHz` in `OsdDebug.json`: the host overwrites it right after creation with the app
     setting (`CapFrameX.OSD.Interop\OsdHost.cs:212, 561-567`).
2. Check the branch pairing: CapFrameX `release/1.9.3` goes with OSD `main`. Run
   `git status --short` in both. If either is dirty, ask the user what the changes are before you
   touch the same files; never stash, reset, check out or clean them.
3. Build the unchanged native trees and run CTest (section 6), and run the CapFrameX unit tests,
   so you know the baseline is green. `chart_gpu_raster` fails on the RTX 5090 with driver
   32.0.16.2101 and did so before (OSD `CLAUDE.md`, "Tests").
4. Do Phase 0 (section 8) before any behavior change. Several findings are estimates; the baseline
   decides their order and whether the larger ones are worth building.
5. Ask the user which package to start with if they did not say. The recommended order is in
   section 7.

## 6. Workspace

### Repositories

| | Path | Branch | State on 2026-11-13 |
|---|---|---|---|
| CapFrameX | `E:\Code\CapFrameX` | `release/1.9.3` | clean except this plan (untracked) |
| OSD (private) | `E:\Code\CapFrameX.OSD` | `main` | clean |
| Test apps | `E:\Code\CapFrameX.Test` | | not inspected |

Native sources: `E:\Code\CapFrameX.OSD\CapFrameX.OSD\src\core\` (core), `hook_poc\src\` (DXGI
hook), `vk_layer\src\` (Vulkan layer). Below, `core\`, `hook\` and `vk\` stand for these folders,
and paths without a prefix are under `E:\Code\CapFrameX\source\`.

### Build

```
cd E:\Code\CapFrameX.OSD\CapFrameX.OSD
cmake --preset vs2026
cmake --build build --config RelWithDebInfo --parallel
ctest --test-dir build -C RelWithDebInfo
```

The hook and the Vulkan layer are CMake projects of their own with an x64 and an x86 tree each
(`hook_poc\build`, `hook_poc\build-x86`, `vk_layer\build`, `vk_layer\build-x86`); the commands are
in the OSD `CLAUDE.md` ("Hook-based overlay", "Vulkan overlay layer"). Their CTest suites:
`hook_poc\CMakeLists.txt:284-373`, `vk_layer\CMakeLists.txt:156-159`.

The CapFrameX app stages core, hooks and layers from those build folders into its output, whatever
their age. Compare the SHA-256 of a build output with the copy beside `CapFrameX.exe` before every
measurement. Build the app with
`msbuild source\CapFrameX\CapFrameX.csproj /p:Configuration=Release /p:Platform=x64 /p:VisualStudioVersion=18.0`
(Debug is locked while Visual Studio runs CapFrameX). Build single projects when a parallel build
fails with LNK1104 or RG1000.

Games load the **registered** Vulkan layer, which is the copy in `C:\Program Files\CapFrameX\vulkan`
(and `vulkan\x86`), not the build output. Replacing it needs the user's approval each time
(section 15).

### Logs and switches

- `%TEMP%\cfx-osd-logs\cfx_osd.log`, or `cfx_osd_<pid>.log` when another process loaded the core
  first; the header names process and PID. `[diag]` lines need the verbose switch ("Extended OSD
  logging", or `verboseLog` in `OsdDebug.json`).
- `%TEMP%\cfx-osd-logs\cfx_present_stats.log`: per-present segment percentiles of the hook and the
  layer, with `"presentStats": true` in `%APPDATA%\CapFrameX\Configuration\OsdDebug.json`. Read
  once when the hook DLL loads, so set it before the game starts.
- `CFX_VKLAYER_LOG=1` writes `cfx_vklayer.log`.
- The in-game knobs `bgHz`, `textHz`, `noComposite` are read inside the game process when the
  instance is created.
- A game that kept running across a hook rebuild keeps its first injected hook: restart the game
  after every hook build.

### Tools

- `scripts\Measure-CpuLoad.ps1` (elevated, no CapFrameX running): CPU time and cycles of
  CapFrameX.exe and PresentMon per scenario, a thread breakdown, `-BaselineReport` for
  before/after. `scripts\CPU_LOAD_TEST.md` documents it.
- `E:\Code\CapFrameX.Test\tools\verify-osd-*.ps1`: regression suites against the test apps
  (`verify-osd-chart-motion.ps1`, `verify-osd-hide-cost.ps1`, `verify-osd-tiles.ps1`,
  `verify-osd-hdr.ps1`, `verify-osd-d3d12-native.ps1`, ...). Some script paths have drifted (log
  path, status V2); fix a script rather than skip it.
- The capframex MCP server answers only while CapFrameX runs; its port is in `CapFrameX.log`
  ("MCP endpoint available at").

## 7. Work packages at a glance

| ID | What | Repo | Size | Evidence | Needs |
|---|---|---|---|---|---|
| P0 | Counters and baselines | both | S | | |
| A1 | Raster the panel only when a tile changed | OSD | S | verified, estimated | P0 |
| A2 | Parse the feeds on the producer thread | OSD | M | verified, estimated | P0 |
| A3 | Display configuration query off the present thread | OSD | S | verified | P0 |
| A4 | Dormant hook: no process snapshot in Present | OSD | S | verified, measured | |
| A5 | Chart copies of the CPU hosts | OSD | M to L | verified, estimated | P0, user (option c) |
| A6 | Wake the producer only when a rebuild is due | OSD | S | verified | |
| A7 | No lock held over a copy between both threads | OSD | M | verified | |
| A8 | Shaders compiled at build time | OSD | M | verified | |
| A9 | Vulkan layer details | OSD | S to M | verified, audit | |
| A10 | DXGI hook details | OSD | S | verified, audit | |
| B1 | Hook-free device: finish the measurement series | both | M | measured | user |
| B2 | Hook-free chart rate | both | S to M | measured | B1, user |
| B3 | One present per hook-free tick | OSD | M | verified | B1 |
| B4 | Stale documentation, small fixes | OSD | S | verified, audit | |
| C1 | `OnlineMetricService`: filter before the thread hop | CapFrameX | S | verified | P0 |
| C2 | Online percentiles: one pass, no window copies | CapFrameX | M | verified, audit | P0 |
| C3 | Target policy scan off the row thread, slower | CapFrameX | M | verified | P0 |
| C4 | Status polling while hidden, cached identity | CapFrameX | S | audit | |
| C5 | Metrics channel without per-string allocations | CapFrameX | S | verified | |
| C6 | Design publisher and telemetry snapshot | CapFrameX | S to M | verified, audit | |
| C7 | Row representation | CapFrameX | S (a), L (b) | verified, estimated | P0, user (b) |
| C8 | Hook-free bridge details | CapFrameX | S | verified, audit | |

Recommended order: P0; then A1, A3, A4 (small, independent, each removes work that serves
nothing); A2; A6; A7; A8; A5 once P0 shows its size; A9, A10. In the CapFrameX repo: C1,
C2, C5 first, then C3, C4, C6, C8, and C7 after its measurement. B1 needs the user in the game and
decides B2 and B3.

## 8. Phase 0: counters and baselines

Nothing here changes behavior. Without it the estimates below stay estimates.

1. **`presentStats` segments** (`hook\present_stats.h:18-33`). The once-per-second colour query
   and the design feed run outside every segment and show only in `hookTotal`. Add `SegColorInfo`
   (A3), `SegDesignFeed` (`DesignShmGet` plus `DesignFeed::Apply`) and `SegPanelUpload` (the panel
   and chart copies of the present thread: `materializeCpuText`, `CopyCpuFrameToUpload`,
   `CpuChartStore::Write`). An event that runs once per second touches 1/FPS of the presents: at
   200 FPS that is 0.5 %, which p99 never shows and p99.9 only just. So extend each 10 s block by
   the number of presents in which a segment exceeded 50 us and 200 us. Keep the hard rule of that
   file: the present thread only reads QPC and writes the preallocated ring.
2. **Producer summary.** Verbose only, one `[diag] producer 10s` line from `bgRenderLoop`
   (`core\OsdInstance.cpp:2483-2509`): wakes, produces, text ticks, rasters, chart rebuilds, bytes
   handed to `cpuChartsCb_`, and the thread's own CPU time and cycles over the window
   (`GetThreadTimes`, `QueryThreadCycleTime` on itself). Pure counters belong in `Diagnostics.h`
   with a test in `apps\diagnostics_test`.
3. **In-game baseline**, per API (D3D11, D3D12, Vulkan; x64 is enough) with the test apps of
   `CapFrameX.Test`: classic rows, one tile design (FUSION or a starter with many tiles) and the
   Performance Strip (`overlay-templates\Performance-Strip.json`, full width). Record the
   `presentStats` blocks and the producer line for 60 s each. Uncapped and at a capped rate above
   100 FPS.
4. **CapFrameX.exe baseline**: `.\scripts\Measure-CpuLoad.ps1 -Repetitions 3` and the same with
   `-TargetArguments '--present_mode 0 --width 640 --height 480'` (uncapped vkcube stresses the
   row path). Keep both `report.json` files for `-BaselineReport`. Note the allocation rate and
   the GC counts of CapFrameX.exe for the uncapped run (`dotnet-counters monitor -p <pid>
   System.Runtime` if installed; otherwise the GC deltas of the `HookFree OSD: render loop 10s`
   line).
5. Write the baseline numbers into section 18 of this file.

## 9. Package A: in-game renderers (OSD repo)

### A1. Raster the panel only when a tile changed

**Finding [verified].** `bgProduceFrame` rasters the whole panel whenever the text period (200 ms,
`textHz`) has passed: `textTick = !extTextOnce_ || structPending || animating || textAccumMs_ >=
textPeriodMs` (`core\OsdInstance.cpp:2180`), then `renderer_.clear` and `scene_.draw` for the CPU
hosts (2257-2280) and for external-v2 (2331-2346). Nothing compares the picture. The signatures the
hook-free window uses for this are stored by every draw, but the in-game producers do not read
them. Values snap at this draw spacing (`updateDisplayValue`, `core\Widget.h:98-107`: `k = dt * 12`
is above 1 from 84 ms on), so with the default 1 s OSD refresh about four of five rasters paint the
picture that is already there **[estimated]**.

Each raster is followed by a whole-panel hand-off, 2 MB for a 500 x 1000 px panel:

- D3D11: the producer copies into a CPU slot (`core\GraphicsDevice.cpp:236-279`), and the
  **present thread** maps the dynamic texture and copies again (`materializeCpuText`, 284-329).
- D3D12: the producer copies under `g_cpuFrame.mutex` (`hook\overlay_d3d12.cpp:302-318`), and the
  present thread copies into the upload buffer under the same mutex (351-381).
- Vulkan: the producer writes into the staging ring (`vk\osd_feed.cpp:163-192`); the present thread
  records one whole-panel `CmdCopyBufferToImage` per swapchain image
  (`vk\vk_compositor.cpp:1522-1593`), so each text frame is uploaded once per image.

**Change.** On a text tick, raster only when something forces it or a tile would draw something
else:

- Forced: the first frame, a structure rebuild, a relayout after value growth (both set `textTick`
  today, 2215 and 2220), `scene_.hasInlineCharts && chartTick` (2250), and a changed background
  opacity. `applyBackgroundAlpha()` returns whether the theme changed; `bgProduceFrame` ignores the
  result today (2242), the hook-free tick uses it (1734).
- Otherwise `AnyTileDiffers(*scene_.root, rc)` (`core\TileRepaint.h:136-142`), which compares each
  leaf's `contentSignature(rc)` with its `drawnSignature`. This covers values, threshold colors,
  chart scale labels and badges, and a smoothed value that has not arrived (a raised `textHz`).
- Do not gate on `contentChanged` alone: `snapshotSharedData` consumes the content mark on whatever
  produce comes first, usually a chart-only tick (2186), so a text tick would miss it. The
  signature comparison does not need the mark. If you want the mark as a shortcut, latch it until
  the next raster.
- The CPU host with a chart layer also delivers the panel while the intro fade runs, because the
  fade travels in the frame description (2257). Keep that; it lasts 0.35 s once.
- `produceCpuCharts` reads the raster of the last text frame (2407-2410). It stays valid when a
  raster is skipped.

Nothing else is needed for the hand-off: without a new frame the sequence numbers stand still
(`cpuSeq_`, `g_cpuFrame.sequence`, the staging ring's `seq`) and the present thread copies nothing.

**Switch for A/B.** `OsdDebug.json` `"textRaster": "always"` restores the unconditional raster
(default `"changed"`), read where `textHz` is read. Document it with the other in-game knobs.

**Risk.** A widget whose picture depends on something its signature does not hold would freeze.
The hook-free partial repaint relies on the same signatures since 2026-11 and has the test for it.

**Tests.** `apps\design_test`: for every widget kind and the test designs, a producer that skips
by this rule leaves the same bitmap as one that rasters every text tick, over a random sequence of
value sets (reuse the generators of `partial_repaint.h`). A case each for the forced reasons.
`diagnostics_test` for the new counters.

**Done when** the producer line shows rasters at about the OSD refresh rate instead of 50 per
10 s, `SegPanelUpload` fires as rarely, and the test above passes.

**Later, only if P0 shows the remaining raster matters:** keep the CPU raster as a retained
surface and repaint only the changed tiles, as the hook-free root does (`PaintChangedTiles`), and
hand the hosts dirty rectangles.

### A2. Parse the feeds on the producer thread

**Finding [verified].** Three kinds of feed work run inside the game's Present:

- **Design metrics**, on every changed metrics revision, which is every sensor poll or 500 ms
  heartbeat whose JSON differs (1 to 4 per second): `DesignShmGet` copies the JSON
  (`hook\overlay_design_shm.cpp:88-90`), `DesignFeed::Apply` calls `cfx_osd_apply_design_json`
  (`hook\overlay_design_feed.cpp:24`), and `applyDesignJson` parses it with nlohmann and rebuilds
  two maps with a UTF-16 conversion per text (`core\OsdInstance.cpp:1388-1398`,
  `core\Config.cpp:494-517`). Call sites: `hook\overlay_osd.cpp:447-450`,
  `vk\osd_feed.cpp:517-518`. Cost per snapshot is not measured; the audit guesses 0.05 to 0.3 ms
  **[estimated]**.
- **Design template**, on activation and on every design revision: `loadSceneFromJson` parses up
  to 1 MB, decodes embedded images through WIC, creates a DirectWrite factory and measures the
  whole layout, and `sceneToJson` serializes the result again (`core\Config.cpp:459-492`,
  `core\OsdInstance.cpp:1392-1395`). A one-time hitch of unknown size, plausibly tens of
  milliseconds for the starters with images **[estimated]**.
- **Classic rows**, once per OSD refresh: `MetricsShmGet` copies the record block into a fresh
  vector and builds five `std::string` per entry (`hook\overlay_metrics_shm.cpp:196-248`), and
  `FeedEntries` hands them over one call per entry (`hook\overlay_osd.cpp:113-156`,
  `vk\osd_feed.cpp:241-273`). In the GPU-bound Tomb Raider run this was masked (section 3).

**Change.**

1. Core: an asynchronous variant of `cfx_osd_apply_design_json` for instances with a producer
   thread (`externalV2_`, `externalCpu_`). The caller's thread only moves the two strings and their
   revisions into one pending slot under `feedMutex_` and wakes the producer. The producer takes
   the slot at the start of `bgProduceFrame`, parses and validates outside every lock, and then
   applies it as `applyDesignJson` does now (`pendingScene_`, `shared_.values`, 1400-1427). The
   rule "validate both halves before changing either" stays, on the producer. It reports the
   accepted and the rejected revision pair through atomics the host reads with a new C API call.
   The synchronous call stays for the hook-free window, the editor preview and instances without a
   producer.
2. Hook and layer: `DesignFeed` submits and reads the result. `active_` now turns true one or two
   presents after a new design was submitted. Check what depends on it: the early return for a
   design without classic rows (`hook\overlay_osd.cpp:451-459`, `vk\osd_feed.cpp:519-527`),
   `PublishMetricsStatus(designActive)` and the layer's `g_drawable`. A design that was active
   stays active while new metrics are pending. Keep the log lines `custom design accepted` and
   `custom design snapshot rejected`.
3. Leave the copy out of shared memory on the present thread: it has to happen inside the sequence
   window, and for the metrics it is a few kilobytes.
4. Classic rows, cheap step: use the batch call `cfx_osd_update_entries_v2`
   (`include\cfx\osd\osd_c_api.h:202`, one feed lock, strings converted before it) instead of
   begin/add/end, and reuse the record buffer and the cached strings in `MetricsShmGet`. Move the
   parsing to the producer as well only if `SegEntryFeed` in P0 says so.

**Tests.** `hook_poc`: `design_feed` and `design_shm` (CTest) cover the feed's state machine;
extend them for submit, accept and reject across presents. Core: a design applied through the
asynchronous call yields the same scene and values as through the synchronous one.

**Done when** `SegDesignFeed` no longer shows a peak per snapshot, and a design switch in a
running game costs no long present (compare the maximum of `hookTotal` over a switch).

### A3. Display configuration query off the present thread

**Finding [verified].** Once per second, inside Present:

- D3D11 (`hook\overlay_d3d11.cpp:251-264`) and D3D12 (`hook\overlay_d3d12.cpp:1806-1817`) call
  `QueryDxgiOutputColorInfo`, which always ends in `QuerySdrWhiteLevelNits`
  (`core\ColorManagement.cpp:145-146`): `MonitorFromWindow`, `GetMonitorInfoW`,
  `GetDisplayConfigBufferSizes`, two vector allocations, `QueryDisplayConfig` and
  `DisplayConfigGetDeviceInfo` per path (27-74). Also for SDR swapchains.
- Vulkan (`vk\vk_compositor.cpp:1393-1399`) does it for HDR swapchains only, but adds
  `FindCurrentProcessPresentationWindow`, an `EnumWindows` over all top-level windows
  (`core\ColorManagement.cpp:76-105`), under `presentLock`.

The duration is not measured (win32k calls, hundreds of microseconds to milliseconds
**[estimated]**). One frame per second pays it.

**Change.**

1. Split `QueryDxgiOutputColorInfo`: the swapchain part (`GetDesc`, `GetPrivateData`, and
   `GetContainingOutput` only for an untracked RGB10 swapchain) stays where it is. The SDR white
   level is queried only when the resolved space is HDR. First confirm in the composite and chart
   shaders that an SDR target ignores `sdrWhiteNits`.
2. For HDR, move the white-level query to the producer: the host passes the output window once
   (a new setter beside `cfx_osd_set_output_color_space`), the producer polls once per second and
   publishes the value through an atomic that the composite reads. The Vulkan layer has no window
   handle; let the producer run `FindCurrentProcessPresentationWindow` too.

**Tests.** `color_management` and `hdr_composite` (CTest), `verify-osd-hdr.ps1`. An HDR toggle
while the game runs must still reach the overlay within about a second.

**Done when** `SegColorInfo` shows no outlier per second on SDR and HDR.

### A4. Dormant hook: no process snapshot in Present

**Finding [verified, measured].** After CapFrameX exits, the resident hook looks for a new
CapFrameX every 2 s from inside Present: `HostWatchTick` (`hook\overlay.cpp:325-360`) calls
`OverlayInitHostWatch`, which takes a `CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS)` and walks it
(265-304). Measured at 3.2 ms for that present. It lasts until the game ends.

**Change.** Probe a named object first: `OpenEventW(SYNCHRONIZE, FALSE,
L"Global\\CfxOsdOverlayVisible")` (the event of `HookVisibilityChannel`,
`CapFrameX.OSD.Integration\HookVisibilityChannel.cs:22`; the test mode uses
`Local\CfxOsdOverlayVisible_Test`). Take the snapshot only when it opens, and keep the handle as
`g_visibleEvent` instead of opening it a second time (367-383). Check that CapFrameX creates the
event whatever renderer is selected (`HookOverlayManager.cs:219` creates the channel in the
manager's constructor); if it does not, the probe must use an object that always exists. The hook
closes its own event handle when it goes dormant (341), so the event disappears with CapFrameX
unless another process holds it, in which case the snapshot runs as today.

**Tests.** A hook test for the state machine with a fake probe: no CapFrameX means no snapshot;
the event appearing leads to one snapshot and a re-arm. In the harness: `presentStats` with the
host closed shows no 2 s outlier in `visible`.

### A5. Chart copies of the CPU hosts (D3D12 hook, Vulkan layer)

**Finding [verified, estimated].** The D3D12 hook and the Vulkan layer cannot shift a mask in a
shader, so the core composes pre-scrolled copies of each chart's plot rectangle at every rebuild
(up to 100 per second): `produceCpuCharts` (`core\OsdInstance.cpp:2402-2459`) calls
`ComposeChartVariants`, which copies the plot rectangle out of the panel once per variant and
blends the lines on top (`core\ChartGeometry.h:226-280`). The number of variants is the pixels the
chart crosses in 64 ms, at most 12. `CpuChartStore::Store` then copies all of it again
(`hook\cpu_chart_store.cpp:35-41`), and the present thread copies the selected variant under the
store's mutex whenever the generation or the variant changed (63-96), which above 100 FPS is
nearly every present.

- A classic 300 x 60 px graph: about 5 variants of 75 kB, 0.75 MB per rebuild with the second
  copy. Harmless.
- The Performance Strip (plot about 1850 x 140 px): 1.04 MB per variant, 12 variants, about 25 MB
  per rebuild with the second copy, up to 2.5 GB/s at 100 rebuilds per second, plus about 1 MB per
  present on the present thread. With resolution scaling at 4K four times that.

D3D11 is not affected: it uploads the mask once per rebuild and shifts it in the shader.

**Change.** Measure first (P0, the producer line with the strip on D3D12 and Vulkan); add a mode
to `apps\chart_perf_bench` that times `ComposeChartVariants` plus `Store` for a 1920 px strip.
Then, by effort:

- **(a)** Compose straight into the store's pending buffer (the host hands out the destination),
  which removes the second copy. Small.
- **(b)** Copy only the rows the lines touch in this and the last shown generations. The present
  thread's copy shrinks alike. Medium; the per-image state of the Vulkan chart slots
  (`shownCharts`) must agree with it.
- **(c)** Hand the hosts the R8 coverage masks and shift them in the shader, as D3D11 does. This
  removes the variants and every per-present chart copy. It touches the D3D12 pipeline and all
  Vulkan shaders (graphics, compute, RGB10 and FP16 variants) and the HDR paths. Large; the user
  decides (section 16).

Do not lower the rebuild rate instead without checking the picture: in the local graph mode the
line's newest end is extrapolated flat until the next rebuild, so a slower rebuild shows at the
right edge. `verify-osd-chart-motion.ps1` judges it.

**Tests.** `cpu_chart_store` (hook CTest), `chart_geometry`, `verify-osd-chart-motion.ps1` for
D3D12 and Vulkan, `verify-osd-tiles.ps1`.

### A6. Wake the producer only when a rebuild is due

**Finding [verified].** Every present signals the producer: `presentTick_.fetch_add` and
`SetEvent(bgWake_)` in `renderExternal` (`core\OsdInstance.cpp:1979-1980`) and in
`cfx_osd_cpu_present_tick`. `bgRenderLoop` wakes, reads the clock, and `ProducerPacer::onWake`
returns false when the period has not passed (`core\RenderPacing.h:178-186`). Above 100 FPS the
surplus presents each cost a syscall on the present thread and a thread wake that does nothing.
The thread is a plain `std::thread` (2515); nothing in core, hook or layer sets a thread priority,
affinity or name.

**Change.**

1. The producer publishes when it wants the next signal (an atomic QPC value: the previous rebuild
   plus the period). The present thread still counts the present and signals only when that time
   has passed. The pacing rule stays: a rebuild runs on the first present at least one period
   after the previous rebuild. The 250 ms idle timeout stays. A pending structure change needs no
   extra signal, since the pacer bounds its pickup to one period today as well.
2. Name the thread (`SetThreadDescription` through `GetProcAddress`, it is missing before
   Windows 10 1607) so profilers show it.
3. **Priority: dropped (the user's decision, 2026-11-13).** The producer keeps the priority it
   starts with; add no priority switch either. Reduce its CPU time by doing less work instead
   (A1, step 1 above). The reasons:
   - Priority inversion. The producer holds `feedMutex_` while it copies the frame series
     (`core\OsdInstance.cpp:779-783`) and, in the D3D12 hook, `g_cpuFrame.mutex` while it copies
     the panel (`hook\overlay_d3d12.cpp:302-318`); the present thread takes both. `std::mutex`
     does not raise the owner to its waiter's priority, so a lowered producer preempted inside
     either lock holds up the game's Present.
   - Starvation. A priority only decides anything while every core is busy, and then a lowered
     thread waits for Windows' starvation boost, which takes seconds. A7 removes the inversion,
     not this. The GPU showed the same pattern: `SetGPUThreadPriority(-7)` starved the hook-free
     window and was reverted (OSD `7859fc1`, 2026-08-07); as `hookFreeGpuPriority` -7 on
     2026-11-09 the overlay's own work waited up to 260 ms while the game's slips stayed. A game
     saturating every core also starved CapFrameX's normal-priority hotkey thread (`3026bddc3`).
   - A raised priority is no way out: the producer would take time from the game's threads.

**Tests.** `hook_free_pacing_test` holds the pacer tests and the negative control. Add a model of
the gated signal: for present streams at 60, 90, 144, 240 and 1000 FPS it yields the rebuild times
of the ungated pacer. `verify-osd-chart-motion.ps1` for all three APIs.

### A7. No lock held over a copy between both threads

**Finding [verified].**

- `feedMutex_`: the present thread takes it for every frame it pushes (`pushFrametime`,
  `core\OsdInstance.cpp:578-586`; the hook pushes every recorded frame,
  `hook\overlay_osd.cpp:426-435`). The producer copies the whole retained frame series under it
  whenever the generation changed (779-783), which in the default local graph mode is every
  rebuild: several seconds of frames, each a block of its own in MSVC's deque.
- D3D12 `g_cpuFrame.mutex`: the producer holds it for the whole-panel copy
  (`hook\overlay_d3d12.cpp:302-318`); the present thread takes it twice per present
  (`GetCpuFrameMetadata` 338-349, `CopyCpuFrameToUpload` 351-381) and copies the panel under it
  when a new frame arrived.

Hold times are tens to hundreds of microseconds **[estimated]**. The worst case is the producer
being descheduled inside the lock on a saturated CPU.

**Change.**

1. Frame series: the present thread appends to a bounded single-producer ring without a lock; the
   producer drains it into a series it owns and prunes there. Replace the deque by a contiguous
   buffer while you are at it (`core\ReplayBuffer.h`: `SelectFrameSamples`, `AppendFrameSample`,
   `PruneFrameSamples`; keep the rare out-of-order insert). The PresentMon path (`pushSamples`, up
   to 1024 samples per present, 3072 after a resume) goes the same way. The hook-free window feeds
   from another thread through the same calls; it must keep working.
2. D3D12 panel frame: three slots with an atomic front index and sequence, as the D3D11 CPU slots
   (`core\GraphicsDevice.cpp:236-279`) and the Vulkan staging ring already work. Better still, let
   the producer write into a persistently mapped upload ring of the D3D12 host, as the Vulkan
   layer does (`FeedRegisterStaging`, `vk\osd_feed.cpp:581-597`); then the present thread only
   records the copy. The ring's lifetime across resize and release is the hard part; the layer's
   registration under `g_sinkMutex` is the model.

**Tests.** `replay_buffer`, `graph_source`, `framerate_graph` (CTest); a concurrency test for the
ring in the style of `present_stats_concurrency`; `verify-osd-d3d12-native.ps1`,
`verify-osd-frame-cadence.ps1`.

### A8. Shaders compiled at build time

**Finding [verified].** The DXGI paths compile HLSL at run time, on the present thread:

- D3D11 first draw: `EnsureResources` compiles `kVS` and `kPS` (`hook\overlay_d3d11.cpp:65-66`,
  called at 236); the audit says their only user, `OverlayDrawGraph`, has no caller **[audit]**.
  The composite pipeline (`core\GraphicsDevice.cpp:699, 710`) and the chart pipeline
  (`core\ChartLayer.cpp:65, 73`, created on the present thread, `core\OsdInstance.cpp:1986-1991`)
  compile two shaders each.
- D3D12: `CreatePipeline` compiles two shaders with `D3DCOMPILE_OPTIMIZATION_LEVEL3`
  (`hook\overlay_d3d12.cpp:633-652`) in `BuildState` (787, 848), so on the first draw and again
  whenever the swapchain state was destroyed, which a resize or a swapchain replacement does
  (600-630).

A first composite once cost about 2.6 ms (2026-07 note); nothing newer is measured.

**Change.** Compile with `fxc` at build time into header arrays, the way the Vulkan layer embeds
SPIR-V (`vk_layer\CMakeLists.txt:28-40`), in all trees including x86. Keep `D3DCompile` for the
shader tests (`cfx_d3d12_shader_test`). Remove the dead D3D11 pipeline after confirming it has no
user. In D3D12 keep root signature and pipeline state per device and format across state rebuilds.
The game process then no longer needs `d3dcompiler_47.dll`.

**Tests.** `d3d12_shader`, `chart_gpu_raster`, `hdr_composite`; `verify-osd-dlss-resize.ps1`
needs DLSS-G, otherwise test a resize with the D3D12 test app.

### A9. Vulkan layer details

- **A suspension never ends [verified].** Target 0 or another PID suspends the layer and keeps
  the instance; `KeepSuspended` never releases by default (`vk\osd_feed.cpp:106-145`), and
  `MetricsShmGet` returns on a target mismatch before it looks at the publisher's age
  (`hook\overlay_metrics_shm.cpp:154-166`). The view stays mapped after CapFrameX exits. A game
  that was once the target then keeps the instance, its thread and, per present, a fence wait, a
  fence reset and an empty `vkQueueSubmit` (`KeepChainWhileHidden`,
  `vk\vk_compositor.cpp:1287-1316`) until it ends. Release once CapFrameX is gone: probe the
  visibility event as in A4 every few seconds while suspended. Never release because of the hidden
  time alone; that is what "Hide and show keep the renderer" removed.
- **Panel size change [verified].** `RecreateOsdTexture` waits for all of the layer's fences on
  the present thread (`vk\vk_compositor.cpp:819-827`), which is the GPU work of the previous
  present. It happens whenever the panel's pixel size changes, for example after a relayout that
  widened it. Retire the old texture to a fence-polled list, as `retiredSwapchains` does, and
  allocate with headroom so that a value that grows by a digit does not recreate it.
- **`presentLock` across the driver's present [verified].** `CompositorQueuePresent` holds it
  from 1325 to the return, also for a pass-through (1328, 1390). A process that presents from two
  threads on one device is serialized across the driver call. Release it before the down-chain
  call when the present passes through; keep it for the composite and the hidden chain.
- **Bookkeeping before the dormancy gate [audit].** Per present in every Vulkan process: six
  uncontended mutex pairs and the compatibility observation (1340-1354), about 0.1 to 0.3 us
  **[estimated]**. Leave it unless P0 says otherwise.

### A10. DXGI hook details

- **`g_queueMutex` on every `ExecuteCommandLists` [verified].** `OverlayD3D12CaptureQueue` locks
  a global mutex for each call of the game (`hook\overlay_d3d12.cpp:1475-1484`). Compare an
  atomic copy of the queue pointer first and lock only when it differs. The audit adds that
  `hook\present_hook.cpp:490-497` takes the same mutex per present on D3D11 before it tests the
  XeSS-FG flag **[audit]**; test the flag first.
- **Presents that can never draw [audit].** A frame-generation runtime's own pacing thread enters
  `OverlayOnPresent`, waits for the lifecycle lock behind the game thread's draw and runs the
  preamble (host watch, visibility, compatibility poll, `hook\overlay.cpp:419-488`) before it
  declines. A lock-free early return from atomics at the top would spare it.
- **State backup [audit].** `D3D11StateBackup` saves and restores 16 states per present
  (`hook\overlay_osd.cpp:160-218`); external-v2 touches 13 of them, so trimming gains little.
  It neither saves nor clears the geometry, hull and domain shaders. That is a correctness
  question, not a performance one: a game that leaves a geometry shader bound would run the
  overlay quad through it. Raise it with the user.

## 10. Package B: hook-free window

### B1. Finish the measurement series, decide the device

**Finding [measured].** What is left of the hook-free cost on a saturated GPU is the present
itself, ten times per second from the chart layer (`SteppedChartLayer::render`,
`core\ScrollChart.cpp:110-159`, called from the tick at `core\OsdInstance.cpp:1746-1748`). The
runs of `HookFree_Partial_Repaint_DevPlan.md` section 16.6 (off, hardware and WARP in each repaint
mode, at least five runs each) were never made. They answer whether WARP reaches "off" with a tile
design, which is that plan's Phase 3 and the user's decision.

**Change.** Run section 16.6 of that plan as written, with the user in the game. Report the table
and ask for the device decision. Nothing in B2 or B3 should be built before it: if WARP is clean,
both lose most of their value.

### B2. Chart rate

**Finding [verified].** The chart tick rate is the app setting `HookFreeRefreshRate` (1, 2, 5,
10, 20, 30, 60, 120; default 10; `CapFrameX.Configuration\CapFrameXConfiguration.cs:806-819`,
choices in `CapFrameX.ViewModel\OverlayViewModel.cs:904`). The bridge forwards it
(`CapFrameX.OSD.Integration\OsdOverlayBridge.cs:171, 210-211`) to
`cfx_osd_set_hook_free_refresh_hz`, and it also sets the wake period of the managed render loop.
The tick presents the chart layer whenever it runs; the gate before it passes while the replay
clock can advance or something is dirty (`core\OsdInstance.cpp:1546-1568`).

**Options, after B1.**

- **(a)** Follow the target's GPU load: 5 Hz while the target's GPU busy share from
  `ICaptureService.GetProcessActivity` is near 1, the configured rate otherwise, with hysteresis.
  A pure rule class beside `HookFreeHideMethods` with its own test. It needs a setting of its own
  (an "automatic" switch), because `NormalizeHookFreeRefreshRate` accepts only the listed numbers
  and older builds must keep reading a valid one. The user decides (section 16).
- **(b)** Do not present a chart picture that equals the one on screen: remember window start
  column, scale, series generation and line style of the last present in `SteppedChartLayer` and
  skip `render` when they match. This saves the chart present of content-only ticks. Count
  presents next to `chartTicks` in the `tick 10s` line so the saving is visible.

### B3. One present per tick

**Finding [verified].** A tick with new content presents twice: the chart chain (1746-1748) and
the root. A chart with a value badge (`scene_.hasChartBadges`, 1772; `LooksAtTiles`,
`core\RenderPacing.h:96-99`) changes its badge on nearly every tick, so every tick presents twice.
Of the starters only Turquoise Reference shows badges **[audit]**.

**Change, as an experiment after B1.** A switch in `OsdDebug.json` that draws the chart lines on
the retained root as one more dirty rectangle (the `chartsInRoot` path exists, 1762) gives one
present per tick. Whether that is cheaper depends on what B1 shows: on the hardware device the
number of presents counted, not their size. Build the product change only if the experiment wins.

### B4. Stale documentation and small fixes

- Correct the three statements of section 5, item 1 in the OSD `CLAUDE.md`.
- `OverlayWindow::positionFixed` calls `SetWindowPos(HWND_TOPMOST)` on every relayout; its
  `changed` flag only gates the log line (`core\OverlayWindow.cpp:173-189`) **[audit]**. Return
  early when nothing changed.

## 11. Package C: CapFrameX.exe

### C1. `OnlineMetricService`: filter before the thread hop

**Finding [verified].** The frame stream hops to the service's own thread before anything is
filtered: `.Skip(1).ObserveOn(_frameDataScheduler).Where(x => EvaluateRealtimeMetrics())`
(`CapFrameX.PresentMonInterface\OnlineMetricService.cs:219-224`). So every row of every process
costs an enqueue and a wake, then `EvaluateRealtimeMetrics` with its entry lookups, then
`UpdateOnlineMetrics`, which allocates a string for the name comparison (`Replace(".exe", "")`,
365) before it looks at the PID (375-385). For a target row it reads settings through
`_appConfiguration` (`MetricInterval` at 164, `UseDisplayChangeMetrics` at 409, `UsePcLatency` at
435) and calls LINQ `Any()` on the circular buffer (484). The audit counts about 11 allocations
per target row **[audit]**.

**Change.**

1. Filter by PID before `ObserveOn`: compare the row's PID column with a cached text of the
   selected PID, ordinal, without parsing. Keep the checks inside `UpdateOnlineMetrics`; the
   selection can change between the filter and the commit (460-464).
2. Cache "some realtime metric is needed" and refresh it on a timer or when entries change,
   instead of evaluating it per row. Keep the fail-open behavior of the catch (342).
3. `Count > 0` instead of `Any()`; settings cached in fields that follow `OnValueChanged`; column
   indices read once per PresentMon start.
4. Drop the name comparison when the PID matches, or do it without allocating.

**Tests.** `OnlineMetricTelemetryTest`, `PresentModeEntryTest`, and a new case: rows of other
processes never reach the service's thread.

### C2. Online percentiles: one pass, no window copies

**Finding [verified, audit].** Every online metric copies the whole window under
`_lockRealtimeMetric`, which the row thread needs too (`GetOnlineFpsMetricValue`,
`OnlineMetricService.cs:665-687`), and then computes its statistic on that copy
(`CapFrameX.Statistics.NetStandard\FrametimeStatisticProvider.cs:245-248, 316-325`; MathNet's
`Quantile` copies and selects again **[audit]**). The classic rows ask per OSD refresh
(`CapFrameX.Overlay\OverlayEntryProvider.cs:1558-1678` **[audit]**), a tile design per telemetry
snapshot (`CapFrameX.OSD.Integration\OverlayTelemetryService.cs:518-531`), which is every 250 ms
while a capture logs sensors. The window holds frame rate x `MetricInterval` (20 s) samples: 4,800
at 240 FPS, 20,000 at 1000 rows per second. An array of more than 10,625 doubles lands on the
large object heap **[estimated]**, so above about 530 rows per second every percentile allocation
does, which drives gen 2 collections.

**Change.** One statistics pass per window state: copy the window once into a reused buffer,
leave the lock, convert once, sort or select once, and derive every requested metric from that.
Cache the result by a generation counter of the window so that the rows and the design snapshot
share it and a second request for the same state costs nothing. The numbers must not change:
write a parity test against the current `FrametimeStatisticProvider` results over random windows,
including invalid samples (`CopyValidTimings`) and the display-time variant; MathNet's quantile is
R-8 and the capture helper states the same definition.

**Tests.** The parity test; `OnlineMetricTelemetryTest`; the statistics tests.

### C3. Target policy scan off the row thread, and slower

**Finding [verified].** While the in-game renderer is selected, every frame row of the target
with a DXGI runtime calls `TryInjectAsync` (`CapFrameX.OSD.Integration\HookOverlayManager.cs:467`),
which calls `RefreshTargetPolicy` before anything else (726). The policy caches for 2 s
(`HookTargetPolicy.cs:82, 127-150`); then `EvaluateTarget` runs on the PresentMon reader thread:
`Process.GetProcessById`, the image path, up to three `File.Exists`, a ToolHelp module snapshot of
the game with two marshalled strings per module, and for a denied scan an enumeration of all
driver services (432-465). It runs for the whole session, also while the overlay is hidden, and
every other row consumer waits meanwhile. The comment at 722-725 states the intent: an anti-cheat
module that appears later must still hide the overlay. Keep that.

The cost is CPU in CapFrameX.exe, low milliseconds per scan **[estimated]**. Nothing here was
seen to stall the game.

**Change.**

1. Refresh the verdict on the status timer (or a task of its own), not on the row thread. Row
   paths only read the cached verdict. The first verdict for a new PID is needed before an
   injection; evaluate it inside the injection task.
2. Slow down after clean scans: 2 s during the first half minute of a process, then 10 s.
   Invalidate on a PID or runtime change as today (`Invalidate`, 152-162).
3. Cache final denials per PID: a blacklisted name, a Ricochet driver beside the game and a found
   anti-cheat module cannot change for a running process. Transient startup failures
   (`IsTransientStartupFailure`) keep the short interval.
4. Put the schedule in a pure class with a fake clock.

**Tests.** `HookOverlayStatusEvaluatorTest`, `RicochetDetectionTest`, the new schedule tests.

### C4. Status polling while hidden, cached process identity

**Finding [audit; the timer and the Vulkan call site are verified].** The status timer runs at
1 s and at 250 ms while a probe session probes or reconfirms (`HookOverlayManager.cs:242,
1150-1156`). A hidden or idle overlay counts as inconclusive, so the session keeps reconfirming
and the timer stays at 250 ms for the whole hidden time, opening three sections and a `Process`
object per tick (`HookCompatibilityProbeSession.cs:297-311`,
`HookCompatibilityVerdictClassifier.cs:123-125`). Every re-show reads `MainModule` and takes a
module snapshot before it finds nothing to record (`HookOverlayManager.cs:1079-1105`). After
`RestartPending` or `GaveUp` an evidence snapshot repeats every 2 s (1998-2020). For Vulkan
titles `PollVulkanLearning` reads the process identity on every tick
(`HookOverlayManager.Vulkan.cs:56`, `HookOverlayManager.cs:1682-1683`).

**Change.** Stay at 1 s while the only reason for "inconclusive" is hidden or idle. Cache name
and path per PID; they cannot change. Back off the stand-down scans (2, 4, 8 ... 30 s). Skip the
reconfirmation scan when the stored entry would not change.

**Tests.** The probe session and verdict classifier tests; add the interval rule as a pure
function.

### C5. Metrics channel without per-string allocations

**Finding [verified].** `HookMetricsChannel.Publish` rewrites every record on each refresh and
allocates two arrays per string, five strings per entry (`WriteStr`,
`CapFrameX.OSD.Integration\HookMetricsChannel.cs:237-280, 320-329`).

**Change.** Encode header, records and font name into one reused buffer with
`Encoding.UTF8.GetBytes(string, Span<byte>)` and copy it into the view once, inside the same
sequence window. Keep the sequence bump and the QPC write on every refresh: the readers judge the
publisher's age by it (`hook\overlay_metrics_shm.cpp:170-182`), and deployed hooks and layers must
keep working. Skipping identical snapshots is not worth a protocol change, since values differ on
nearly every refresh while a game runs.

**Tests.** The channel tests; the native `metrics_shm` suite reads what the publisher wrote
(byte-identical output before and after for the same entries).

### C6. Design publisher and telemetry snapshot

**Finding [verified, audit].** `OverlayDesignRuntimePublisher` rebuilds the metrics JSON on every
telemetry snapshot and on its 500 ms heartbeat (`Change(null)` at 112, `UpdateSnapshot` 274-292),
through a `JObject` and a string (`OverlayRuntimeDesign.cs:137-153` **[audit]**), and compares the
string with the last one; an equal one is not published again (290). `CreateSnapshot` fills a
dictionary over the whole source catalog whatever the design reads
(`OverlayTelemetryService.cs:484`), although the required ids are known one line above (483).

**Change.**

1. On a heartbeat without a new telemetry snapshot, skip the JSON build unless the design tracks
   averaged keys, whose values move with time. Keep whatever the heartbeat writes to the channel
   header: the native reader checks its freshness (`Fresh(header.writeQpc)`,
   `hook\overlay_design_shm.cpp:75-78`).
2. Fill only the required ids in `CreateSnapshot` when every lease names its ids; keep the whole
   catalog for a consumer that asks for everything (the designer's live preview). Check every
   reader of `OverlayTelemetrySnapshot` first.
3. Optional: write the JSON with `Utf8JsonWriter` into a pooled buffer and compare bytes.

**Tests.** The publisher's tests with the injected heartbeat, `OverlayRuntimeSourceBinderTest`,
`HookFreeDesignFeedTest`.

### C7. Row representation

**Finding [verified, estimated].** Every PresentMon line becomes a `string[]` plus one string per
column (`CapFrameX.PresentMonInterface\PresentMonCaptureService.cs:151-168`), about 33 objects and
1.5 kB, roughly 35,000 allocations per second at 1000 rows per second. Up to six subscribers
parse the PID again and four the frame time **[audit]**. A capture keeps the arrays
(`CapFrameX.Data\CaptureManager.cs:329-330`); the 500-row archive shifts its list on every row
(`RemoveAt(0)`, 562-573).

**Change.**

- **(a) Small.** Make the archive a ring. Measure allocation rate and GC share at a high row rate
  (P0, item 4); that decides (b).
- **(b) Large; the user decides (section 16).** Split and parse once: a row type that carries the
  raw line, the PID, the process name (interned) and the numeric columns the live consumers need,
  with the remaining columns read on demand. Every subscriber of `FrameDataStream` changes
  (`OnlineMetricService`, `PresentMonOverlayMetrics`, `HookFrametimePublisher`, `OsdOverlayBridge`,
  `HookFreeFeedDiagnostics`, `PresentMonProcessList`, `HookOverlayManager.OnFrameRow`,
  `CaptureManager`) and their tests. A capture then keeps one string per row and splits at its
  end.

**Tests.** `CaptureManagerTest`, `PresentMonProcessListTest`, `RecordManagerCacheTest`, the
integration category (elevated) for a real capture.

### C8. Hook-free bridge details

- `ApplyPosition` resolves the monitor index through `EnumDisplayMonitors`
  (`CapFrameX.OSD.Integration\OsdOverlayBridge.cs:517-525`, `DisplayMonitorResolver.cs:65-84`),
  per refresh according to the audit **[audit]**. Cache it by device name and drop the cache on a
  display change.
- The entry feed to the hook-free window allocates a native entry array and five UTF-8
  conversions per entry on each refresh, also for a tile design without a classic row block
  (`CapFrameX.OSD.Interop\OsdHost.cs:741-757`) **[audit]**. Reuse the buffers; skip the feed when
  the active design shows no rows.

## 12. Measuring

- **Present thread:** `presentStats` blocks before and after, same app, same settings, 60 s
  each. Read the maximum and the outlier counts, not only percentiles: a 1 Hz event at 200 FPS
  touches 0.5 % of the presents and never moves p99. Compute the rate of an event against the
  percentile line before you design an A/B.
- **Producer:** the `[diag] producer 10s` line (CPU time, rasters, rebuilds, bytes).
- **Game:** captures through the MCP tools (`cfx_start_capture`, `cfx_compare_records`,
  `cfx_set_osd_options`), at least five runs of 20 s per variant, interleaved with "overlay off",
  `autoDisableOverlay` false. The user chooses the games. The earlier references: Tomb Raider
  (D3D11, about 435 FPS), LEGO Batman, Doom: The Dark Ages (Vulkan, frame generation), Mafia: The
  Old Country (FSR frame generation). Packages A5 to A7 concern CPU-bound games, so include one
  scene that is CPU-bound (low resolution).
- **CapFrameX.exe:** `Measure-CpuLoad.ps1 -Repetitions 3 -BaselineReport <before>\report.json`;
  compare cycles, not CPU %.
- Report runs that contradict the expectation as they are. Keep "measured" and "assumed" apart.

## 13. Tests

- OSD: CTest in all five trees after every package. New tests belong in `apps\design_test`
  (scene, raster and repaint), `apps\diagnostics_test` (counters), `apps\hook_free_pacing_test`
  (pacing), `apps\replay_buffer_test` (series), and the hook's and layer's own suites.
- Several core test apps list the `.cpp` files they compile; a new `.cpp` that `Widget.cpp` or
  `Scene.cpp` needs has to be added to each, a header to none.
- CapFrameX: `vstest.console source\CapFrameX.Test\bin\x64\Release\net10.0-windows\CapFrameX.Test.dll
  /Platform:x64 /TestCaseFilter:TestCategory!=Integration`, and the Integration category from an
  elevated shell for C7.
- `CapFrameX.Test`: the `verify-osd-*` suites named in each package.

## 14. Documentation, memory, prebuilts

- OSD `CLAUDE.md`: the in-game text cadence (A1), the asynchronous design feed (A2), the new
  switches, the corrected statements (B4), whatever A5 changes about the chart layer.
- CapFrameX `CLAUDE.md`: the target policy schedule (C3), the percentile pass (C2), new settings.
- Record measured results in section 18 of this file as you go.
- `external\CapFrameX.OSD-prebuilt\` and the installed Vulkan layers are not part of a package.
  Tell the user at the end which binaries changed; update them only when asked (a clean build,
  the README entry with source revision and hashes).

## 15. Rules

- Chat with the user in German. Every artifact is English: code, comments, log texts, documents,
  commit messages, memory.
- Commit or push only when the user asks. Commit messages are one short line, the user is the
  only author, no `Co-Authored-By` trailer, one commit per topic.
- Never register the Vulkan layer in HKCU and never re-register it in HKLM. Replacing the DLLs in
  `C:\Program Files\CapFrameX\vulkan` needs the user's approval each time.
- No new kind of access to the game process. Package C removes or slows down scans; it adds none.
- Solve a class of problem with a general rule, not with a special case for one game.
- Report measurements as they are and do not claim an effect from a single run.
- Files written by the Write tool have LF line endings; this repository's documents and sources
  use CRLF. Normalize with perl, and count carriage returns with perl only.
- Keep an on-screen task list and stop for the user at the decisions of section 16.

## 16. Decisions that are the user's

1. The default device of the hook-free window (hardware or WARP), after B1.
2. An automatic hook-free chart rate and its switch in the UI (B2 a).
3. Masks and a shader shift for the D3D12 hook and the Vulkan layer (A5 c).
4. The producer thread's priority (A6): decided on 2026-11-13, it stays unchanged (A6, step 3).
5. The row type (C7 b).
6. Whether the missing geometry shader handling of `D3D11StateBackup` is to be fixed (A10).
7. Every commit, every prebuilt update and every replacement of the installed layers.

## 17. Checked and left alone

Reported by the audit passes as cheap or already well done; not read again for this plan.

- The entry feed is gated by the shared-memory sequence; the compatibility channel is polled
  every 250 ms; the status block and the renderer arbiter use interlocked operations only.
- The Vulkan layer intercepts only instance, device, queue and swapchain creation and
  `vkQueuePresentKHR`; `vkQueueSubmit` and `vkAcquireNextImageKHR` pass through. Its stage masks
  are specific, and the graphics path draws only the panel quad.
- The hook-free window commits DirectComposition only on geometry changes, checks the z-order at
  most once per second and re-asserts topmost only when obscured.
- `PresentMonOverlayMetrics.OnFrame` allocates nothing and unsubscribes with the feed; the sensor
  poller is single-flight; placement is published on change only.
- No GC or tiering setting is configured, so the .NET 10 defaults apply (workstation, concurrent).
  Server GC would add threads and CPU; do not switch.

## 18. Results

### 18.1 Measuring tool and Phase 0 (2026-11-14)

- New in CapFrameX.Test: `tools\measure-osd-present-cost.ps1`. It runs the D3D11 and D3D12 test
  apps (x64 by default; 240 FPS, `--gpu-load 2`, 1280 x 720) with the hook against the OSD test
  host and its local test channels, for classic rows (1 s refresh), the FUSION ARL design and the
  Performance Strip (250 ms publish), 6 s warm-up, 40 s measured (the first `presentStats` block,
  which holds the warm-up, is dropped). It reads `cfx_present_stats.log` and the verbose
  `[diag] producer 10s` lines, writes `results.json` and compares with `-BaselineReport`.
- `presentStats` now also times `colorInfo`, `designFeed` and `panelUpload` and counts the
  presents above 50 us, 200 us and 1 ms per segment; the producer writes `[diag] producer 10s`.
- Baseline: `CapFrameX.Test\reports\osd-present-cost\baseline-p0` (OSD 23e73ae plus the counters).
  Findings that changed the order: `colorInfo` costs about 35 us per present only on HDR outputs
  (`ColorManagement.cpp` asked for the SDR white level on SDR too); `designFeed` 60 to 110 us on
  every 250 ms publish (the design JSON parsed on the present thread); on D3D12 the Performance
  Strip copies 4.47 GB of chart copies per 10 s through the producer, and 5600 to 6300 of 9600
  presents spend more than 50 us in the hook, most of it the chart copy (A5).
- Vulkan could not be measured or tested: on this machine (RX 9070 XT) every Vulkan app started
  at medium integrity, vkcube included and with all implicit layers disabled, fails
  `vkGetPhysicalDeviceSurfaceCapabilitiesKHR` with `VK_ERROR_UNKNOWN` (-13). Started elevated,
  vkcube runs (the Integration tests, 18.3), but the loader ignores `VK_LAYER_PATH` for elevated
  processes, so the layer suites could only test the registered layer in `Program Files`, not this
  build. The HDR suite needs Windows HDR switched on; it was off.
- After the OSD commits (`85b2f6a`..`aae744d`) the prebuilts and the installed layers were updated
  (`external\CapFrameX.OSD-prebuilt\README.md`, entry of 2026-11-14). With the new layer installed,
  an elevated `Measure-CpuLoad.ps1 -Renderer InGame` over vkcube loaded it from Program Files, it
  activated and composited without an error. The CPU load runs had left a portable sandbox's layer
  registered in HKLM (both Program Files entries set to 1); that was undone in the same step.

### 18.2 Package A (in-game renderers)

Built (uncommitted, OSD repo):

- A1 text raster gate (`AnyTileDiffers`, `textRaster` knob, `cfx_osd_request_panel`).
- A2 steps 1, 2 and 4: designs submitted to the producer (`cfx_osd_submit_design_json`,
  `cfx_osd_design_result`, `DesignFeed` with the former log lines), the classic rows in one
  `cfx_osd_update_entries_v2` call, `MetricsShmGet` reusing its buffer and strings.
- A3 step 1 only: the SDR white level is queried only for HDR spaces. Step 2 (the HDR query on the
  producer) is open; it needs the HDR suite, see 18.1.
- A4: `HostSeekPolicy`. The dormant hook probes `Global\CfxOsdOverlayVisible` and takes the
  process snapshot only when it opens, plus every 15th throttle period regardless, so the test
  mode (`Local\CfxOsdOverlayVisible_Test`) and an event that another process holds still find a
  host. The probe opens and closes the event; it does not keep it as `g_visibleEvent`.
- A5 (b): only the line rows of a changed chart copy are written (`ChartLineRows`,
  `CpuChartStore::RowsToWrite`).
- A6 wake gate and thread name (priority unchanged, as decided), A7 lock-free frame ring and pooled
  D3D12 panel slots, A10 queue capture and XeSS classification without the queue mutex in the
  common case, B4 (OSD `CLAUDE.md`, `positionFixed` skips an unchanged placement).

Measured, D3D x64 at 240 FPS, baseline -> A1..A7 (one run each; `reports\osd-present-cost\a7`):

| Case | hook > 50 us | hook p99 (us) | rasters / 10 s | producer Mcycles / 10 s | panel MB / 10 s |
| --- | --- | --- | --- | --- | --- |
| D3D11 classic | 103 -> 51 | 57.4 -> 35.0 | 48 -> 10 | 421 -> 133 | 16.4 -> 3.4 |
| D3D11 design | 280 -> 120 | 110.6 -> 118.8 | 48 -> 40 | 1081 -> 956 | 47.1 -> 39.3 |
| D3D11 strip | 189 -> 122 | 116.0 -> 125.2 | 48 -> 40 | 679 -> 631 | 52.2 -> 43.2 |
| D3D12 classic | 271 -> 108 | 79.7 -> 59.6 | 48 -> 10 | 442 -> 163 | 16.4 -> 3.4 |
| D3D12 design | 1982 -> 2009 | 125.8 -> 106.0 | 48 -> 41 | 1262 -> 1170 | 47.4 -> 39.9 |
| D3D12 strip | 6329 -> 5607 | 139.7 -> 140.7 | 48 -> 40 | 2214 -> 2297 | 52.2 -> 43.2 |

- `designFeed` above 50 us: 120 -> 0 in both design cases (A3). A design publishing every 250 ms
  still re-rasters 40 times per 10 s: its values change at every publish, as intended.
- The single maxima (170 to 280 us) moved both ways between runs; one run each does not resolve
  them. The p50 stayed within 1 us.
- Regression suites (D3D x64 and x86): tiles, fonts, hide-cost, chart-motion, d3d12-native,
  frame-cadence, late-attach, generic-d3d11 passed after A1..A7. CTest green in all five trees.
- The Vulkan layer shares the changed core and feed code (design submit, batched rows, panel
  request, line rows, the cached presentation window in `vk_compositor.cpp`); it builds and its
  CTests pass in both trees, but it has not run in a Vulkan process (18.1). Run the Vulkan suites
  before shipping it.

Final state (A1..A8, A5 b, A10; `reports\osd-present-cost\final`, D3D12 twice more in `final-r2`
and `final-r3`), baseline -> final:

| Case | hook > 50 us | hook p50 (us) | hook p99.9 (us) | panelUpload > 50 us | producer Mcycles / 10 s |
| --- | --- | --- | --- | --- | --- |
| D3D11 classic | 103 -> 55 | 16.9 -> 14.0 | 172 -> 116 | 4 -> 1 | 421 -> 130 |
| D3D11 design | 280 -> 120 | 17.7 -> 13.9 | 171 -> 124 | 145 -> 120 | 1081 -> 899 |
| D3D11 strip | 189 -> 121 | 17.9 -> 12.9 | 159 -> 135 | 145 -> 121 | 679 -> 586 |
| D3D12 classic | 271 -> 285 / 553 / 355 | 21.4 -> 20.0 / 22.6 / 19.9 | 158 -> 174 | 0 -> 0 | 442 -> 149 |
| D3D12 design | 1982 -> 2797 / 1582 / 726 | 41.0 -> 43.1 / 40.9 / 31.8 | 168 -> 280 | 0 -> 0 | 1262 -> 1161 |
| D3D12 strip | 6329 -> 3832 / 1881 / 1623 | 57.5 -> 50.7 / 42.9 / 42.9 | 159 -> 242 | 145 -> 52 / 102 / 111 | 2214 -> 2211 |

- A5 (b) shows on the strip: the present thread's chart copy (`panelUpload` p50) fell from 19 to
  2 us, and the presents above 50 us from 6329 to 1600..3800. The producer still composes and
  stores 4.47 GB of copies per 10 s; that is A5 (a) and (c).
- The D3D12 runs scatter widely: in the same runs the game's own Present (`origPresent`, which the
  overlay does not touch) had 0 to 47 presents above 200 us, and the hook's `composite` tail
  (command recording and `ExecuteCommandLists`) moved with it. The p99.9 column and the counts
  above 200 us therefore do not resolve a change; read the medians and the counts above 50 us
  across the three runs. For D3D12 classic and design they show no gain beyond that scatter
  (the A7 run alone had shown 271 -> 108 for classic); only the strip improves in every run.
- After everything, again green: the eight D3D suites, CTest in all five trees (17, 35, 35, 10, 10
  tests, `baked_shaders` included), the CapFrameX unit tests, and `verify-osd-hookfree-hide.ps1`
  with the new core (three hide/show cycles at 144 Hz, worst present 7.8 ms).

A8 built afterwards: `cfx_shader_bake` bakes the D3D11 composite and chart shaders and the D3D12
composite at build time with the former sources, entry points, targets and flags; the `baked_shaders`
CTest compares the bytecode with a run-time compile. The hook, the layer and the core no longer link
`d3dcompiler`; the unused D3D11 graph pipeline is removed. Not done of A8: keeping the D3D12 root
signature and pipeline state across state rebuilds (the compile is gone, the PSO creation stays).

Not built: A9 (Vulkan; untestable here, see 18.1), A5 (a) and (c) ((c) is the user's
decision), the lock-free early return of A10 (the declined presents feed the status coverage and
the frame-generation watchdogs that the host's compatibility verdicts read; an early return before
them changes what the host sees), the geometry shader question of A10 (the user's decision). Checked and left alone in C8: skipping
the hook-free entry feed for a design without rows; the core takes classic values for designs and
its commit cadence from that feed (`commitEntriesLocked`).

### 18.3 Package C (CapFrameX.exe)

Built (uncommitted): C1/C2 one sorted window for the online FPS metrics (`OnlineFpsWindow`, equal
to the statistic provider to the bit) and only the target's rows on the metric thread; C3 target
policy verdicts refreshed off the row thread (`HookTargetPolicyCache`, 2 s then 10 s, final denials
kept); C4 1 s status poll while paused, cached target identity, evidence rescans 2..30 s; C5 metrics
channel encoded into reused buffers (byte for byte as before); C6 metrics JSON reused for unchanged
inputs, telemetry snapshots with the leased sources only; C7 (a) the capture archive as a ring;
C8 the hook-free monitor index cached by display layout. Unit tests: 3631 passed after C1..C5,
3636 of 3636 after everything (a short hardware name needed its full name in the narrowed
snapshot; fixed, `ShortHardwareNames_FollowTheFullNamesAndOnlyNeedThem`).

Elevated (2026-11-14, from an elevated PowerShell the user confirmed): the unit tests again,
3636 of 3636, and the `Integration` category, 8 of 8, with vkcube, PresentMon and real captures
(the capture archive ring of C7 a held 155 to 165 rows per capture). vkcube runs elevated on this
machine; only the medium-integrity Vulkan processes fail (18.1).

The first CPU load round found a regression of C1: the realtime demand check started its timestamp
at `long.MinValue`, `now - long.MinValue` overflowed to a negative value, the check never came due,
and without a telemetry lease (a tile design, the remote API's design feed) no row reached the
metric thread. The classic online metrics and `/api/metrics` read NaN; the report showed no target
FPS outside the design scenarios. Fixed with a first-check flag, covered by
`RowsOfTheTargetReachTheMetricsThroughTheStream`, which drives the real row stream (the earlier
test called `UpdateOnlineMetrics` directly). The current-build run of that round is void. After the
fix: 3639 of 3639 unit tests.

CPU load of CapFrameX.exe plus its PresentMon process (`Measure-CpuLoad.ps1`, 30 s warm-up, 60 s
measured per scenario, D3D11 test app as target; `temp\cpu-load\pkg-c-*`). Baseline rounds 1 to 3
against the current rounds 2 and 3, run alternately; current round 1 is void (the regression
above). Total Mcycles/s, mean (range):

| Scenario | Baseline | Current | Change of the means |
| --- | --- | --- | --- |
| Idle | 166 (158-180) | 101 (78-124) | -39 %, below the baseline range |
| PresentMon | 214 (206-230) | 167 (166-169) | -22 %, below |
| InGame-Hidden | 201 (191-213) | 188 (172-204) | -7 %, overlaps |
| InGame-NoSensors | 210 (191-235) | 219 (188-251) | +4 %, overlaps |
| InGame-Sensors | 234 (211-254) | 203 (192-213) | -13 %, overlaps |
| InGame-Design | 247 (229-265) | 196 (191-201) | -21 %, below |
| HookFree-Hidden | 176 (161-194) | 170 (165-176) | -3 %, overlaps |
| HookFree-NoSensors | 215 (208-223) | 221 (214-228) | +3 %, overlaps |
| HookFree-Sensors | 310 (220-474) | 219 (217-220) | -30 %, overlaps |
| HookFree-Design | 252 (240-259) | 235 (211-259) | -7 %, overlaps |
| Rtss-Hidden | 211 (194-234) | 193 (174-212) | -8 %, overlaps |
| Rtss-NoSensors | 289 (201-464) | 204 (191-217) | -29 %, overlaps |
| Rtss-Sensors | 224 (193-266) | 195 (191-199) | -13 %, overlaps |

- Clear only where the expectation is strongest: with no or one consumer of the rows, PresentMon's
  rows of every other presenting process no longer travel to the online metric thread (C1), and a
  tile design's metrics JSON is no longer rebuilt per heartbeat (C6).
- The target ran at 144 FPS in some runs and at 240 in others (a few at 19 to 24, one at 3), and
  CapFrameX's work grows with the rows; normalized per target frame the changes scatter between
  -17 % and +21 %. These runs do not resolve differences of about 10 % per scenario.
- The session's shells ran at medium integrity, so the elevated steps ran from a PowerShell the
  user confirmed through UAC.

The baseline is a worktree of `a7fecbb6` at `artifacts\cpuload-base`, built with the same native
DLLs as the current build (equal hashes of hook, layer and core), so the comparison measures the
C# changes alone. To measure again, from an elevated PowerShell in the repository root, the target
being the D3D11 test app (vkcube runs only elevated on this machine, 18.1):

```powershell
$common = @{ Renderer = 'InGame','HookFree','Rtss'; OverlayDesign = '*FUSION*ARL*'
  TargetPath = 'E:\Code\CapFrameX.Test\bin\x64\cfx_d3d11_x64.exe'; TargetProcessName = 'cfx_d3d11_x64'
  TargetArguments = '--fps 240 --gpu-load 2 --width 1280 --height 720 --duration 900' }
.\scripts\Measure-CpuLoad.ps1 @common -OutputDirectory temp\cpu-load\pkg-c-base `
  -AppDirectory artifacts\cpuload-base\source\CapFrameX\bin\x64\Release\net10.0-windows
.\scripts\Measure-CpuLoad.ps1 @common -OutputDirectory temp\cpu-load\pkg-c-current `
  -BaselineReport (Get-ChildItem temp\cpu-load\pkg-c-base -Recurse -Filter report.json).FullName
```

Remove the worktree afterwards with `git worktree remove artifacts\cpuload-base`.

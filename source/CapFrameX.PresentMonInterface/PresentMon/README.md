# Bundled PresentMon

CapFrameX bundles its own build of the x64 console executable, `CX-PresentMon-2.6.1-x64.exe`:
Intel's [PresentMon v2.6.0](https://github.com/GameTechDev/PresentMon/releases/tag/v2.6.0)
release (September 21, 2026) plus the fixes below, which were tested as builds cfx.1 and
cfx.2, and the frame generation fixes of parts 3 and 4.
`CaptureServiceConfiguration.PresentMonAppName` names the file, and CapFrameX shows the
version taken from that name.

- Source: branch `cfx/bugfixes` of [CXPresentMon](https://github.com/DevTechProfile/CXPresentMon),
  Intel tag v2.6.0 plus the fixes, with the version set to 2.6.1. `--help` prints
  "PresentMon 2.6.1 (CapFrameX build, based on Intel PresentMon 2.6.0)".
- SHA-256: `ce7da452e8e2cc5154e5e653bb5f3f7a424b1423bea87fa240f92196921d8218` (unsigned build
  of October 31, 2026; the hash changes when the file is signed).
- Not signed yet. The previous 2.6.1 build (SHA-256
  `987aaf638d7bcee2cbf0669b9797f9a8cc799079a8aa0a51a6b13b25363354aa`) was signed by
  "Open Source Developer Mark Daniel Fangmeyer" (Certum, RFC 3161 timestamp).
- Upstream asset it replaces: [PresentMon-2.6.0-x64.exe](https://github.com/GameTechDev/PresentMon/releases/download/v2.6.0/PresentMon-2.6.0-x64.exe),
  SHA-256 `b2a706bc6ad475749e3b7e3409263aa1e6906d45bdcf993f6dbc0f660188f1af`.
- License: [MIT](license.txt), copied from the release's source tree.

## Changes, part 1 (test build cfx.1)

The CSV output is the same as upstream 2.6.0. The build fixes internal tracking that
grew without bound during long sessions. A user reported PresentMon reaching about
260 MB and a full CPU core while playing Bodycam on a Radeon RX 9060 XT, also with PC
latency tracking off.

The cause is the app timing data behind `--track_app_timing`, which CapFrameX always
passes. Bodycam sends about 250 Intel-PresentMon app timing events per second but
never `AppPresentStart`. Upstream 2.6.0 matches app timing entries to presents by
their present start time, so these entries are never assigned, and neither pruning
rule removes them before the process exits. Because every present scans the whole
map, CPU time per present grows along with memory until the event thread saturates
a core. Reported upstream as
[GameTechDev/PresentMon#695](https://github.com/GameTechDev/PresentMon/issues/695).

- App timing and PC latency entries of any process are now dropped once their newest
  timestamp is more than four deferral periods (8 s) old (`IsStaleAppTimingData`).
  This is the fix for the report above.
- GPU work tracking (HAGS hardware queues): packets were only removed on a matching
  `QueuePacket_Stop`, so missed matches would grow the queue for the whole session
  and every unmatched completion would scan it. Queues are now limited to 1024
  packets per node, completions are matched anywhere in the queue, and `HwQueue_Stop`
  releases destroyed queues, so their work no longer counts as running. This is
  hardening only; it never triggered on the reporting system.
- Presents waiting for DWM are no longer queued twice.
- Swap chains are pruned relative to the newest present of a batch, and process
  start/stop events no longer stall behind a reused process id.
- Optional diagnostic log: set the environment variable `PRESENTMON_DIAG_LOG` to `1`
  (writes `%TEMP%\PresentMonDiag_<pid>.log`) or to a file path. Off by default.

## Changes, part 2 (test build cfx.2)

Build cfx.2 added one fix to cfx.1. A frame-generation runtime that types its frames
through the PresentMon provider (XeSS-FG) marks every present as waiting for a frame
id, and only the runtime's next typed present releases it. When the runtime stops
typing frames (XeSS-FG switched off or replaced by FSR frame generation), the last
typed present stays deferred and holds back the output of every process until the
completed-present ring overflows. In AC Shadows with CapFrameX's ring of 4096 that
took about 60 s, during which CapFrameX received no frames, and the overflow dropped
103 presents. The stale-deferral check now also clears this wait, so the stall ends
after the 2 s deferral limit. Upstream 2.6.0 has the same gap.

## Changes, part 3 (frame generation display times)

The version stays 2.6.1. Two changes, both found with an ETL of Mafia: The Old Country
with FSR frame generation on a Radeon RX 9070 XT:

- Display times: about 30% of the frames had `MsUntilDisplayed` and
  `MsBetweenDisplayChange` set to NA, almost all of them generated frames, and the
  following Application frame got about 16 ms instead of 8 ms. The kernel events show
  that every flip was displayed. The driver uses the hardware flip queue, and frame
  generation presents the generated and the application frame about 0.1 ms apart, so
  the next present reaches the Win32k `InFrame` state before the previous one is
  scanned out. PresentMon then marked the previous present as discarded and stopped
  tracking its submit sequence, so its VSyncDPC no longer found it. A flip that
  `MMIOFlipMultiPlaneOverlay_Info` reports as waiting for VSync or HSync is now kept
  until its VSyncDPC arrives; if it is superseded after all, the next displayed present
  on the swap chain completes it as not displayed, as before. Upstream 2.6.0 has the
  same problem.
- Frame type 101 (AMD FSR frame generation) from upstream pull request
  [GameTechDev/PresentMon#680](https://github.com/GameTechDev/PresentMon/pull/680): the
  `FrameType` column now reads `AMD FSR FG` instead of `Unknown`. CapFrameX counts both
  as generated frames (`PresentMonProcessList.IsGeneratedFrame`).

## Changes, part 4 (animation error with PC latency and frame generation)

In CapFrameX captures of Mafia: The Old Country with FSR frame generation, the animation
error was missing for 0 to 100% of the application frames, varying between captures and
already with the earlier builds. The game sends PC latency markers. PresentMon matched
their timing data to every completing present and marked the entry as used. The FSR FG
frame is presented and completed before the application frame, and both follow the
application frame's PresentStart marker, so the generated frame took the entry and the
application frame got none. Live, ETW events are not delivered in timestamp order, so an
application frame occasionally got its data. The animation error source then switched to
PC latency for the swap chain, and from then on most application frames had no
simulation start and no animation error. Replaying an ETL never showed this, because
there the source stays with the CPU start. Upstream 2.6.0 has the same matching.

Generated frames of runtime frame generation (XeSS-FG, AFMF and FSR FG frame types from
the PresentMon provider) now read PC latency data without taking it, and only data newer
than any data an application frame already took. With XeSS-FG the application frame is
presented first, so older data is stale. Driver frame generation reported through
`FlipFrameType` events is not affected.

## Capture compatibility

`PresentMonServiceConfiguration` starts PresentMon with frame-type tracking, app
timing, QPC timestamps in milliseconds, a configurable circular buffer and the
`--write_display_metadata` argument added in 2.6.0. The CSV has 32 columns with PC
latency tracking, or 31 without it: `VidPnSourceId` and `LayerIndex` follow
`PresentFlags`, `PresentId` is the last column.

CapFrameX stores `LayerIndex` per frame and summarizes it as "Display Layer" in the
record details. PresentMon only fills the three columns for presents it sees in a
`MMIOFlipMultiPlaneOverlay3_Info` event (version 8 or later, requires
`--track_frame_type`) and writes 0 otherwise, so rows with `PresentId` 0 count as
frames without layer data. Windows builds without that event version report none,
and neither do the upstream `Tests/Gold` traces.

## Changes from 2.5.1

The upstream release adds tracking of newer OS DDI flip events, display metadata,
and D3D12 pipeline-state compilation metrics. ETL playback no longer prunes old
swap-chain data between batches, making replay more deterministic. The present
overflow counter is now atomic. The new PSO metrics are not exposed by CapFrameX's
current CSV configuration.

The release's telemetry-provider, Intel overlay UI, and Windows service changes
apply to the separate Intel PresentMon components; CapFrameX bundles only the
console executable. See the release notes linked above for the full change list.

## Update verification

- Intel Authenticode signature and GitHub release SHA-256 verified.
- Release x64 application, test project, and WiX 3.14.1 installer built
  successfully, including MSI validation.
- 72 existing capture-configuration, capture-manager, frame-filter, feed-diagnostic,
  and record-manager tests passed on .NET 10.
- All six upstream `Tests/Gold` ETL fixtures replayed with both 2.5.1 and 2.6.0,
  with PC latency enabled and disabled: 24 successful runs, identical CSV output
  for every old/new pair, and every row matched CapFrameX's expected column count.
- Circular buffer sizes 2048, 4096, and 8192 accepted by the new executable.

These checks cover recorded traces and application tests, not a live game session.

## Build cfx.1 verification

- All six upstream `Tests/Gold` ETL fixtures, with PC latency enabled and disabled:
  CSV output byte-identical to the Intel 2.6.0 executable.
- Live side-by-side runs against the Intel 2.6.0 executable (separate ETW sessions,
  CapFrameX arguments): Radeon RX 6800 XT without HAGS, and Arc B580 with HAGS in a
  D3D12 game with XeSS frame generation. Frame types matched on all 106,423 compared
  rows, and metric differences stayed within the timestamp jitter between sessions
  (99.9% below 0.04 ms).
- Synthetic GpuTrace tests: with completions that never match, the Intel 2.6.0 code
  grows by 16 bytes per packet and slows down with every completion. The fixed code
  stays at 1024 queued packets and processes 2 million packets in 2.3 s.
- The reporting user ran the test build with its diagnostic log for 86 minutes
  (RX 9060 XT with HAGS, Bodycam, six captures of up to 10 minutes, PC latency on).
  The app timing map held 650 to 1,160 entries from the game, none of them ever
  matched to a present. PresentMon stayed at 7.7 to 7.9 MB and 0.1% CPU after 40 and
  60 minutes, and the frame time spikes from the report were gone. The HAGS queues
  held at most 68 packets, so the 1024 limit never applied.
- Intel 2.6.0 from the GitHub release on a Radeon RX 9070 XT with HAGS, Stalker 2
  plus an OBS recording with the AMD hardware encoder: no growth, since that game
  sends no such app timing events.

## Build cfx.2 verification

- The stall was reproduced with cfx.1 and its diagnostic log in AC Shadows on a
  Radeon RX 9070 XT: switching XeSS-FG off left `completedWaitingFrameId=1` and
  `ready=0` while `completed` grew to the ring size, then `overflowed=103`.
- Build cfx.2, same game and system, XeSS-FG switched off (the game recreated its
  swap chain): the waiting present was released, `completed` stayed at 1 or 2,
  nothing overflowed and frames kept being dequeued. The in-game overlay's
  PresentMon chart never stood still: deliveries kept arriving at the usual ~1 s ETW
  cadence and the replay ran at 1.000x.

## Build 2.6.1 verification

The 2.6.1 source equals cfx.2 except for the version number and the `--help` text.

- PresentMonTests: all 25 tests passed, including the 18 `Tests/Gold` ETL comparisons
  (six cases, each with the default, v1 and v2 metrics).
- Run with CapFrameX's arguments, including PC latency, on a gold ETL: the CSV header
  matches `COLUMN_HEADER_WITH_PC_LATENCY` exactly.
- `signtool verify /pa` succeeded on the signed executable.

## Part 3 verification

- Mafia: The Old Country ETL (RX 9070 XT, FSR frame generation, hardware flip queue,
  no lost events), CapFrameX arguments: NA display times went from 987 of 3,295 frames
  to 0. Displayed FPS changed from 78.7 to 112.2 (1% low from 48.9 to 90.3, frame time
  standard deviation from 4.70 to 1.11 ms), which matches the VSyncDPC times of all
  flips in the trace. Header and column count match `COLUMN_HEADER_WITH_PC_LATENCY` and
  `COLUMN_HEADER_WITHOUT_PC_LATENCY`.
- AC Shadows ETL (XeSS-FG): CSV byte-identical to the previous 2.6.1 build.
- `Tests/Gold`: all 18 comparisons (six cases, default, v1 and v2 metrics) match, apart
  from floating-point rounding in the last digit.
- PresentMonUnitTests: all 299 tests passed, including a new AMD FSR FG metrics test.

These checks cover recorded traces and unit tests, not a live game session.

## Part 4 verification

- Live, Mafia: The Old Country running with FSR frame generation, two 10 s captures with
  CapFrameX's arguments in a separate ETW session: application frames now carry their own
  PC latency simulation start (the animation time restarts at 0 when the source switches
  to PC latency), and 99.5% of them have an animation error. With the previous build the
  application frames never got that data in fresh sessions.
- Mafia ETL, CapFrameX arguments: animation error for 1,646 of 1,649 application frames,
  PC latency 62.7 ms for application and 53.8 ms for FSR FG frames (previously 62.5 and
  53.7 ms). Header and column count match both column layouts.
- AC Shadows ETL (XeSS-FG): identical except 2 of 1,034 XeSS-FG PC latency values, the
  rows in which the previous build used stale timing data.
- `Tests/Gold`: CSV output identical to the part 3 build in all 18 comparisons.
- PresentMonUnitTests: all 302 tests passed, including three new PC latency frame
  generation tests; the first of them fails with the previous matching.

The live captures were short, fresh sessions; a long CapFrameX session, where the
problem appeared, was not part of these checks.

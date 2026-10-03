# Bundled PresentMon

CapFrameX bundles its own build of the x64 console executable, `CX-PresentMon-2.6.1-x64.exe`:
Intel's [PresentMon v2.6.0](https://github.com/GameTechDev/PresentMon/releases/tag/v2.6.0)
release (September 21, 2026) plus the fixes below, which were tested as builds cfx.1 and
cfx.2. `CaptureServiceConfiguration.PresentMonAppName` names the file, and CapFrameX shows
the version taken from that name.

- Source: branch `cfx/bugfixes` of [CXPresentMon](https://github.com/DevTechProfile/CXPresentMon),
  Intel tag v2.6.0 plus the fixes, with the version set to 2.6.1. `--help` prints
  "PresentMon 2.6.1 (CapFrameX build, based on Intel PresentMon 2.6.0)".
- SHA-256: `987aaf638d7bcee2cbf0669b9797f9a8cc799079a8aa0a51a6b13b25363354aa`
- Signed by "Open Source Developer Mark Daniel Fangmeyer" (Certum, RFC 3161 timestamp).
- Upstream asset it replaces: [PresentMon-2.6.0-x64.exe](https://github.com/GameTechDev/PresentMon/releases/download/v2.6.0/PresentMon-2.6.0-x64.exe),
  SHA-256 `b2a706bc6ad475749e3b7e3409263aa1e6906d45bdcf993f6dbc0f660188f1af`.
- License: [MIT](license.txt), copied from the release's source tree.

## Changes, part 1 (test build cfx.1)

The CSV output is the same as upstream 2.6.0. The build fixes internal tracking that
grew without bound during long sessions, reported as PresentMon reaching hundreds of
MB and a full CPU core on a Radeon RX 9060 XT with hardware-accelerated GPU
scheduling (HAGS):

- GPU work tracking (HAGS hardware queues): packets were only removed on a matching
  `QueuePacket_Stop`, so missed matches grew the queue for the whole session and
  every unmatched completion scanned it. Queues are now limited to 1024 packets per
  node, completions are matched anywhere in the queue, and `HwQueue_Stop` releases
  destroyed queues, so their work no longer counts as running (GPU busy equal to the
  frame time).
- Presents waiting for DWM are no longer queued twice.
- App timing and PC latency data that can no longer be matched is pruned for every
  process, not only for the process that just presented.
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
- The HAGS growth itself was not reproduced on available hardware (the Arc B580
  delivers matching completions), so it is confirmed only by the user report.

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

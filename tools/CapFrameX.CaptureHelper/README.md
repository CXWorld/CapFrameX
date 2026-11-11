# CapFrameX Capture Helper

A small capture app for Windows 7 and later, built around Intel's PresentMon 1.10.0: the last
PresentMon that starts on Windows 7. From 2.0.0 on, PresentMon imports `SetThreadDescription`
(Windows 10 1607) statically, and from 2.4.0 on every start also calls `TdhLoadManifestFromBinary`
(Windows 10). CapFrameX itself runs on .NET 10, which does not support Windows 7.

## Requirements

- Windows 7 SP1 or later, with the .NET Framework 4.8 (Windows 7 does not ship it; without it Windows
  offers the download).
- Administrator rights: the app asks for them at start, since PresentMon's ETW session needs them.
- The x86 package for 32-bit Windows, the x64 package for 64-bit Windows. Both work on 64-bit Windows.

## Use

**Capture tab**
- *Capture hotkey*: F11 (default), F10, F9, Ctrl+F11, Alt+F11, Shift+F11 or Scroll Lock. F12 is not
  offered: Windows reserves it for debuggers. The first press starts a capture, a second one ends it
  early. A sound marks the start and the end; the file follows 2.5 s after the end (see "Capture
  buffer").
- *Capture duration*: 10 s to 5 min, or until stopped (at most 30 min).
- *Capture folder*: where the CSV files go, by default `Documents\CapFrameX Capture Helper`.
- The list shows every process PresentMon sees presenting frames, with its frame rate, updated every
  second. A process that stops presenting leaves it after 3 s.
- The target is the process selected in the list. It stays the target while its process runs, also
  when it presents nothing for a while (loading screen, minimized game), and is cleared when the process
  exits. A capture whose target exits ends right away and is saved. Without a selection, the hotkey
  captures the application in the foreground, which is the game while one plays. *Start capture* needs a
  selection, since the helper itself is in the foreground then.
- The file contains only the target's frames: PresentMon records every process, and the helper keeps
  the rows with the target's process ID (see below why PresentMon is not filtered).

**Analysis tab**: the capture folder as a tree (newest files first) and *Open file…* for any other
PresentMon CSV. A new capture opens there by itself. It shows the frame times over time and:
- *Average FPS*: frames × 1000 / sum of the frame times,
- *P1 FPS* and *P0.2 FPS*: the 1st and 0.2nd percentile of the per-frame FPS (1000 / frame time),
  computed as CapFrameX does (MathNet's quantile, R-8), so both report the same numbers.

The frame time is PresentMon's `msBetweenPresents`. Files PresentMon wrote itself (UTF-16) and
PresentMon 2.x files (`FrameTime`) open too; with several processes in one file, the one with the
most frames is shown.

## Files

A capture is a PresentMon CSV, `<process>_<yyyy-MM-dd_HH-mm-ss>.csv` (UTF-8): PresentMon's header and
its rows of the target, unchanged:

```
Application,ProcessID,SwapChainAddress,Runtime,SyncInterval,PresentFlags,Dropped,TimeInSeconds,msInPresentAPI,msBetweenPresents,QPCTime
```

Settings and the log are in `%APPDATA%\CapFrameX\CaptureHelper` (`settings.xml`, `CaptureHelper.log`).
The log records PresentMon's arguments, its stderr and every capture; ask Windows 7 users for it.

## How PresentMon runs

The helper starts PresentMon once and keeps it running:

```
PresentMon-1.10.0-<x86|x64>.exe -session_name CapFrameXCaptureHelper -stop_existing_session
    -output_stdout -qpc_time -no_track_display -exclude dwm.exe -exclude CapFrameX.CaptureHelper.exe
```

- `-output_stdout`: the rows of all processes arrive on stdout (PresentMon flushes after each row);
  the process list counts them.
- `-qpc_time`: every row carries its present time on the QPC clock, the clock of
  `Stopwatch.GetTimestamp()`. The capture buffer below cuts the captures by it.
- No process filter for PresentMon: in 1.10.0, `-process_name` only filters what PresentMon writes, and
  `-process_id` would need a PresentMon restart for every target. After a start, the first rows arrived
  only 1 to 4 s later (measured with vkcube), so a capture right after selecting a target would miss its
  beginning, and the list would lose every other process. The helper filters the rows itself.
- `-no_track_display`: a present is complete once the system took it over. The frame times need nothing
  more. On current Windows 11 builds 1.10.0 does not know the newer flip events and completes hardly any
  present with display tracking (10 rows instead of about 2,600 in 5 s on build 26300). OpenGL and Vulkan
  games still appear through DxgKrnl's PresentHistory events, which 1.10.0 enables either way (on
  Windows 7 through its Win7 DxgKrnl provider). The display columns (`PresentMode`,
  `msBetweenDisplayChange`, ...) are therefore missing.
- Stopping: the helper ends the ETW session with `-terminate_existing -session_name CapFrameXCaptureHelper`.
  PresentMon then exits by itself; killing it would leave the session running, which PresentMon's
  README names as a cause of instability on Windows 7.
- A PresentMon left behind by a crashed helper is ended at the next start: the helper stops its session
  and waits for it to exit before it starts its own. Taking the session over with
  `-stop_existing_session` alone does not work: on its way out, the old instance stops its session
  through its old handle, which ended the new PresentMon as well (0.1 s after its start). If PresentMon
  exits on its own, the Capture tab says so and offers *Restart PresentMon*.

## Capture buffer

ETW hands PresentMon's rows over in batches, 0.3 to 2 s after their present (measured on Windows 11), so
the stream fluctuates and its arrival times say nothing about the frames. A capture is therefore buffered
as in CapFrameX's `CaptureManager` and cut by the present time (`QPCTime`):

1. **Archive**: a ring of the last 500 rows of every process (`ARCHIVE_LENGTH`). A capture starts with
   its target's rows from it, so the frame before the start request is there even after a pause in the
   target's frames. Archive and capture meet under the lock the rows come in under, so there is neither
   a gap nor an overlap between them; the main app removes an overlap by QPC, because its archive fills
   on until its asynchronous capture subscription runs.
2. **Capture buffer**: every row of the target from the start request on, whatever its present time.
3. **Gather**: 2.5 s after the end (`PRESICE_OFFSET`), when the rows of the last frames have arrived, the
   capture is cut. The end is the set duration or the stop request (a second hotkey press, *Stop
   capture*, the target's exit).
4. **Cut** (`CaptureWindow`, as `GetAdjustedCaptureData` and `DropFramesOfFirstStartTime`): the reference
   is the last frame before the start request, or the first frame when none came before it. A capture
   that ran its set duration (stopped at most 0.2 s early) keeps that duration from the first frame after
   the reference. A stopped one keeps the frames after the first frame of the start request up to the
   reference plus the time it ran.

Against a port of the main app's code, 4,000 random streams (20 to 300 FPS with jitter, frame pairs with
one present time, start requests before the first frame) gave the same frames, except one frame within
1 ms of the end in 121 of them: the main app rounds the times to milliseconds, the helper compares QPC
ticks. The log names every capture's window (start request, reference, first frame, end, rows from the
archive). Not taken over: the main app keeps only the dominant swap chain and refuses a capture of
several instances of one executable; the helper keeps the target's process ID.

## Build

```
powershell -ExecutionPolicy Bypass -File build.ps1
```

builds both variants into `publish\x86` and `publish\x64` and zips each
(`CapFrameX.CaptureHelper-<version>-<x86|x64>.zip`) and both together (`CapFrameX.CaptureHelper-<version>.zip`,
folders `x86` and `x64`). `-Platform x86` (or `x64`) builds one variant only, e.g. while the other one
runs from `publish`; the zip with both then takes the other from its last build. Without the script:
`dotnet build -c Release -p:Platform=x86` (or `x64`, the default). Needs the .NET SDK and the .NET
Framework 4.8 targeting pack.

## PresentMon binaries

From the [v1.10.0 release](https://github.com/GameTechDev/PresentMon/releases/tag/v1.10.0) (MIT,
`PresentMon/license.txt`). Intel published them unsigned; sign them with the release before shipping.

| File | Size | SHA-256 |
|---|---|---|
| `PresentMon-1.10.0-x64.exe` | 388,096 | `e57a2f8ee1de1ef1a5516d875f1b115e881943cd729fe9c5a2f88b1dc79a8a3b` |
| `PresentMon-1.10.0-x86.exe` | 321,024 | `2eeaf9050922a4cf16ba64ad394dcc8d6318d9fee282ff8d76dfdbf43455934f` |

## Tested

On Windows 11 (build 26300) with vkcube, both variants: process list; timed 10 s captures (every file
starts at the first frame after the start request and spans 10.000 s from it, all rows inside the logged
window, only the target's process ID); captures stopped early with the button and, before the capture
buffer, with the hotkey; the target kept through 10 s without frames (vkcube suspended); an open-ended
capture ended when its target was killed; the start after a killed helper; *Restart PresentMon* after a
killed PresentMon; the window cut against the main app's code and the analysis values against MathNet;
clean exit without a PresentMon process or ETW session left. Not yet on Windows 7.

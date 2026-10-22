# CPU load test

`scripts/Measure-CpuLoad.ps1` measures how much CPU CapFrameX costs while it runs in the background
of a game: PresentMon reading, sensor reading and the overlay, **without capturing**. It starts the
real application once per scenario and measures CapFrameX.exe and its PresentMon process from the
outside, so the numbers are what Task Manager or Process Explorer would show, only reproducible.

## Requirements

- Windows, 64-bit PowerShell (Windows PowerShell 5.1 or PowerShell 7), **elevated** (PresentMon uses ETW).
- The application built (`Release|x64`), or any CapFrameX folder passed with `-AppDirectory`.
- No running CapFrameX: only one instance can run.
- A Vulkan-capable GPU for the default target vkcube (`3d-test-app\vkcube.exe` in the app output).
- For the `Rtss` scenarios an installed RTSS; without it they are skipped. For the `InGame` scenarios
  with vkcube a registered CapFrameX Vulkan layer (HKLM), otherwise the overlay is not drawn.

## Quick start

```powershell
# From an elevated PowerShell in the repository root
.\scripts\Measure-CpuLoad.ps1
```

All scenarios, once each, with 30 s warm-up and 60 s measurement: about 25 minutes. The report
lands in `temp\cpu-load\<timestamp>\report.md`. Close other applications first, especially ones
that present frames (browsers, chat clients): PresentMon traces every process, and CapFrameX may
pick one of them as the overlay target.

```powershell
.\scripts\Measure-CpuLoad.ps1 -ListScenarios                    # what can be measured
.\scripts\Measure-CpuLoad.ps1 -Scenario Idle,PresentMon,HookFree-* -Repetitions 3
.\scripts\Measure-CpuLoad.ps1 -Renderer HookFree -OverlayDesign 'FUSION*'
.\scripts\Measure-CpuLoad.ps1 -Settings @{ OSDRefreshPeriod = 250; HookFreeRefreshRate = 60 }
```

`-Settings` takes a hashtable, so call the script from a PowerShell prompt, not with `-File` from cmd.

## Scenarios

| Scenario | Game | Overlay | Measures |
| --- | --- | --- | --- |
| `Idle` | no | renderer `Off` | CapFrameX and PresentMon with nothing to read |
| `PresentMon` | yes | renderer `Off` | PresentMon reading of the game: process list, frame archive, online metrics |
| `<R>-Hidden` | yes | renderer R selected, toggled off (ALT+O) | what a selected but hidden overlay still costs |
| `<R>-NoSensors` | yes | on, classic rows without hardware sensors | overlay pipeline and renderer without sensor polling |
| `<R>-Sensors` | yes | on, classic rows as configured | the above plus sensor reading |
| `<R>-Design` | yes | on, tile design `-OverlayDesign` | a tile design instead of the classic rows (InGame, HookFree) |

R is `InGame`, `HookFree` or `Rtss` (`-Renderer`). The baselines use the renderer `Off` on purpose:
with the in-game renderer selected CapFrameX injects into the game and follows every PresentMon row
even while the overlay is toggled off, which `InGame-Hidden` shows separately.

`report.md` lists the differences between neighbouring scenarios as contributions:
PresentMon reading = `PresentMon - Idle`, overlay = `<R>-NoSensors - PresentMon`,
sensor reading = `<R>-Sensors - <R>-NoSensors`, tile design = `<R>-Design - <R>-Sensors`.

## How a run works

1. The app output is mirrored into `temp\cpu-load\sandbox\app` and runs there in portable mode;
   `portable.json` points every folder at the run's own directory. Nothing in `%APPDATA%`, the
   documents folder or the build output is touched.
2. **Priming**: one start creates the configuration every run copies (defaults, or the copy of
   `-ConfigurationSource`). Through CapFrameX's MCP endpoint it hides the hardware sensor rows of
   the overlay profile and lets CapFrameX save that profile: that is the `NoSensors` variant.
3. **Each run** gets a fresh copy of that configuration plus its scenario settings (renderer, overlay
   on/off, design; no update check, tray start), starts CapFrameX, starts the target only once
   CapFrameX is up (RTSS and the Vulkan layer must be in place before the game creates its
   swapchain), waits until `/api/processes` lists it, warms up and measures.
4. CapFrameX is closed with `WM_CLOSE` (killed after 30 s), the target is closed, and an RTSS that
   CapFrameX launched is stopped so it does not hook the next scenario's game.

Repetitions run as outer loop (all scenarios, then all scenarios again), so drift from temperature
or background activity spreads over the scenarios instead of biasing one of them.

## What is measured

Per sample interval (`-SampleIntervalMilliseconds`, default 1 s):

- **CPU time** (kernel + user) of CapFrameX.exe and of the `CX-PresentMon*` process, as % of one
  logical processor: 100 % is one busy core. "% of system" divides by the logical processors, as
  Task Manager does. Windows charges CPU time per timer tick, so small values are noisy.
- **CPU cycles** (`QueryProcessCycleTime`): exact and independent of the clock; the better measure
  for small differences and before/after comparisons.
- Working set, private bytes and handles of CapFrameX; CPU of the target and of RTSS; the busy
  share of the whole system.
- **Threads of CapFrameX**: CPU per thread between the start and end of the measurement, grouped
  by thread name (named .NET threads such as `CapFrameX hardware sensor polling` or `.NET TP Worker`),
  else by the module of the thread's start address (e.g. `[cfx_osd_core.dll]`). This points at the
  component that costs the CPU.

Not attributed to CapFrameX: the in-game overlay draws inside the game (see the target column), RTSS
draws in its own processes (RTSS column), and the ETW overhead of PresentMon's trace lands in the
traced processes and the kernel.

## Output

`temp\cpu-load\<timestamp>\`:

- `report.md`: environment, scenario table, contributions, thread breakdown, warnings.
- `report.json`: the same data plus every run; input for `-BaselineReport`.
- `samples.csv`: one row per sample interval and run, invariant number format.
- `runs\<nn>-<scenario>-r<rep>\`: the settings the run used and the CapFrameX log (`Logs`).
  `-KeepRunData` also keeps its configuration.
- `prime\`: the primed configuration and the two overlay profiles.

## Before/after comparisons

```powershell
.\scripts\Measure-CpuLoad.ps1 -Repetitions 3                                   # before the change
# rebuild
.\scripts\Measure-CpuLoad.ps1 -Repetitions 3 -BaselineReport temp\cpu-load\<before>\report.json
```

The report then shows the change per scenario. For reliable results: the same power plan,
mains power on laptops, no other load, the same display refresh rate (vkcube runs with VSync,
`--present_mode 2`), and several repetitions; compare cycles rather than CPU % for small effects.

## Options

| Parameter | Default | |
| --- | --- | --- |
| `-AppDirectory` | `source\CapFrameX\bin\x64\Release\net10.0-windows` | any CapFrameX folder, e.g. an installation |
| `-Scenario` | `*` | wildcards on scenario names |
| `-Renderer` | `InGame, HookFree, Rtss` | renderers to measure |
| `-OverlayDesign` | | tile design name (wildcards) or profile ID for the `-Design` scenarios |
| `-Repetitions` | 1 | runs per scenario |
| `-WarmupSeconds` / `-DurationSeconds` | 30 / 60 | per run |
| `-SampleIntervalMilliseconds` | 1000 | |
| `-CooldownSeconds` | 5 | pause between runs |
| `-TargetPath` / `-TargetArguments` | vkcube, `--present_mode 2 --width 640 --height 480` | the game; `--present_mode 0` renders uncapped and stresses PresentMon reading |
| `-TargetProcessName` | target file name | without `-TargetPath`: attach to a running game instead of starting one; it keeps running |
| `-ConfigurationSource` | | a configuration folder to start from, e.g. `%APPDATA%\CapFrameX\Configuration` (only read) |
| `-Settings` | | AppSettings.json values for every run; the keys that define the scenarios are ignored |
| `-ShowWindow` | off | keep the main window open instead of the tray |
| `-Port` | 1337 | web service port |
| `-BaselineReport` | | `report.json` of an earlier measurement |
| `-OutputDirectory` | `temp\cpu-load` | |
| `-KeepRunData` | off | keep each run's configuration |

## Troubleshooting

- "CapFrameX did not list 'vkcube'": PresentMon sees no presents. Check the run's `CapFrameX.log`,
  an ETW session conflict (FrameView SDK) or a vkcube that is minimized or occluded.
- "CapFrameX also lists ...": another process presents frames; close it, the overlay may follow it.
- InGame scenarios without Vulkan layer: register the layer (installer, or `register_layer.cmd` of
  the OSD repo) or measure with a DirectX game (`-TargetPath`).
- A run fails or hangs at startup: see `runs\<run>\Logs\CapFrameX.log`.

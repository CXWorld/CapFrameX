# CapFrameX frame generation trace

Records an ETW trace of a game with frame generation, for GPU vendors and the PresentMon team. It
was written for the report to AMD about FSR Frame Generation with Driver-Assisted Frame Pacing
(Adrenalin 26.10.1 and later): PresentMon shows the two frames of each pair almost at the same time,
while a high-speed camera shows even pacing.

## How to run

1. Close CapFrameX, Intel PresentMon, RTSS/Afterburner and other overlays.
2. Start the game in fullscreen with frame generation on and find a steady scene.
3. Double-click **`Run-FrameGenTrace.cmd`** and allow the administrator prompt.
4. Press Enter, switch to the game within 10 seconds and leave it in the foreground. A beep marks
   the start of the 20 second recording, two beeps its end.
5. Saving the trace takes a while. The zip `CapFrameX-FrameGenTrace_<date>.zip` lands on the desktop.

For a comparison, record the same scene twice and tag the runs, for example with VSync off (the
driver paces the frames) and VSync on (the swapchain paces them itself):

```
Run-FrameGenTrace.cmd -Label vsync-off
Run-FrameGenTrace.cmd -Label vsync-on
```

Other options: `-Seconds 30`, `-StartDelaySeconds 15`, `-ExtraProviders <guid>,<guid>` for further
ETW providers a vendor asks for (recorded with all keywords at verbose level), and `-EtlFile <path>`
to replay and summarize an existing trace without recording.

## What it records

`FrameGenTrace.wprp` (written into the result folder) is a Windows Performance Recorder profile:

- every provider PresentMon reads, so `PresentMon --etl_file FrameGenTrace.etl` replays the trace:
  DxgKrnl presents and flips, DXGI, D3D9, DWM, Win32k, process start/stop, the Intel-PresentMon
  provider (frame types of XeSS-FG and FSR Frame Generation 4.0.0 on DX12, app timing), and NVIDIA's
  flip metering and PC latency markers;
- DxgKrnl GPU scheduling, the hardware scheduling log (HAGS) and rundown, for a GPU timeline in WPA
  or GPUView;
- kernel thread scheduling (context switches, ready threads, DPCs, interrupts) without stacks.

The trace contains the names and paths of the processes that ran during the recording, but no
screen content, files or keystrokes. Expect a few hundred MB for 20 seconds before compression.

## What the summary says

The script replays the trace with the PresentMon console from the CapFrameX installation and
summarizes the busiest swapchain in `FrameGenTrace-summary.txt`: present modes, frame types, the
display rate, `MsBetweenDisplayChange`, `MsBetweenPresents` and `MsUntilDisplayed`. The line
"displayed within 1 ms of the previous displayed frame" counts frames PresentMon sees reaching the
screen at (almost) the same time as the one before. With frame generation that should be close to
none; a share near half of the frames is the pattern of the reported issue.

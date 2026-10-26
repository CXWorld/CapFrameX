# CapFrameX MPO check

Finds out whether your PC supports multiplane overlays (MPO) and whether a game keeps its hardware
plane while an overlay window is shown above it. That decides whether the CapFrameX hook-free overlay
adds latency on your PC.

## How to run

1. Close CapFrameX and any always-on-top tools (screen recorders, monitoring windows).
2. Extract the folder anywhere and double-click **`Run-MpoCheck.cmd`**.
3. Allow the administrator prompt (PresentMon needs it to record frames).
4. Do not touch the mouse or keyboard for about 50 seconds. The screen shows a colored fullscreen
   test image, and a small green panel appears in the top-left corner from time to time.
5. Send the zip file `CapFrameX-MPO-Check_<date>.zip` from your desktop to the CapFrameX team.

If Windows warns about the downloaded files, right-click the zip, choose **Properties**, tick
**Unblock**, and extract it again.

## Requirements

- Windows 10 or 11.
- PresentMon: the check uses the copy installed with CapFrameX. Without CapFrameX, download the
  PresentMon console application (`PresentMon-<version>-x64.exe`) from
  https://github.com/GameTechDev/PresentMon/releases and put it next to `MpoCheck.ps1`.
  Without PresentMon the check only reports what the display driver supports.

## What it does

1. Asks the display driver how many MPO planes each display has.
2. Renders a fullscreen test scene with Direct3D 11 at half the refresh rate and records its frames
   with PresentMon: once alone, then twice with an overlay window above it for 15 seconds, updating
   10 times per second (the default of the hook-free overlay) and 60 times per second. The overlay
   window is built like the CapFrameX hook-free overlay.
3. Shows the result and writes `MpoCheck-report.txt` plus the PresentMon captures into the zip.

The report contains your Windows version, GPU and driver versions, monitor models, display modes and
the recorded frame data of the test scene. It contains no user names, files or other programs.

## Reading the result

| PresentMon mode                       | Meaning                                                                  |
|---------------------------------------|--------------------------------------------------------------------------|
| `Hardware: Independent Flip`          | The game owns the screen; nothing is composed on top.                    |
| `Hardware Composed: Independent Flip` | The game is on its own MPO plane; Windows composes the rest beside it.   |
| `Composed: Flip`                      | Windows (DWM) copies the game into the desktop image: one refresh later. |

- `[ok]` with the overlay: the game keeps independent flip, at most with a short frame gap when the
  overlay appears.
- `[~~]` with the overlay: Windows composed the game for a few seconds after the overlay appeared,
  then gave it its plane back. Expect that whenever the hook-free overlay appears.
- `[!!]` with the overlay: Windows still composes every game frame at the end of the capture, which
  costs latency as long as the overlay is visible. The In-Game renderer of CapFrameX avoids that.

How long Windows composes after an overlay appears varies from run to run (on one test PC between
none and about eight seconds), so a second run may differ.

Windowed games are not tested: whether Windows gives a window its own plane depends on everything
else on the desktop, so that result does not repeat.

Options for `MpoCheck.ps1`: `-PresentMonPath <exe>`, `-OutputDirectory <folder>`,
`-CaptureSeconds <3..60>`, `-OverlayRates <Hz,Hz,...>` (default `10,60`), `-NoPause`.

<p align="center">
  <img src="https://raw.githubusercontent.com/CXWorld/CapFrameX/v1.9.2/images/CX_Header_Logo_Wide.jpg" alt="CapFrameX" width="100%">
</p>

<h3 align="center">Tile overlay and a lighter overlay renderer</h3>

<p align="center">
  CapFrameX 1.9.2 adds the <b>tile overlay</b> with its own editor and makes the <b>CapFrameX overlay renderers</b> cheaper for the game.<br>
  It also ships the PresentMon capture fixes of the 1.9.2 betas, an automatic capture target and four more UI languages.
</p>

<table align="center">
  <tr>
    <td align="center" width="170"><h2>45</h2><sub>STARTER DESIGNS</sub></td>
    <td align="center" width="170"><h2>10</h2><sub>TILE KINDS</sub></td>
    <td align="center" width="170"><h2>4</h2><sub>NEW LANGUAGES</sub></td>
    <td align="center" width="170"><h2>350+</h2><sub>COMMITS SINCE 1.9.1</sub></td>
  </tr>
</table>

## Download

| Package | Use |
| --- | --- |
| **[Installer](https://github.com/CXWorld/CapFrameX/releases/download/v1.9.2/release_1.9.2_installer.zip)** | Extract the ZIP and run `CapFrameXBootstrapper.exe`. |
| **[Portable](https://github.com/CXWorld/CapFrameX/releases/download/v1.9.2/release_1.9.2_portable.zip)** | Extract into a **new** folder and run `CapFrameX.exe`. Copy your old `Portable` folder and `portable.json` over. |
| [SHA-256 checksums](https://github.com/CXWorld/CapFrameX/releases/download/v1.9.2/SHA256SUMS.txt) | Verify both ZIPs. |

Requires the **[.NET 10 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/10.0)**. Application version **1.9.2.4**, every binary Authenticode-signed. Close CapFrameX and your games before updating. Setup now installs into `C:\Program Files\CapFrameX`; an upgrade moves an existing installation out of `Program Files (x86)`.

## Highlights

<table>
  <tr>
    <td width="33%" valign="top" align="center"><h3>Tile overlay</h3><sub>Arrange readings, bars, gauges, charts and images in the new tile editor, or start from one of 45 designs.</sub></td>
    <td width="33%" valign="top" align="center"><h3>Lighter renderers</h3><sub>Hiding, showing and updating the CapFrameX overlay takes less from the game, in-game and hook-free.</sub></td>
    <td width="33%" valign="top" align="center"><h3>Dependable captures</h3><sub>PresentMon fixes for long sessions and frame generation, and a capture target that is picked for you.</sub></td>
  </tr>
</table>

<p align="center"><sub>Expand a section for the details.</sub></p>

<details open>
<summary><b>Tile overlay</b> &nbsp;·&nbsp; <sub>tile editor, 45 starter designs, charts, heatmaps, sharing</sub></summary>

<br>

Open it under **Overlay → Tile overlay**. The in-game and hook-free renderers draw tile designs; RTSS keeps the row overlay.

- **Tile editor** with live preview, undo and a library of saved profiles: tiles on a grid or a free canvas, colors per tile, one font and tile opacity per design.
- **Ten tile kinds:** reading, bar, ring gauge, live chart, text, icon, image, sensor group (bars or a heatmap, for example every core load by core type), container and the row overlay as a block. The palette adds CapFrameX logo tiles and Ryzen 5, 7 and 9 badges.
- **Charts:** frame times, display times and frame rates per frame or counted per second, up to four lines on one axis, an adaptive stutter threshold and grid lines.
- **45 starter designs:** everyday layouts, vertical strips, hybrid-CPU layouts and hardware themes (APEX, REDLINE, FUSION, GREENLINE, ICELINE, DUOLINE, BLUELINE), BLACKWELL THERMALS with every GDDR7 module temperature of an RTX 50 card, anime artwork, Slick Overlay (adapted from HeyOkay's RTSS overlay), OLED Minimal and the full-width Performance Strip. Hardware designs name the CPU and GPU that are installed.
- **Layout:** span the full screen width at the top or bottom, scale with the game's resolution, OLED pixel orbit, and a font of your choice for the row overlay.
- **Share:** export a design and import it on another PC. CapFrameX maps its sensors onto your hardware and lists what it could not match.
- **AI authoring:** MCP clients can create, edit, render and activate tile designs.

</details>

<details open>
<summary><b>Renderer performance</b> &nbsp;·&nbsp; <sub>hide and show, partial repaints, frame generation, fewer polls</sub></summary>

<br>

- **Hide and show keep the renderer.** The in-game hook and the Vulkan layer no longer release and rebuild their resources. Before, every capture with auto-hide began with a frame about 5 ms late and the overlay came back after a 45 ms rebuild (Mafia: The Old Country, RX 9070 XT).
- **Hook-free repaints only what changed.** Tile values update once per OSD refresh, and only the tiles that draw something new are repainted and presented. A tile design repainted the whole window 33 to 59 times in 10 s before; each repaint cost the game about 1.9 ms of GPU time (Doom: The Dark Ages, RTX 5090).
- **Hook-free hides by present mode.** Over a game on its own display plane the window is emptied instead of removed. Removing it cost 1 to 2 s of displayed frames at each capture start.
- **Frame generation:** graphs draw both frames of a pair instead of a flat line with false spikes, continue on current data after a show, and a capture no longer starts with a frame from before the start.
- **Less background work:** a tile design polls only the sensors and computes only the metrics it shows, DIMM and SATA reads are throttled, RTSS receives only the entries it can draw.

</details>

<details>
<summary><b>Capture and PresentMon</b> &nbsp;·&nbsp; <sub>CX-PresentMon 2.6.1, automatic target, PMD timing, hotkeys</sub></summary>

<br>

- **CX-PresentMon 2.6.1**, our PresentMon build (not an Intel release), fixes: tracking data that grew CPU and memory use over long sessions ([#695](https://github.com/GameTechDev/PresentMon/issues/695)), capture output that stalled for up to a minute after frame generation was switched, missing display times and the missing animation error with FSR frame generation, the `AMD FSR FG` frame type, and a DWM flip delay that moved a game frame's screen time on NVIDIA.
- **Correction:** disabling **PC Latency** never worked around the tracking problem, contrary to our [earlier post on X](https://x.com/CapFrameX/status/2105998858019078230).
- **Automatic capture target:** with several processes listed, CapFrameX follows the game (foreground window, GPU load, known game folders) until you select one. Exited processes no longer linger in the list.
- **PMD:** power samples are placed by time, not by count. Before, the power curve of a 600 s capture could end 12 s early.
- A delayed capture starts when its delay is over, PresentMon cannot outlive CapFrameX, and the log records every hotkey press the keyboard hook misses.

</details>

<details>
<summary><b>Overlay renderers and compatibility</b> &nbsp;·&nbsp; <sub>Off renderer, hook-free stand-in, MPO warning, new entries</sub></summary>

<br>

- New **Off** renderer for capture-only setups. Only the selected renderer draws.
- The **hook-free overlay stands in** for games the in-game renderer cannot draw in: Direct3D 9, Easy Anti-Cheat, BattlEye, VAC, Ricochet and blacklisted titles.
- **MPO warning** in the Overlay tab when the hook-free display has no free plane, and an [MPO check](https://github.com/CXWorld/CapFrameX/tree/v1.9.2/scripts/MpoCheck) to test it.
- New entries and formats: **Present Mode**, the graphics API in the `<APP>` group, percentage values as text, bar or both, storage device names.
- **In-game compatibility:** editable profile files with per-user overrides, and more robust learning (Direct3D 11 games that load `d3d12.dll`, games running before CapFrameX, loading screens, target switches).
- The **portable** version offers to register its Vulkan layers at startup.

</details>

<details>
<summary><b>Analysis, records and sensors</b> &nbsp;·&nbsp; <sub>faster record list, background cut, new readings</sub></summary>

<br>

- The record list loads from an on-disk index and in batches; dropped records load off the UI thread.
- Cutting a record runs in the background and keeps its sensor data.
- Faster moving averages and frametime distribution, a readable axis around long stalls, and comparison charts that redraw correctly after range and theme changes.
- **Sensors:** RAM manufacturer from SPD data, AMD CCD topology and clocks per core type, AMD GPU memory total, the temperature spread across the memory modules of NVIDIA cards, Intel IGCL v306.

</details>

<details>
<summary><b>App, installer and updates</b> &nbsp;·&nbsp; <sub>languages, WiX 6, signed updates, tools</sub></summary>

<br>

- **Languages:** German, Spanish, Russian and Korean beside English, switched live.
- **Installer:** WiX 6, 64-bit install folder, autostart kept on upgrades, and from this release on a setup can replace a newer version (in-app rollback).
- **Updates:** the publisher signature of every package is verified, then CapFrameX restarts into setup and launches again.
- **Reliability:** a message instead of a crash when the settings cannot be saved, and a start from an archive's temporary folder is explained.
- **Tools:** a [frame generation trace](https://github.com/CXWorld/CapFrameX/tree/v1.9.2/scripts/FrameGenTrace) for GPU vendors and a [capture helper for Windows 7](https://github.com/CXWorld/CapFrameX/tree/v1.9.2/tools/CapFrameX.CaptureHelper).
- Bundles BENCHLAB Service 2.4.0, unchanged.

</details>

## Good to know

- Tile designs need a CapFrameX renderer. RTSS shows the row overlay.
- Hardware designs show what your system reports. A tile without a matching sensor stays empty.
- The in-game overlay learns the working route per game. Some routes only take effect at the next game start.

**Full changelog:** [`v1.9.1...v1.9.2`](https://github.com/CXWorld/CapFrameX/compare/v1.9.1...v1.9.2)

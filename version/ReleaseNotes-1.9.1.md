CapFrameX 1.9.1 restores the experimental in-game overlay with signed binaries, brings back the bundled BENCHLAB service, and improves overlay compatibility, hardware information, and capture workflows. This is a stable release; the in-game renderer remains experimental.

## Downloads and requirements

| Package | Installation |
| --- | --- |
| `release_1.9.1_installer.zip` | Extract and run `CapFrameXBootstrapper.exe`. Setup installs CapFrameX and registers the x64 and x86 Vulkan layers in their matching HKLM registry views. |
| `release_1.9.1_portable.zip` | Extract the complete archive and run `CapFrameX.exe`. Keep the included `portable.json` for an isolated portable configuration. Vulkan integration requires separate layer registration. |

Install the [.NET 10 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/10.0) first. Another .NET major version, the plain .NET Runtime, or the legacy .NET Framework does not replace this requirement. Portable installations also require the Visual C++ x64 runtime; see [Portable Mode](https://github.com/CXWorld/CapFrameX/blob/v1.9.1/PORTABLE_MODE.md).

The internal application version is **1.9.1.4**, with the release channel. CapFrameX binaries, native overlay components, BENCHLAB, and setup carry the CapFrameX publisher's Certum code-signing certificate with SHA-256 timestamps. Original vendor signatures are preserved. Package hashes are supplied in `SHA256SUMS.txt`.

Close CapFrameX and games before updating. Portable users should extract into a new folder, then copy their previous `Portable` data folder and customized `portable.json`.

## Overlay and OSD

- Restored **CapFrameX in-game (Experimental)** for DirectX and Vulkan titles, including signed x64 and x86 hook and layer DLLs. Hook-free and RTSS renderers remain available.
- Improved compatibility probing, per-game learned profiles, and recovery after frame-generation changes, including FSR and XeSS routing.
- Smoother in-game frametime charts, sharper antialiased lines, and corrected frame cadence under frame generation.
- Overlay toggles in the Overlay tab and status bar, per-entry text sizes, and an OSD log-folder shortcut.
- Fixed template apply/revert, profile switching, and the save button. Improved hook-free placement and averaged metrics at each refresh.
- Optional compatibility reports require explicit user consent. Extended diagnostics remain opt-in.

## Sensors and system information

- Bundled **BENCHLAB Service 2.4.0**, rebuilt as a signed, self-contained .NET 10 executable. Compatible separately installed services remain supported; fixed null sensor readings.
- Updated **Intel IGCL v298** and **AMD ADLX 2.0**, with expanded Intel telemetry, fan and PSU readings, and AMD SmartShift monitoring.
- Added software component versions, platform security status, and **Copy system info** to the Info dashboard.
- Corrected Intel memory gear decoding and improved overlay entries across CPU changes.
- Removed the click-to-photon latency feature.

## Capture, interface, and reliability

- Updated to **PresentMon 2.6.0** and added stored display-layer metadata to captures.
- Restored per-game capture durations, added persistent hotkey disabling, and preserved additional analysis graph selections.
- Fixed dragging multiple records into analysis views, modern .NET autostart registration, and update confirmation/relaunch behavior.
- Improved layouts and clipping across tabs, including maximized 1080p windows. The minimum/default main-window size is now 1488 × 768.
- Removed unused AvalonDock files and empty localization folders from packages.

Full changelog: [`v1.9.0...v1.9.1`](https://github.com/CXWorld/CapFrameX/compare/v1.9.0...v1.9.1)

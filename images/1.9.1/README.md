# Release screenshots

Real window captures of the signed CapFrameX **1.9.1.4**, release channel, taken on
2026-09-27 from a separate portable copy of the release payload:

- `info.png`: Info dashboard.
- `overlay.png`: Overlay items.
- `overlay-options.png`: OSD options with the experimental in-game renderer selected.

Each image is **1920 × 1080**, matching the previous master screenshots. The capture
tool returned 1920 × 1081; only the bottommost window-border pixel was cropped when
converting to PNG. The application content was neither retouched nor reconstructed.

The record list uses repository test fixtures (MetroExodus and re2.exe). System
information is from the release workstation. Portable extraction does not register
Vulkan layers, so its registration status is independent of the installer's layer
registration. All renderer choices and diagnostics controls are visible together
in the OSD-options image.

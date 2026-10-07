# CapFrameX 1.9.2.1 Beta

This build updates the 1.9.2 beta through the CapFrameX update server. The GitHub release and its download packages remain at 1.9.2.0.

- Prefer SPD module-vendor data for RAM manufacturer identification, with fallbacks for incomplete detection.
- Load editable hook compatibility profiles at startup, with application defaults and optional user overrides. Restart CapFrameX and the affected game after editing profiles.
- Display percentage overlay values as text, a bar, or text followed by a bar in RTSS and CapFrameX overlays.
- Show storage device names in overlay descriptions.
- Verify downloaded update packages against the CapFrameX signing policy and validate installer arguments before installation.
- Restart into setup after a requested update download, waiting for captures and file saving to finish, and launch CapFrameX again when installation finishes.

The build retains CX-PresentMon 2.6.1 and BENCHLAB Service 2.4.0. The [1.9.2 beta notes](ReleaseNotes-1.9.2-beta.md) describe the runtime requirements and capture fixes.

**For reviewers:** Avoid the hook-free overlay for benchmark measurements. Hiding it can cause frametime spikes, and some AMD/Intel drivers can have DWM issues. Check the active renderer, including automatic fallback. Disabling PC Latency does not resolve the PresentMon tracking issue; the bundled CX-PresentMon fixes address it.

# CapFrameX 2.0 Linux Service - Development Plan

Last updated: 2026-09-20

> **Requirements:** a Linux service implemented separately from the Windows service, exposing the
> same API contract to the one Angular frontend. **Presents come from the Vulkan layer.** The
> telemetry source is validated in `CapFrameX_2.0_Linux_Telemetry_Validation_Plan.md` before it is
> built. **The Avalonia GUI is removed.**
>
> Structure, platform ports and the shared core: `CapFrameX_2.0_Service_Architecture_DevPlan.md`.
> This plan covers the top-level folder `CapFrameX.Service.Linux/`.
>
> **Shared core:** API, contracts, capture state machine, records (incl. the importer for the old
> CSV captures), analysis, database and the demand registry live in `CapFrameX.Service.Shared/` and
> are referenced, never copied. This folder holds only the Linux host (composition root), the Linux
> implementations of the platform ports, the native parts, tools and packaging.
> `CapFrameX.Service.Linux.sln` includes the shared projects; nothing here references
> `CapFrameX.Service.Windows`.
>
> **Layer (requirement 2026-09-20):** the Linux Vulkan layer is the **`CapFrameX.OSD` Vulkan layer**,
> built for Linux, with the capture code of the old `capframex-linux` layer ported into it - one
> layer for presents *and* overlay. See `CapFrameX_2.0_OSD_CrossPlatform_DevPlan.md` (sections 4-5).
> Where this plan says "the layer", phases L1-L2 mean the old C layer (bring-up), L3 onwards the
> OSD layer.

## 1. Starting point - `capframex-linux/` (verified 2026-09-20)

| Part | Size / tech | State |
|---|---|---|
| Vulkan layer `VK_LAYER_capframex_capture` | C, ~1,600 lines (`layer.c`, `swapchain.c`, `timing.c`, `ipc_client.c`, `data_export.c`) | implicit `GLOBAL` layer, manifest with absolute `library_path` `/usr/lib/libcapframex_layer.so`, disable switch `DISABLE_CAPFRAMEX_LAYER=1`; hooks swap-chain creation and `vkQueuePresentKHR`; supports `VK_EXT_present_timing` |
| Daemon `capframex-daemon` | C, ~2,650 lines (`main.c`, `ipc.c`, `process_monitor.c`, `launcher_detect.c`, `ignore_list.c`, `config.c`) | process monitor + launcher detection, ignore list, IPC server, **routes** frame data from layers to the subscribed app |
| IPC | binary, `MessageHeader { type, payload_size, timestamp }`, 19 message types, packed `FrameDataPoint` | Unix socket `capframex.sock` under `~/.config/capframex/` - deliberately **not** `/tmp` or `$XDG_RUNTIME_DIR`: Proton's container (pressure-vessel) shares `/home` but not those |
| Avalonia GUI (`src/app`, .NET 8) | `CapFrameX.App` (5 views, 5 view models), `CapFrameX.Core` (analysis, capture client, data, hardware, hotkey), `CapFrameX.Shared` (IPC messages, models) | **to be removed** |
| Records | CSV (`MsBetweenPresents,MsUntilRenderComplete,MsUntilDisplayed,MsActualPresent`) + JSON sidecar | different from the Windows JSON record |
| Hotkeys | SharpHook (libuiohook) | X11 only in practice; under Wayland it sees keys only while an XWayland window has focus |
| Telemetry | `SysfsReader`: `/sys/class/hwmon`, `/sys/class/drm`, cpufreq, `/proc/stat`, `/proc/meminfo`, DMI, RAPL `intel-rapl:0/energy_uj` | prototype level; RAPL path is root-only on current kernels, so CPU power silently fails for normal users |
| Plans | `OVERLAY_DEV_PLAN.md` (ImGui overlay in the layer), `Display_Timing_Tracker_DEV_PLAN.md` (DRM vblank/page-flip events) | not implemented |

## 2. Removing the Avalonia GUI

Remove it at the start of the Linux work, not at the end - the decision is made, and a frozen second
UI only attracts fixes that are then thrown away. Before deleting, salvage what is knowledge rather
than UI:

| Keep (port into) | From |
|---|---|
| IPC message definitions and framing -> `Linux.Capture` | `CapFrameX.Shared/IPC/Messages.cs`, `Core/Capture/DaemonClient.cs`, `FrametimeReceiver.cs` |
| sysfs/hwmon/DRM discovery logic, Vulkan GPU enumeration -> input for the telemetry validation, later `Linux.Telemetry` / `Linux.Platform` | `Core/Hardware/*` |
| CSV + sidecar reader -> read-only importer in `CapFrameX.Service.Shared/src/CapFrameX.Service.Records` (architecture plan 3.3) | `Core/Data/Session.cs`, `SessionManager.cs` |
| settings keys and defaults -> mapping onto the shared settings model | `Core/Configuration/AppSettings.cs` |
| game/launcher heuristics (stay in C for now, see section 3) | `daemon/launcher_detect.c`, `process_monitor.c`, `ignore_list.c` |

Discard: all views/view models, `Core/Analysis/StatisticsCalculator.cs` and `FrametimeAnalyzer.cs`
(replaced by `CapFrameX.Statistics.NetStandard` via `Service.Analysis`; run both once over the same
capture and note the differences in the dev log so users' old numbers can be explained),
`Core/Hotkey` (SharpHook - replaced, section 6), `FrameReceptionTest` (its purpose moves into the
`Linux.Capture` tests).

Also: `capframex.desktop` targets the new host; `scripts/build.sh`, `README.md`, `CLAUDE.md` are
rewritten; the `.sln` under `src/app` disappears. After the salvage the old C layer and the daemon
move to `CapFrameX.Service.Linux/native/legacy/` (needed until L3), the two plan documents move to
`dev-plans/archive/`, and the `capframex-linux/` folder disappears.

## 3. Capture: presents from the Vulkan layer

### 3.1 Target topology
```
game process                                   user session
+---------------------------+                  +-------------------------------------------+
| CapFrameX.OSD vk layer    |  Unix socket     | CapFrameX.Service.Linux                   |
|  capture: present hook,   |  + memfd rings   |  Linux.Capture  : layer server            |
|   VK_EXT_present_timing   |----------------->|  (IFrameSource)   process registry        |
|  overlay: compositor      |<-----------------|  Linux.Overlay  : profile + metrics feed  |
+---------------------------+  config/commands |  shared core: orchestrator, records, API  |
                                               +-------------------------------------------+
```
The **daemon is absorbed by the service**. Its three jobs - being the socket server, tracking
processes, keeping the ignore list - are exactly what `IFrameSource` has to do anyway, and a
separate router process between layer and service adds a hop, a second protocol endpoint and a
second lifecycle to manage for no functional gain. Frame timestamps are taken inside the layer, so
the receiver's scheduling (including .NET GC pauses) cannot distort the data; it only has to keep
up, which at a few thousand small messages per second is not a concern.

Staging, to get data on screen early:
- **L1:** `Linux.Capture` is a *client* of the existing daemon (port of `DaemonClient`). Fastest path
  to real frames in the new UI; nothing native changes.
- **L3:** the OSD layer with its capture module replaces the old C layer (OSD plan X4), and
  `Linux.Capture` becomes the *server*: it owns the socket, speaks the new transport (control
  messages over the socket, frame ring over memfd - OSD plan 4.2) and takes over
  process registry and ignore list (ported from C; launcher detection via `/proc/<pid>/` parent
  chain, cgroup and environment as the daemon does today). The daemon is deleted.
  The layer needs one behavioural change: reconnect with backoff when the service is not running,
  and stay completely passive (no timing work beyond the present hook's counter) while no service is
  connected or no capture/overlay demand exists for its process (section 7).

### 3.2 Protocol requirements (for the L3 transport)
The old protocol was written for a trusted GUI on the same machine and is retired with the daemon;
its replacement (OSD plan 4.2) must meet these from the start:
- versioned hello (`protocol_version`, layer build id); reject unknown versions with a logged reason;
- strict length validation against `payload_size`, bounded message size, per-connection rate limit;
- `SO_PEERCRED` check: accept only connections from the same uid;
- socket directory `0700`, socket `0600`; keep the location under `$HOME` for Proton, but move it
  to `~/.local/state/capframex/` or keep `~/.config/capframex/` - decide once, the layer and the
  service must agree, and Flatpak Steam needs the path exposed (section 8);
- batch frames (N frames or T ms per message) to cut syscalls at very high frame rates;
- the C structs in `common.h` become the single definition; the C# side is generated from or
  tested against them (layout test with `sizeof`/offset constants emitted by a small C program in
  the build), so the two sides cannot drift silently.

### 3.3 Frame metrics - closing the gap to PresentMon
Today's `FrameDataPoint`: frame number, `timestamp_ns`, CPU-side frametime, and - only with
`VK_EXT_present_timing` - actual present time, ms until render complete, ms until displayed. The gap
table is in the architecture plan (3.1). Work, in priority order:

| # | Metric | Approach | Note |
|---|---|---|---|
| 1 | display-side frametime without `VK_EXT_present_timing` | evaluate what is available per driver: the extension (coverage must be measured - it is recent), `VK_GOOGLE_display_timing` where still exposed, otherwise none | the DRM vblank approach of `Display_Timing_Tracker_DEV_PLAN.md` measures the *display*, not the *game's* flips, and cannot attribute flips to a swap chain under a compositor - keep it as a research item, not a plan item |
| 2 | GPU time / GPU busy per frame | Vulkan timestamp queries injected by the layer around the present-side submission are intrusive; preferred: per-process GPU engine time from DRM **fdinfo** sampled by the service and aligned to frames (amdgpu, i915/xe), NVML process utilisation on NVIDIA | validated in the telemetry plan (T6) |
| 3 | present mode, swap-chain format/extent, HDR | from `VkSwapchainCreateInfoKHR` - already partly sent (`SwapchainInfoPayload`); add present mode and colour space | cheap |
| 4 | animation error | computed in the shared core once display times exist | no layer work |
| 5 | frame generation | examine how FSR-FG/DLSS-FG under Proton present; mark generated frames if distinguishable | research |
| 6 | PC latency | no equivalent source on Linux today | capability stays `unavailable` |

OpenGL-native titles are out of scope for 2.0 (Proton/DXVK/VKD3D and native Vulkan cover the
audience); note it in the capability reason.

## 4. Telemetry

Depends on the outcome of `CapFrameX_2.0_Linux_Telemetry_Validation_Plan.md`. Short version of its
answer to "is the Linux kernel suitable?": **yes, as the primary source** (hwmon, DRM/amdgpu/xe/
i915 sysfs, cpufreq, `/proc`, powercap, DRM fdinfo) - with three structural gaps that the design
must cover: the NVIDIA proprietary driver exposes almost nothing through the kernel (NVML is
mandatory), CPU package power (`powercap` RAPL) is root-only, and mainboard sensors depend on
Super-I/O drivers with unlabelled channels.

`Linux.Telemetry` therefore is planned as a set of small readers behind `ITelemetrySource`, each
reporting availability + reason: `HwmonReader`, `CpuReader` (`/proc/stat`, cpufreq, topology),
`MemoryReader`, `AmdGpuReader` (sysfs + `gpu_metrics`), `IntelGpuReader` (xe/i915 sysfs + hwmon +
fdinfo), `NvmlReader` (P/Invoke `libnvidia-ml.so.1`), `DrmFdinfoReader` (per-process GPU time and
VRAM), `RaplReader` (through the optional helper, section 5). Implementation starts only after the
validation report is accepted (L2).

## 5. Process model and privileges

- `CapFrameX.Service.Linux` runs as the **logged-in user**, no root. Started by the desktop host on
  demand or by a `systemd --user` unit (`capframex.service`, `WantedBy=default.target`) when
  autostart is enabled; XDG autostart as fallback for non-systemd sessions.
- Single instance: lock file in `$XDG_RUNTIME_DIR/capframex/`.
- Token hand-over: `$XDG_RUNTIME_DIR/capframex/service.token`, mode `0600`, per start.
- Tray: StatusNotifierItem over D-Bus, owned by the service (architecture plan section 3);
  optional - no feature depends on it.
- **Optional privileged helper** `capframex-telemetry-helper` (decided by the validation plan, T5):
  tiny root process, socket-activated systemd *system* unit, reads an **allowlist of files**
  (`/sys/class/powercap/*/energy_uj`, selected MSRs if validated as necessary) and returns numbers
  over a Unix socket restricted to the requesting uid via `SO_PEERCRED` + polkit rule. No generic
  file or MSR read. It is the structural counterpart of PawnIO on Windows: the one narrow,
  audited, privileged component. Without it the service runs fine and reports `cpu.power` as
  unavailable with the reason.
- Paths: `$XDG_CONFIG_HOME/capframex` (settings, overlay profiles), `$XDG_DATA_HOME/capframex`
  (database, captures by default), `$XDG_STATE_HOME/capframex` (logs).

## 6. Hotkeys

Global hotkeys are the weak spot of Linux desktops and need an honest capability model:
- **X11 session:** `XGrabKey` through a small native shim or a managed X11 binding - reliable.
- **Wayland session:** the `org.freedesktop.portal.GlobalShortcuts` portal (KDE, GNOME 48+, others
  as they adopt it); the user confirms the binding in a system dialog. Where the portal is missing:
  no global hotkeys - the capability says so, and the UI offers the alternatives: bind a compositor
  shortcut to the CLI (`capframex-ctl capture toggle`, a thin client of the API), or use the
  in-layer hotkey below.
- **In-layer hotkey (later):** the layer can observe key state in the game's own window system
  connection; MangoHud does this. Works on every session type while the game has focus, which is
  exactly when it is needed. Planned with the overlay work, since it shares the input path.
- No evdev/`/dev/input` reading - it needs the `input` group or root and is a keylogger by design.

## 7. On-demand operation

The service follows the on-demand policy of the architecture plan (section 8): no socket server
work, no sensor polling, no fdinfo sampling without a consumer. Specific to Linux:
- The layer is loaded into **every** Vulkan process (implicit layer). It must cost nothing when
  idle: no thread, no allocation in the present hook, a single atomic check, until the service asks
  this pid for frames or overlay.
- Telemetry readers are instantiated per demanded metric; NVML is `dlopen`ed on first NVIDIA
  demand; the helper is contacted only when a privileged metric is demanded.

## 8. Packaging

- Native: layer built from the `CapFrameX.OSD` submodule (`linux/vk_layer`) or staged from the
  prebuilt folder, through `CapFrameX.Service.Linux/native/layer`; manifest installed to
  `/usr/share/vulkan/implicit_layer.d/` (package) or `~/.local/share/vulkan/implicit_layer.d/`
  (user install/AppImage helper), with a **relative or install-prefix-correct** `library_path`
  instead of today's hard-coded `/usr/lib/...`; 32-bit layer build for 32-bit Proton titles
  (`lib32`/`i386` multiarch) - same shadowing concern as on Windows: both manifests share the layer
  name, so each must only be reachable by loaders of its bitness (distribution lib dirs handle it).
- Service: self-contained .NET publish (`linux-x64`), installed with the host (UI plan 6.5);
  `.deb` and AppImage first. The AppImage cannot install a system-wide layer - it offers a user-level
  layer install on first start.
- Helper: separate optional package (`capframex-telemetry-helper`) because it installs a root unit
  and a polkit rule.
- **Flatpak Steam**: the game runs in Steam's sandbox; an implicit layer on the host is invisible
  there and the socket path is not shared. Needs a Vulkan-layer Flatpak extension
  (`org.freedesktop.Platform.VulkanLayer.*`, as MangoHud ships) plus a filesystem override for the
  socket directory. Planned after the first release; documented as a known limitation until then.
- Steam Deck / SteamOS (read-only root, gamescope session): user-level install only; gamescope
  affects hotkeys and overlay composition - part of the test matrix, not a separate code path.

## 9. Phases

| Phase | Scope | Acceptance |
|---|---|---|
| **L0** | Salvage (section 2), delete `src/app`, move old layer + daemon to `native/legacy/`, `CapFrameX.Service.Linux` host + `Linux.Platform` ports (paths, trash, lifecycle, system info) | host starts as user on Ubuntu LTS and Fedora; conformance suite green; M1 of the UI plan (records + analysis) works, including import of old CSV captures |
| **L1** | `Linux.Capture` as daemon client; `IFrameSource`; capture through the shared orchestrator; records in the shared JSON format | capture of a native Vulkan title and a Proton title from the Angular UI; record opens on Windows |
| **L2** | Telemetry per accepted validation report; optional helper | sensors + well-known metrics live in UI and in records; unavailable metrics carry reasons |
| **L3** | Protocol hardening, service becomes the layer server, daemon deleted, idle-cost rules for the layer | no daemon process; layer idle cost verified (section 7); reconnect after service restart |
| **L4** | Frame-metric gap work (3.3 #1-#4), hotkeys (section 6), `capframex-ctl` | capability matrix documented per GPU vendor/driver |
| **L5** | `Linux.Overlay`: profile + metrics feed to the OSD layer (OSD plan X3-X6); in-layer hotkey | the five presets of the overlay plan render in a native and a Proton title |
| **L6** | Packaging (section 8), Flatpak extension | install/upgrade/remove on the distro matrix of the UI plan |

L0 and L1 do not depend on the telemetry validation; the validation runs in parallel from day one.

## 10. Risks

| Risk | Mitigation |
|---|---|
| `VK_EXT_present_timing` coverage too thin -> no display-side metrics on most systems | measure coverage in L1 on the test matrix; UI shows CPU-side frametime with a clear label; do not block the release on it |
| Implicit layer in every Vulkan process causes crashes/anti-cheat issues attributed to CapFrameX | idle-cost and passivity rules; ignore list applied *inside* the layer before any hook work; disable env var documented; layer version handshake |
| Proton container / Flatpak cannot reach the socket | socket under `$HOME` (already), Flatpak extension in L6, clear diagnostics in the UI ("layer loaded but cannot reach service") via a layer-written breadcrumb file |
| Hotkeys unusable on some Wayland compositors | portal + CLI + in-layer hotkey; capability reasons |
| NVIDIA users get less telemetry than on Windows | NVML covers load/clock/power/temp/VRAM/throttle reasons; board sensors via hwmon as for everyone |

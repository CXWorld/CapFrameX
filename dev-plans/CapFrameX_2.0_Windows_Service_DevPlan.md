# CapFrameX 2.0 Windows Service - Development Plan

Last updated: 2026-09-20

> **Requirement:** the Windows service **always runs with administrator rights**. It continues to use
> **PresentMon** (ETW) for presents and **PawnIO** for low-level hardware access.
>
> Structure, platform ports and the shared core are defined in
> `CapFrameX_2.0_Service_Architecture_DevPlan.md`. This plan covers the top-level folder
> `CapFrameX.Service.Windows/`.
>
> **Shared core:** API, contracts, capture state machine, records, analysis, database and the demand
> registry live in `CapFrameX.Service.Shared/` and are referenced, never copied. This folder holds
> only the elevated host (composition root) and the Windows implementations of the platform ports.
> `CapFrameX.Service.Windows.sln` includes the shared projects; nothing here references
> `CapFrameX.Service.Linux`.

## 1. What needs the elevation

| Component | Why elevated |
|---|---|
| PresentMon | real-time ETW session (`Microsoft-Windows-DxgKrnl`, DXGI, D3D9, Win32k providers) requires administrator or `Performance Log Users` + provider ACLs; CapFrameX has always used administrator |
| PawnIO | opening the PawnIO device and loading the signed modules (MSR, SMU, Super I/O, IMC, OOBMSM) |
| In-game hook injection | `OpenProcess` with `PROCESS_VM_WRITE \| PROCESS_CREATE_THREAD` on games that themselves run elevated or are started by elevated launchers |
| Vulkan layer / RTSS interplay, target detection of elevated games | process queries across integrity levels |

Consequence: there is no unelevated mode and no degraded start. If the process finds itself without
an elevated token it logs, reports nothing on the API and exits with a distinct exit code the host
turns into a "repair installation" prompt (section 2.3).

## 2. Process model

### 2.1 Elevated process in the interactive user session - not a session-0 Windows service
`Program.cs` currently calls `UseWindowsService()` and `Service.Installer` installs the API as an
auto-start `LocalSystem` service. "Runs with admin rights" is satisfied by that, but the model does
not fit what the service has to do once the 1.9.1 code is merged:

| Needs the interactive session | Problem in session 0 |
|---|---|
| Hook compatibility channel `Local\CfxOsdHookCompatibilityV2_{pid}`, OSD shared memory, RTSS shared memory | `Local\` kernel objects are per session; a session-0 process does not see the game's objects without `Session\<n>\` paths and matching ACLs, per session, per fast-user-switch |
| Hook-free overlay (DirectComposition topmost window, `OsdHost`) | session 0 has no interactive desktop |
| Global hotkeys (`RegisterHotKey`, low-level keyboard hook) | bound to the desktop of the calling thread |
| Capture directory `Documents\CapFrameX\Captures`, `%appdata%\CapFrameX\Configuration`, portable mode | `LocalSystem` resolves these to the system profile; every path would need impersonation or explicit user resolution |
| Sounds, tray icon, toast notifications | no UI in session 0 |
| Injection into the user's game | cross-session injection works but adds a class of failures the current, verified injection path does not have |

A session-0 service plus a user-session agent would solve this with two processes and an extra IPC
hop for every frame-adjacent operation. **Recommendation:** one elevated process **in the user's
session**, which is exactly what CapFrameX 1.x is today (`requireAdministrator` manifest) minus the
UI.

**Decided 2026-09-20 - the Windows "service" is a console application that is started headless.**
It is not a Windows service in the SCM sense: no `ServiceInstall`, no `UseWindowsService()`, no
session 0. In this plan "service" names its role (the backend the frontend talks to), not the
Windows service mechanism.

- Project: `OutputType=Exe`, ASP.NET Core generic host, `requireAdministrator` manifest. The same
  shape as `CapFrameX.Service.Linux`, which is a console application too - one mental model, and
  `dotnet run` / F5 with log output on stdout and Ctrl+C shutdown work during development on both
  platforms.
- **Headless start:** a console-subsystem process gets a console window whenever it is started
  without an attached console, so the window is suppressed by *how* it is launched, not by the
  program:
  - from the scheduled task (2.2): the task action is
    `conhost.exe --headless "<install dir>\service\CapFrameX.Service.Windows.exe"`, which runs a
    console application without creating a window;
  - from code (tests, tools): `CreateProcess` with `CREATE_NO_WINDOW`.
  The program itself does not call `FreeConsole`/`ShowWindow` - hiding an already created window
  flickers. W1 verifies that no window flashes at logon and on demand start, including with
  Windows Terminal set as the default terminal; if that cannot be made reliable, switching the
  project to `OutputType=WinExe` is a one-line fallback that changes nothing else.
- A console application can still own UI-less Win32 resources: the tray icon and `RegisterHotKey`
  need a message loop, provided by one hidden message-only window on a dedicated STA thread.
- Logging: Serilog to file always; to stdout only when a console is attached (development).
  Stdout/stderr are not a channel to the host - everything goes through the API.
- Shutdown: `POST /api/app/shutdown`, tray "Exit", Ctrl+C when run interactively, and
  `CTRL_CLOSE`/session-end handling - all through the generic host's graceful stop, so PresentMon,
  the OSD/hook and PawnIO are released in order (2.3).

### 2.2 Getting elevated without a UAC prompt on every start
The desktop host is a CEF application and must stay at medium integrity (Chromium's sandbox and
GPU process are not designed for an elevated browser process). So the host cannot simply be the
parent of the service.

- Adopted with the headless-console decision: the installer registers a **scheduled task**
  `CapFrameX Service`:
  principal = the installing user, **run level `Highest`**, triggers = "at log on" (optional,
  controlled by the autostart setting) + on demand, "allow start on demand" = true, no time limit,
  single instance (`IgnoreNew`), action = `conhost.exe --headless` + the service executable (2.1).
  The legacy app already uses this mechanism for its autostart (`ColorbarViewModel`,
  `TaskRunLevel.Highest`, `LogonTrigger`; creation in `InstallerCustomActions`), so it is proven on
  the user base.
- The host starts the service with `ITaskService::Run` on that task. A user who is a member of
  Administrators can start their own highest-run-level task on demand **without a UAC prompt**; the
  one UAC prompt happens at install time.
- The service executable additionally carries a `requireAdministrator` manifest, so a direct start
  (debugging, broken task) prompts instead of running unelevated.
- `Service.Installer`: the `ServiceInstall`/`ServiceControl` elements for the CapFrameX API are
  replaced by the task registration (custom action, removed again on uninstall). The Benchlab/PMD
  service keeps its own `LocalSystem` service - it is a separate vendor component.

### 2.3 Lifecycle
- Single instance: named mutex in the session namespace; a second start signals the first and exits.
- The service owns the **tray icon** (it is the long-lived process; capture and overlay keep
  working with the UI closed). Tray actions: open UI (starts/activates the host), start/stop
  capture, overlay on/off, exit.
- Host start sequence: check `GET /api/health` -> if absent, run the task -> poll health with
  backoff (budget 5 s) -> on failure show a diagnostic page: task missing / task disabled / exit
  code "not elevated" / port in use, each with a repair action.
- Exit: UI "Exit" asks the service to stop via API (`POST /api/app/shutdown`), which stops capture,
  terminates PresentMon, releases the OSD/hook, disposes PawnIO, then exits. Closing the window only
  closes the host.
- Updates: keep the 1.9.1 model - `UpdateInstaller.TryStartPendingUpdate` runs at **service**
  start before anything is initialised; the installer replaces host, service and CEF runtime
  together, so the host is asked to close first.
- Standard (non-administrator) users are not supported, as in 1.x. Document it; do not build an
  over-the-shoulder elevation path.

## 3. Components (all under `CapFrameX.Service.Windows/src/`)

| Project | Source | Work |
|---|---|---|
| `Windows.Capture` | today's `Service.Capture` (PresentMon 2.4.0, fixed column indices, `ValidLineLength = 27`) | re-base on the PresentMon integration of merged 1.9.1 (`CapFrameX.PresentMonInterface`): newer metrics (animation error, PC latency, frame type), process list handling, the hook-free feed diagnostics. Replace fixed column indices by header-based mapping. Implements `IFrameSource`; the column -> `FrameSample` mapping is the only place that knows PresentMon. |
| `Windows.Telemetry` | today's `Service.Monitoring` (LHM port, PawnIO modules as embedded resources, NVAPI/NVML, ADLX, IGCL) | implements `ITelemetrySource` incl. the well-known metric mapping; sensor configuration persisted through shared settings. PawnIO driver presence/version check at start -> capability `telemetry.lowlevel` with a precise reason (driver missing, blocked by HVCI policy, version too old). The three native DLLs (`CapFrameX.Hwinfo`, `.IGCL`, `.ADLX`) stay vcxproj and are staged next to the host. Honour the ADLX shutdown order that fixed the exit crash in 1.9.1. |
| `Windows.Input` | today's `Service.Input` (complete, 37 tests) | implements `IHotkeyBackend`; stage 2 of the hotkey modernisation (`RegisterHotKey`) lands here, not in the legacy app |
| `Windows.Overlay` | 1.9.1: `CapFrameX.OSD.Integration`, `HookOverlayManager`, compatibility probing, `OsdOverlayBridge`, Vulkan-layer registration checks; plus `Service.Overlay` (RTSS data provider) | implements `IOverlayBackend`. Moves as-is first; the learned-profile store, stage ladder and diagnostics keep their behaviour and log lines. Overlay plan O2-O3 builds on it. |
| `Windows.Pmd` | `CapFrameX.PMD`, Benchlab artefacts under `misc/` | capability `pmd`; serial access needs no elevation but lives here for one process model |
| `Windows.Platform` | new | `IAppPaths` (Known Folders, portable mode per `PORTABLE_MODE.md`), `ITrash`, `ISystemInfoProvider` (port of `CapFrameX.SystemInfo.NetStandard`), `IPrivilegeInfo`, `IPlatformLifecycle` (mutex, tray, task registration) |

Placement rule: if a piece of code would work unchanged on Linux, it does not belong in this folder
but in `CapFrameX.Service.Shared` behind a port.

Reuse rule: legacy projects that are already `netstandard2.0`/`net10.0` after the 1.9.1 merge are
**referenced**, not copied (`CapFrameX.Statistics.NetStandard`, `CapFrameX.Data.Session`,
`CapFrameX.SystemInfo.NetStandard`, `CapFrameX.OSD.Integration`). WPF-bound projects are not
referenced; behaviour is ported.

## 4. The elevated service is a privilege boundary

An elevated process that takes commands over loopback from a medium-integrity client is a
confused-deputy risk: anything the API lets a caller do, any medium-integrity program of that user
can do **as administrator**. The token (below) is readable by those programs by design - it
authenticates "a program of this user", it cannot do more. So the defence is the shape of the API:

1. **No endpoint accepts an executable, DLL, driver or script path.** PresentMon, the hook DLLs,
   the Vulkan layer, PawnIO modules and updater packages are resolved from the installation
   directory only, and verified (Authenticode for binaries, the existing SHA-256 check for update
   packages) before use.
2. **File operations are limited to data roots** (capture directory, config directory, export
   target chosen through the host's native dialog). Paths from the client are canonicalised and
   must stay inside a root; no reparse-point traversal; delete = recycle bin and only for files the
   record index knows.
3. Changing a data root through the API is allowed, but the new root must not be inside
   `%WINDIR%`, `Program Files*` or another user's profile.
4. **No generic primitives**: no "run", no registry endpoint, no raw MSR/port endpoint - PawnIO is
   used by the telemetry code only; nothing exposes it.
5. Injection targets are chosen by the service's own detection logic; the API can select among
   detected candidates by pid, never inject into an arbitrary pid/path supplied by the client.
6. Kestrel binds `127.0.0.1` only; `Host` header must be `127.0.0.1:<port>`/`localhost:<port>`
   (DNS-rebinding), `Origin` must be the app scheme, token required on every route (UI plan 5.1).
7. Token hand-over: generated per service start, written to
   `%LOCALAPPDATA%\CapFrameX\run\service.token` with an ACL for the user only, deleted on exit. The
   host reads it and injects it into the page. Rotate on every start; never log it.
8. A threat-model review of the route list is a release gate for every milestone that adds mutating
   endpoints.

Follow-up to evaluate (not for M0-M2): a named pipe with an ACL as transport for the host's
requests instead of TCP, which removes the browser-reachable surface entirely.

## 4a. On-demand operation (issue #396)

The Windows service implements the on-demand policy of the architecture plan, section 8. Windows
specifics:
- **PresentMon** runs only while a frame-source lease exists (capture armed, an overlay module that
  shows frame metrics, a live view). No lease -> no PresentMon process, no ETW session.
- **PawnIO**: the device is opened and modules are loaded on the first lease of a sensor that needs
  them and released after the grace period. Sensors served by NVAPI/ADLX/IGCL or plain Windows APIs
  do not touch PawnIO. The service is elevated regardless (requirement); what is on demand is the
  driver use, not the privilege.
- **Overlay stack**: `cfx_osd_core`, hook injection and the compatibility probing start with an
  active overlay profile, not with the service.
- Native wrappers (ADLX, IGCL, Hwinfo) load with their first sensor lease - and unload in the order
  that fixed the ADLX exit crash.
- Lightweight modes are UI configuration (UI plan section 4a); the lightest state - service + tray +
  hotkeys, no CEF host - is the default after logon autostart.

## 5. Work packages

| WP | Deliverable | Acceptance |
|---|---|---|
| W1 | `CapFrameX.Service.Windows` host as console application: manifest, elevation self-check, single instance, tray + hotkey message loop, shutdown endpoint; remove `UseWindowsService()`; headless start via the task | starts elevated via the task without UAC and **without any visible window** (logon and on demand, Windows Terminal as default terminal included); unelevated start exits with the documented code |
| W2 | Installer: scheduled-task registration replaces `ServiceInstall`; uninstall removes it; upgrade from a build that installed the Windows service removes that service | clean install, upgrade and uninstall verified on Windows 11 |
| W3 | Host <-> service start sequence and diagnostic page (2.3) | every failure mode in 2.3 reproduced and shown with its repair action |
| W4 | Privilege-boundary rules of section 4 in `Service.Api` + tests (path canonicalisation, Host/Origin, token) | negative tests green; route review signed off |
| W5 | `Windows.Platform` ports | conformance suite green with real Windows ports |
| W6 | `Windows.Capture` re-based on merged 1.9.1 PresentMon integration, `IFrameSource` | integration tests against `vkcube` (the 1.9.1 `Integration` category, elevated) pass; metric capability flags correct |
| W7 | `Windows.Telemetry` as `ITelemetrySource`, PawnIO capability reporting, well-known metrics | sensor values match 1.9.1 on the same machine; no exit crash (ADLX) |
| W8 | `Windows.Input` as `IHotkeyBackend` | capture start/stop by hotkey with the UI closed |
| W9 | `Windows.Overlay` (move of the 1.9.1 overlay stack into the service process) | hook-free, DXGI hook and Vulkan layer paths verified in one title each; learned profiles keep working |

Order: W1-W5 belong to the UI plan's M0/M1 (W1 + W4 replace the generic "token auth" part of B1),
W6-W8 are M2, W9 is M3 together with the overlay plan.

## 6. Risks

| Risk | Mitigation |
|---|---|
| Scheduled task missing/disabled by policy or "cleaner" tools | start diagnostics + one-click repair (re-registration needs one UAC prompt); manifest as fallback |
| Elevated service becomes an elevation-of-privilege vector | section 4 as design rule, negative tests, route review gate |
| Moving the overlay stack out of the WPF process changes timing (render loop, GC pauses were tuned there) | move as-is first, keep the extended OSD logging, compare `HookFree OSD: ...` summaries before/after |
| PresentMon version skew between `Service.Capture` (2.4.0) and merged 1.9.1 | W6 starts from the 1.9.1 integration; header-based column mapping |
| Elevated process started at logon delays the desktop | on-demand policy (architecture plan section 8): start-up does configuration, token, listener, tray and hotkeys only; PresentMon, PawnIO, OSD and EF Core load on first lease; measure logon impact in W1 |

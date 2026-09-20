# CapFrameX Service Redesign - Development Log

Last updated: 2026-05-21

This document records the implementation state and decisions for the CapFrameX backend redesign. It complements the broader CEF/Angular plan in `dev-plans/CEF_Angular_CapFrameX_Next_DevPlan.md`.

## Goals

- Move CapFrameX backend work into a modern service boundary.
- Keep frontend communication typed, observable, and testable without a desktop host.
- Support Windows and Linux at the service level where possible.
- Keep Windows-only capabilities such as PresentMon, PawnIO, RTSS, and vendor driver integrations isolated behind provider boundaries.
- Preserve proven legacy monitoring and capture behavior during migration.

## Architecture Direction

The service is split into a small host plus capability-specific modules:

- `CapFrameX.Service.Api`: ASP.NET Core host, localhost API, lifecycle and bridge endpoints.
- `CapFrameX.Service.Contracts`: DTOs shared between service and frontend bridge.
- `CapFrameX.Service.Core`: domain models and interfaces.
- `CapFrameX.Service.Application`: application orchestration.
- `CapFrameX.Service.Infrastructure`: infrastructure services and event bus.
- `CapFrameX.Service.Data`: SQLite/data access layer.
- `CapFrameX.Service.Input`: input abstraction.
- `CapFrameX.Service.Capture`: PresentMon capture integration; Windows-only capability.
- `CapFrameX.Service.Monitoring`: LibreHardwareMonitor-derived sensor stack; currently Windows-heavy because of PawnIO and vendor APIs.
- `CapFrameX.UI`: Angular frontend consuming the service through typed HTTP DTOs and event streaming.

Linux should not load or require PawnIO, PresentMon, RTSS, or Windows driver integrations. These should become optional Windows provider assemblies or Windows-only capability registrations.

## Frontend Bridge Status

Implemented baseline:

- Shared DTO project: `CapFrameX.Service.Contracts`.
- HTTP endpoints for app health/version, capabilities, capture status, and records.
- Server-sent event endpoint at `/api/events`.
- Bridge heartbeat service publishing `app.heartbeat`.
- Angular API client with typed DTOs.
- Angular `EventSource` bridge client for backend events.
- CORS/local-origin policy prepared for localhost, Tauri, and app-style schemes.

Current transport choice:

- HTTP for request/response calls.
- SSE for low-frequency backend events.
- WebSocket or another binary/streaming path should be evaluated before sending high-frequency frametime or sensor streams through Angular change detection.
- A future CEF host should keep native host actions narrow and allowlisted. The service bridge should remain testable without launching CEF.

## Monitoring Sync Status

On 2026-05-21, `source/LibreHardwareMonitorLib` was synchronized into `CapFrameX.Service/src/CapFrameX.Service.Monitoring`.

Transferred areas include:

- ADLX-based AMD GPU interop.
- Nvidia display handle mapping updates.
- Hardware simulation support.
- Intel IMC, OC mailbox, and OOBMSM wrappers.
- New PawnIO resource binaries present in the legacy tree.
- Updated package versions to match the legacy monitoring implementation.

Service-specific adaptations:

- Namespace mapped from `LibreHardwareMonitor` to `CapFrameX.Service.Monitoring`.
- Legacy `CapFrameX.Monitoring.Contracts.ISensorConfig` dependency mapped to a local service contract.
- Legacy `CapFrameX.Extensions` dependency mapped to local service extensions.
- `NativeMethods.txt` keeps SetupAPI entries required by CsWin32 for battery/device enumeration.
- `AnyCPU` builds map to `x64` so CsWin32 generation produces the expected Windows bindings.
- `IntelOOBMSM.bin` is conditionally embedded when the binary is added later.

Verification:

- `dotnet build CapFrameX.Service\src\CapFrameX.Service.Monitoring\CapFrameX.Service.Monitoring.csproj` succeeds.
- `dotnet build CapFrameX.Service\src\CapFrameX.Service.Api\CapFrameX.Service.Api.csproj` succeeds.
- The monitoring project still reports legacy warnings, mostly nullability, XML documentation, unused interop fields, and NuGet pruning warnings.

Known monitoring gap:

- `IntelOOBMSM.bin` is referenced by the new OOBMSM wrapper but is not yet present in the legacy tree. The service project is prepared to embed it once it is added.

## Ignore/Generated Output Policy

Generated frontend output should not be committed:

- `node_modules/`
- `.angular/`
- `/CapFrameX.UI/dist/`

The Angular production bundle can be regenerated with `npm run build` from `CapFrameX.UI`.

## Immediate Next Development Steps

1. Convert capability discovery from static placeholders to real provider registration.
2. Split Windows-only capture and monitoring providers from cross-platform service contracts.
3. Add a provider health/capability endpoint that reports unavailable features instead of failing service startup.
4. Add bridge contract generation or a stricter manual sync process for TypeScript DTOs.
5. Add integration tests for `/api/health`, `/api/app/version`, `/api/capabilities`, and `/api/events`.
6. Decide whether the next desktop host milestone uses CefSharp first or a custom CEF host.
7. Define the high-frequency streaming path for capture frames and sensors before wiring real live data to the Angular UI.

## 2026-09-20 - Implementation planning

- Scope: planning only, no code changes.
- Added `CapFrameX_2.0_UI_Implementation_Plan.md` (mockup-driven UI plan, backend slices, work
  packages M0/M1, decisions D1-D7) and `CapFrameX_2.0_Overlay_DevPlan.md` (tile-based overlay on
  `cfx_osd_core`, design basis TroyMetrics/Benchmark-Overlays, licensing constraints, core gap
  list G1-G15). The central UI mockup is stored in `dev-plans/mockups/`.
- The "Immediate Next Development Steps" above are superseded by section 8 of the UI plan; items
  1-3 (capability providers) move to milestone M2, items 4-5 are work packages B4 and B1.
- Findings recorded while surveying the code: `release/2.0.0` is 364 commits behind
  `release/1.9.1`; the API host has no authentication; `Service.Application` is empty and nothing
  from Data/Infrastructure/Capture/Monitoring is registered in `Program.cs`; no statistics code and
  no legacy record importer exist in the service; `CapFrameX.UI` cannot run `ng test` (missing
  `tsconfig.spec.json` and karma dependencies) and references missing `src/assets`/`favicon.ico`.

## 2026-09-20 - Requirement: frontend on Windows and Linux

- Scope: planning only, no code changes.
- Decision input: the Angular frontend and its desktop shell must run on both platforms. CefSharp is
  therefore out; recommended shell is a native C++ CEF host (CEF Views), verified by a spike on both
  platforms (WP-H1). Tauri stays in the tree as a documented fallback until the spike is accepted.
- UI plan section 6 now specifies: shell options, the host bridge per platform, capability-driven
  frontend rules (no platform checks in feature code, bundled font), service platform providers
  (`Service.Capture.Linux` as a client of the `capframex-linux` daemon, `Service.Monitoring.Linux`
  on hwmon/sysfs/NVML, hotkeys, system info), XDG paths through `IAppPaths`, CI on Windows + Ubuntu
  from M0, AppImage/.deb packaging.
- Overlay plan section 9: design, template schema, presets and editor are shared; renderers are not
  (`cfx_osd_core` on Windows, the Vulkan-layer overlay on Linux).
- New open decisions: D6 now includes the Windows elevation constraint (PresentMon needs
  elevation, the CEF host must not run elevated); D8 - retiring the Avalonia GUI.
- Milestone M1 (records + analysis) needs no platform provider and is accepted on both platforms.

## 2026-09-20 - Service architecture, folder split, OSD reuse, lightweight modes

- Scope: planning only, no code changes.
- Decisions taken: two separately implemented services; the Windows service always runs elevated
  and keeps PresentMon + PawnIO; the Linux service takes presents from the Vulkan layer; the
  Avalonia GUI is removed; top-level folders `CapFrameX.UI`, `CapFrameX.Service.Windows`,
  `CapFrameX.Service.Linux`; `CapFrameX.OSD` (above all its Vulkan layer) is reused on Linux;
  lightweight modes and the "evaluate on demand / load on demand" policy from issue #396 are
  requirements for 2.0.
- New plans: `CapFrameX_2.0_Service_Architecture_DevPlan.md` (shared core + two hosts, platform
  ports, one contract with a conformance suite, repository layout, demand registry),
  `CapFrameX_2.0_Windows_Service_DevPlan.md` (elevated user-session process via a highest-run-level
  scheduled task instead of a session-0 Windows service; privilege-boundary rules for the API),
  `CapFrameX_2.0_Linux_Service_DevPlan.md` (Avalonia salvage + removal, daemon absorbed by the
  service, hotkeys, packaging), `CapFrameX_2.0_Linux_Telemetry_Validation_Plan.md` (kernel
  interfaces as primary source, NVML/RAPL/Super-I/O gaps, probe tool, validation phases V0-V4),
  `CapFrameX_2.0_OSD_CrossPlatform_DevPlan.md` (one OSD repository split into shared/windows/linux;
  portable rasteriser behind `Renderer`, POSIX IPC shim, capture ported into the OSD Vulkan layer).
- Findings: `vk_compositor.cpp`, `layer.cpp`, the shaders and the scene model of `CapFrameX.OSD`
  contain no Win32/Direct2D code; the Vulkan layer already consumes CPU-rasterised BGRA frames
  (`cfx_osd_create_cpu`), so Linux needs a different rasteriser and IPC shim, not a new renderer.
  The old Linux app writes CSV + JSON sidecar records, not the Windows JSON record - a read-only
  importer is planned. The legacy app already registers a `TaskRunLevel.Highest` scheduled task for
  autostart, which the Windows service model reuses.
- D9 confirmed the same day: the platform-neutral core lives in a fourth top-level folder,
  `CapFrameX.Service.Shared`.
- Windows process model decided the same day: the service is a console application started
  headless (scheduled task with highest run level, `conhost.exe --headless`), not an SCM Windows
  service.
- Open: rasteriser choice after OSD spike X1, telemetry helper decision
  after validation phase V3.
- The earlier entry's Linux notes (`Service.Capture.Linux` as client of the daemon inside one
  service, Tauri/CefGlue fallbacks) are superseded where they conflict with the above.

## 2026-09-20 - Implementation started: restructure, ports, first TDD slices

Scope: first code of the 2.0 service work. Windows written test-first, Linux written against the
shared ports with its pure parts under test on any platform.

- **Structure (A1)**: `CapFrameX.Service/` dissolved with `git mv` into `CapFrameX.Service.Shared`
  (Contracts, Core, Application, Data, Api + Data.Tests), `CapFrameX.Service.Windows`
  (Capture, Telemetry = ex-Monitoring, Input, Overlay, Ipc = ex-Infrastructure, installer, tools,
  tests, native) and `CapFrameX.Service.Linux` (new). Renamed projects keep their old
  `RootNamespace`/`AssemblyName`, so namespaces and embedded resource names are unchanged.
  One solution per service, classic `.sln`.
- **Contracts/ports (A2, A4)**: `FrameSample`, `FrameMetrics`, `FrameSourceInfo`,
  `PresentingProcess`, `WellKnownMetrics`, `CapabilityIds`; ports `IFrameSource`,
  `IFrameSubscription`, `ITelemetrySource`, `IHotkeyBackend`, `IOverlayBackend`, `IAppPaths`,
  `IPrivilegeInfo` plus `PlatformAvailability`, `SensorDescriptor`, `SensorSample`.
- **DemandRegistry** (issue #396, shared): leases per demand key, grace period on release so a view
  switch does not restart PresentMon, effective interval = shortest any holder insists on.
  Test-first, 12 tests, `TimeProvider`-based so the grace period is deterministic.
- **PresentMon 2.5.1**: binary taken from `release/1.9.1`, replaces 2.4.0. The fixed column indices
  are gone: `PresentMonColumnLayout` reads the indices from PresentMon's header line,
  `PresentMonFrameMapper` maps a row to `FrameSample`, and the capture service re-binds its columns
  when the header arrives. Unavailable values stay `null` instead of becoming zero. Test-first,
  25 tests. `ParameterNameIndexMapping` keeps the legacy alias names.
- **PawnIO**: complete wrapper, driver and modules taken from `release/1.9.1` (42 files, namespaces
  mapped). New: `NvidiaThermal.cs`, `PawnIO.inf`/`.cat` (PnP install), `IntelOOBMSM.bin` (the gap
  the log noted in May), `IntelPCHThermal.bin`, `Nvidia.bin`, `ZhaoxinMSR.bin`; 22 modules embedded.
  `IntelCpu` call site ported to the new `IntelOcMailbox` signature (NGU sentinel fallback).
  Module lookups now use a literal namespace and one folder casing, pinned through `LogicalName` -
  `nameof(CapFrameX.Service.Monitoring)` would resolve to `Monitoring` and miss every module.
- **Linux (against the ports)**: `LayerMessageHeader`, `LayerFrameData`, `LayerProtocolReader`
  (stream reassembly, payload limit), `LayerFrameMapper` (absolute ns to capture-relative seconds,
  the layer's "zero means unmeasured" convention) and `XdgAppPaths`. All of this is pure and runs
  under test on Windows; 26 tests. Sockets, layer and telemetry readers are still open.
- **Test suite repairs**: the Monitoring tests opened the sensor stack from several classes in
  parallel and a different test failed on every run - parallelisation disabled for that assembly,
  19/19 stable across repeated runs. The capture integration tests now report themselves as
  *skipped* without administrator rights instead of failing.
- **Also fixed**: `CapFrameX.DatabaseTool` targeted `net9.0` against `net10.0` projects and could
  not build.

Verification: `dotnet build` and `dotnet test` on both solutions - Windows 151 passed, 6 skipped
(need elevation); Linux 62 passed.

Known gaps: the 1.9.1 merge (plan task P1) has still not happened, so the rest of the Monitoring
tree remains the May 2026 snapshot while its PawnIO layer is current - re-syncing
`LibreHardwareMonitorLib` wholesale is the follow-up. PresentMon 2.5.1 has not been exercised
against a running game, because that needs an elevated session.

## 2026-09-20 - Merge of release/1.9.1 and full LibreHardwareMonitorLib sync

- `release/1.9.1` merged into `release/2.0.0`; the branch is no longer behind. Only two conflicts:
  - `.gitignore`: 1.9.1 ignores `/CapFrameX.Service/` and `/CapFrameX.UI/`. Those lines were **not**
    taken - that code is tracked on 2.0.0, and the second rule would have hidden new frontend files.
  - `README.md`: kept 1.9.1's product description and replaced the outdated 2.0 section with the
    current folder layout.
- **`source/LibreHardwareMonitorLib` fully adopted into `CapFrameX.Service.Windows.Telemetry`**
  (212 files, 182 of them sources). The transformation is mechanical and verified to reproduce the
  library exactly: namespace `LibreHardwareMonitor` -> `CapFrameX.Service.Monitoring`,
  `CapFrameX.Monitoring.Contracts` and `CapFrameX.Extensions` mapped onto the service's local
  replacements. New from 1.9.1: `Hardware/SensorPolling.cs`,
  `Hardware/Storage/ICancellableNVMeDrive.cs`.
- Three corrections the sync had to make, each a trap for the next sync:
  - the upstream **licence header is preserved verbatim** - the May 2026 sync had rewritten
    "Copyright (C) LibreHardwareMonitor and Contributors" into the service namespace, which restates
    someone else's attribution; links to the upstream repository are preserved too;
  - `nameof(LibreHardwareMonitor)` cannot survive a multi-part namespace, because `nameof` yields
    only the last identifier - every PawnIO module lookup is a literal now, in one folder casing,
    pinned through `LogicalName` in the project file;
  - three library files are Latin-1 encoded and are normalised to UTF-8 on the way in.
- Package references of both projects are identical, so the project file needed no change beyond
  the PawnIO item groups.

Verification: both solutions build; Windows 151 passed / 6 skipped (need elevation), Linux 62
passed; all 22 PawnIO modules embedded and every lookup in the code resolves against the built
assembly.

Open: the rest of `source/` is not consumed by the service yet (statistics, session model); the
capture path has still not been exercised against a running game, which needs an elevated session.

## 2026-09-20 - PresentMon 2.5.1 verified against real output (elevated session)

With administrator rights the capture integration tests finally *run* instead of reporting
themselves skipped. What that turned up:

- **Bug found and fixed:** `PresentMonTestConfiguration` still named `PresentMon-2.4.0-x64`. The
  earlier rename only matched the name *with* the `.exe` suffix, so the tests were starting a
  binary that no longer exists. This is why the capture service returned `false` on start.
- **The header-driven column binding is confirmed by real output.** The shipped build prints
  **27** columns, not the 29 of `PresentMonKnownLayouts`: without `--track_frame_type` there is no
  `FrameType` column and no `MsInstrumentedLatency`. With the fixed indices the service used
  before, everything from `TimeInSeconds` onwards would have been read one column off. The observed
  header and a real row are now a test fixture (`PresentMonRealOutputTests`), including the case
  where a column exists but its value is `NA`.
- **Open question - PresentMon does not observe every renderer here.** On this machine PresentMon
  reports a D3D9 window continuously but never the Silk.NET **OpenGL** test renderer, and never
  **vkcube** either, with both processes alive and presenting (window present, NVIDIA driver,
  60 FPS). Verified that this is *not* caused by the version bump: PresentMon **2.4.0**, restored
  from git, behaves identically. Five `TestRendererIntegrationTests` therefore still fail; their
  assertions now say why instead of "collection was empty". Whether this is an environment issue
  (a competing ETW session - one named `PresentMon` keeps reappearing) or a genuine coverage gap
  for OpenGL and Vulkan titles is unresolved and worth its own investigation, because it would
  affect users with such titles.

Verification: Windows unit tests 121 passed (12 shared, 24 data, 47 input, 19 telemetry, and 54 of
59 capture); the 5 failures are the integration tests above. Linux 62 passed.

## 2026-09-20 - PresentMon command line aligned with 1.9.x

The capture service was starting PresentMon with a shorter switch set than the shipping
application, which silently changed what PresentMon reports. Written test-first
(`PresentMonArgumentsTests`, 13 tests):

- **`--track_frame_type` added.** Without it PresentMon omits the `FrameType` column entirely and
  frame generation is invisible to the analysis.
- **`--track_app_timing` added** and **`--set_circular_buffer_size 4096`** added.
  `PresentMonCircularBuffer` normalises the value, because PresentMon only accepts powers of two
  and rejects the *whole* command line otherwise - which would keep the service from starting.
- **`--restart_as_admin` removed.** 1.9.x has it commented out for a reason: the service is always
  elevated, so it buys nothing, and a PresentMon that re-executes itself takes the redirected
  stdout with it - and that stream *is* the capture.
- The test configuration builds the same switches now; tests that exercise a different command line
  than the service uses prove nothing.

With these switches PresentMon is expected to emit the 29-column layout `PresentMonKnownLayouts`
describes (`FrameType` from the first switch, `MsInstrumentedLatency` from the second). **Not yet
confirmed against live output** - see below.

### Open: PresentMon captures nothing on this machine

Measured repeatedly on 2026-09-20 in an elevated shell, with `vkcube` running and presenting:

- every run reports `warning: 46000-76000 ETW events were lost`, and no CSV is written at all -
  not even a header, with minimal switches just as much as with the full set;
- the circular buffer size makes no difference (2048 and 8192 behave identically), so this is ETW
  session buffer loss, not the present event buffer;
- earlier in the session PresentMon did report one D3D9 window continuously while never reporting
  the OpenGL test renderer or vkcube; once that window closed, output stopped entirely;
- **not caused by the version bump**: PresentMon 2.4.0, restored from git, behaves the same;
- the machine has ~50 active ETW sessions, and a leftover session named `PresentMon` reappears
  after every run.

Until this is understood the five `TestRendererIntegrationTests` cannot pass, and the live
behaviour of the new switches is unverified. Worth checking next: whether another tool holds the
providers, whether a reboot clears it, and whether PresentMon needs larger ETW session buffers
(`--set_trace_buffer_*` style options) on a machine this busy.

## 2026-09-20 - B1: the localhost API is no longer unauthenticated

`Program.cs` called `app.UseAuthorization()` without any authentication scheme - decoration, not a
check. On Windows this process is elevated, so every route was lending administrator rights to
whatever asked for it. Written test-first (31 policy tests, 17 endpoint tests):

- `SessionToken` (`Service.Core/Security`): 256 bits from `RandomNumberGenerator`, base64url so it
  survives a query string, compared with `CryptographicOperations.FixedTimeEquals`. Generated per
  service start; the host supplies it through `CAPFRAMEX_SERVICE_TOKEN`, and a run without one
  still starts so development is not blocked.
- `LocalApiGuard` (same folder, no ASP.NET types, so the rules are testable without a host) checks
  three things in this order:
  1. the `Host` header names this service's loopback endpoint - any website can point its own name
     at 127.0.0.1, and the Host header is what separates that from a local caller;
  2. the `Origin`, when sent, is the CapFrameX frontend - this is what stops a page from driving
     the service, since a browser always sends it cross-origin;
  3. the token matches.
  Host is judged first on purpose: a caller already out of bounds learns nothing about the token.
- `LocalApiGuardMiddleware` runs in front of every route. Refusals answer `application/problem+json`
  with 401 or 403 and never name the failed check; the reason goes to the log only. Asserted by a
  test, because a helpful error message here is an oracle.
- The event stream may take its token from the query (`access_token`), because `EventSource` cannot
  set request headers; every other route rejects a query token, since it ends up in logs.
- CORS lost the `tauri://localhost` origin - the Tauri scaffold is superseded by the CEF host.

The endpoint tests host the real API through `WebApplicationFactory`, so they fail if the
middleware is ever dropped from the pipeline - which unit tests of the policy alone would not
notice.

Not yet done from B1: `Service.Api` is still an executable rather than the library the two
composition roots map, and the token is not yet written to a file the desktop host can read.

## 2026-09-20 - Start model and token hand-over

Decision recorded in the Windows and Linux plans: the **frontend starts first** and starts the
service; started on its own the service is an ordinary console application with a visible console,
which is the diagnostic path and the reason its project stays `OutputType=Exe`. A frontend that
finds a running service through `GET /api/health` attaches to it rather than starting a second one.

Both orders have to end with the frontend knowing the session token, and it must never travel on a
command line, where any process could read it from the process list:

- frontend first: the token goes in the child process environment (`CAPFRAMEX_SERVICE_TOKEN`);
- service first: it generates its own.

Either way the service publishes it to `<runtime directory>/service.token`, rewritten per start and
removed on shutdown, for a frontend that did not start it. `SessionTokenStore` (shared, 11 tests)
owns that logic; the one platform-specific part is behind `ISecretFileWriter`:

- `WindowsSecretFileWriter` replaces the file's ACL with the current user alone and **switches
  inheritance off** - a runtime directory that inherits "Users: read" would otherwise hand the
  token to every account on the machine, and an added rule does not remove an inherited one. Four
  tests assert the resulting ACL, including that no inherited entry survives. Setting the DACL had
  to go through `FileInfo`: a plain write handle lacks `WRITE_DAC` and the first implementation
  threw `UnauthorizedAccessException`.
- `PosixSecretFileWriter` creates the file, applies mode `0600`, then writes - so the token never
  exists on disk under the process umask. Verified on Linux later; it cannot run on Windows.

New project `CapFrameX.Service.Windows.Platform` holds the Windows side.

## 2026-09-20 - The two composition roots exist

`CapFrameX.Service.Api` is a library now, not an executable. It exposes `AddCapFrameXApi` and
`MapCapFrameXApi`; routes live there and nowhere else, so both hosts map the same endpoints and the
contract cannot drift between the platforms. `Worker.cs`, the project template's stub, is gone.

- **`CapFrameX.Service.Windows`** (`net10.0-windows`, console app, `requireAdministrator` manifest):
  checks elevation first and exits with a distinct code rather than serving half an API - PresentMon
  needs a real-time ETW session and PawnIO its device, so there is no degraded mode. Also refuses to
  start when the port is taken. `WindowsAppPaths` keeps the 1.x locations so both generations see
  the same settings and captures; portable mode is switched on by `portable.json` next to the
  binaries.
- **`CapFrameX.Service.Linux`** (`net10.0`, console app): same shape, XDG paths, no elevation - the
  Vulkan layer needs none and the kernel exports its telemetry to everyone.
- Both publish the session token on start and revoke it on shutdown, and both take one from
  `CAPFRAMEX_SERVICE_TOKEN` when the frontend started them.
- `POST /api/app/shutdown` is the deliberate "Exit": the service outlives the frontend on purpose,
  so closing the window must not stop it. It answers before stopping, because a caller that gets no
  response cannot tell a shutdown from a crash.

The API tests no longer host a `Program`; they build a host through the two extension methods, which
is exactly what a composition root does - so they fail if a host ever has to add the guard itself.

Verified against the running elevated service, not only in tests: `401` without a token, `200` with
the token read from the published file, `403` from a foreign origin, `202` on shutdown, process
gone, token file removed.

Known limitation: a *forced* kill cannot run the shutdown hook, so the token file survives it. That
is not an opening - the token only authenticates against a service that is running, and the next
start overwrites it - but a frontend must treat the file as a hint and confirm with
`GET /api/health`, never as proof that a service is there.

## 2026-09-20 - The database now exists

It did not before: the `InitialCreate` migration had been written in December but **never applied**.
Neither host referenced `CapFrameX.Service.Data`, no `DbContext` was registered, and no database
file existed on a developer machine. The schema was a file in the repository, nothing more.

- `AddCapFrameXDatabase(paths)` registers the context and the three repositories, and
  `DatabaseMigrationService` applies pending migrations before the service answers. Migrations, not
  `EnsureCreated`: an installation holding a user's records has to be upgraded, not recreated.
- The path comes from `IAppPaths.DataDirectory`, not from a hard-coded `LocalApplicationData` - the
  old `CapFrameXDbContextFactory.GetDefaultDatabasePath()` ignored both portable mode and the XDG
  layout. That factory stays for the EF design-time tools only.
- Both hosts register it, so Windows and Linux create the same schema in their own place.

Verified live: starting the Windows host logs `Applying 1 database migration(s):
20251226142228_InitialCreate`, creates `capframex.db`, and the database tool reads the three tables
back.

**Schema tests now run against real SQLite** (`SchemaMigrationTests`), not the in-memory provider,
which ignores relational constraints and would have let all of this pass. That immediately turned
up a property worth knowing before the record importer is written: **`Session.SuiteId` is a
mandatory foreign key**, so importing a capture file means creating or choosing a suite for it -
there is no loose session. The plan's item to move `Service.Data.Tests` off the in-memory provider
is started rather than finished: the existing repository tests still use it.

Also: EF Core moved 9.0.0 -> 10.0.1 to match the target framework.

### Open: dependency advisories

`dotnet build` reports two `NU1903` warnings that predate this work and are not resolved by the EF
upgrade: `SQLitePCLRaw.lib.e_sqlite3` (2.1.10, still 2.1.11 under EF 10) and `Microsoft.OpenApi`
2.3.0, both "known high severity". Neither has an obvious fixed version on the feed. They ship in a
service that runs elevated on Windows, so they want a decision rather than a warning everyone
scrolls past.

## 2026-09-20 - Dependency advisories, and what the design-time package was shipping

Asked whether `SQLitePCLRaw.lib.e_sqlite3` works on Linux. It does - the package carries
`libe_sqlite3.so` for linux-x64 plus arm64, musl and more, and a cross-publish of the Linux host
from Windows picks up the right one and no Windows DLL. Following that up cleared the build of
security warnings and turned up something bigger:

- **`SQLitePCLRaw`**: the advisory covers `<= 2.1.11`; pinning `SQLitePCLRaw.bundle_e_sqlite3` to
  2.1.13 resolves it. EF Core also moved 9.0.0 -> 10.0.1 to match the target framework.
- **`Microsoft.OpenApi`**: advisory covers `<= 2.7.4`, fixed in 2.7.5; pinned to 2.12.2.
- **`Microsoft.EntityFrameworkCore.Design` was shipping build tooling.** It arrives with
  `Microsoft.CodeAnalysis.Workspaces.MSBuild` and `Microsoft.Build.Tasks.Core`, which is also where
  the six `System.Security.Cryptography.Xml` advisories came from. `PrivateAssets=all` was already
  set and did *not* prevent it: a publish of the Linux service contained `BuildHost-net472/` and
  `BuildHost-netcore/`. Moving the package out of `Service.Data` and into the `CapFrameX.DatabaseTool`
  startup project took the publish from **85 files to 39** and removed those directories.

Result: `dotnet build` of the Linux solution reports **no advisories at all**; the Windows solution
reports them only for `CapFrameX.DatabaseTool`, a developer tool that is not part of a service
install. The migration command moved with the package and is documented in the Data project's
README; the services still migrate themselves at start, so nothing is run by hand on a user's
machine.

The migration workflow is verified, not only documented: with `dotnet-ef` 10.0.12 installed,
`migrations list` finds the context through the tool as startup project, and an `add` / `remove`
round trip works. The probe migration came out **empty**, which is the point - the model matches
the snapshot, so the design-time setup survived moving the package.

That round trip did regenerate `CapFrameXDbContextModelSnapshot.cs`: `ProductVersion` 9.0.0 ->
10.0.1 and `ToTable("X")` -> `ToTable("X", (string)null)`, an EF 10 codegen change. No table or
column changed. It is committed rather than reverted, because a stale snapshot would mix this noise
into the next real migration.

## 2026-09-20 - B2 started: reading capture files

`CapFrameX.Service.Records` exists and reads the capture format CapFrameX 1.x writes. It
**references** `CapFrameX.Data.Session` rather than reimplementing the model: records travel
between machines and between both generations, so a second parser would only find new ways to
disagree with the files users already have.

`RecordFileReader` returns a result, not an exception. The indexer walks a directory the user
controls, where a half-written file, a leftover from a crashed capture and anything else ending in
`.json` are all normal; each of those has to skip one file, not stop the scan.

Two things the tests turned up, both worth keeping in mind for the indexer:

- **`SampleTime` is an integer**, not a timestamp, and `CreationDate` carries sub-second precision
  with a `Z` suffix. The first hand-written fixture guessed wrong and the reader rejected it.
- **A JSON object without a `Runs` array throws `ArgumentNullException`**, not `JsonException`:
  the legacy `[JsonConstructor]` receives null and passes it to `new List<ISessionRun>(null)`. The
  reader therefore catches broadly on parse - deliberately, because the input is user-controlled
  and the contract is "skip the file".

Tests: 12 against hand-written fixtures, plus two that read the **running user's own capture
folder** when there is one. On this machine that is **306 real captures, all of them readable**.
The fixtures pin the shape the reader expects; the folder test pins that the shape is the one real
files have. It skips where no captures exist, so a green run on a build agent is no evidence it ran.

Still open in B2: the `AddRecordSource` migration, the summary projection with duration and
sparkline, the indexer with its file watcher, and the `/api/records` endpoints.

## 2026-09-20 - B2: record summaries and the sparkline

`RecordSummaryFactory` projects a capture onto what the list shows: name, game, process, hardware,
duration, run and frame counts, and a sparkline. Two decisions worth keeping:

- **Duration is the span the frames cover**, summed per run, not the frame count times a frame time
  - a capture with dropped frames or several runs would otherwise be misreported.
- **Metrics a capture does not carry are flagged, not zeroed** (`HasPcLatency`, `HasDisplayChange`),
  so the list can say "this capture has no latency" instead of showing 0 ms.

`FrametimeSparkline` uses **min/max decimation**, not sampling or averaging. The sparkline's job is
to show whether a capture stuttered, and a single 200 ms frame among ten thousand 16 ms ones
vanishes under an average and is missed entirely by every-nth sampling. Tests pin exactly that: a
spike survives, the lowest frame time survives, and a rising series stays rising - a sparkline that
reorders its points would be a lie.

Verified against reality: **all 306 captures in the running user's folder** read, and every one of
them produces a usable list entry - a name, at least one run, frames, a duration above zero and a
sparkline within budget. That test skips where no captures exist.

## 2026-09-20 - B2 complete: the index, the watcher and `/api/records`

The remaining three pieces of B2, which together make a folder of capture files appear in the
frontend as a list.

**Schema** - `Session` gained the record source (`SourceFilePath`, `SourceFileSize`,
`SourceModifiedUtc`, `IndexVersion`) and the list projection (`DurationSeconds`, `RunCount`,
`FrameCount`, `SparklineJson`, `HasPcLatency`, `HasDisplayChange`); `SessionRun.CaptureDataJson`
and `SensorDataJson` became optional. Migration `20260920153818_AddRecordSource`. Two indexes carry
rules rather than performance: `SourceFilePath` is **unique and filtered to rows that have one**,
so a file cannot be indexed twice while self-recorded sessions do not all collide on NULL, and
`IndexVersion` finds the rows a changed projection has to re-read. `IsRequired()` on the two JSON
columns had to go first - it silently overrode the nullable properties, and EF emitted no
`AlterColumn` at all.

**The frame data is not copied into the database.** The capture file stays the record; the table is
an index over it. Copying the frames in would double the storage and create a second version of the
same capture that can drift from the first - and CapFrameX 1.x keeps writing those files.

**`RecordIndexPlanner`** decides what a scan means, apart from both the file system and the
database, so the rules are testable on their own: a file not in the index is *added*; a file whose
size or last write time differs is *updated*; a row below the current `IndexVersion` is *updated*,
which is how a changed projection re-reads everything without a schema migration; a row **above**
it is left alone, so an older service sharing a database does not undo a newer one; an indexed
record whose file is gone is *removed*; and a row without a source file takes no part at all,
because a scan of the folder says nothing about a session the service recorded itself. Paths are
compared the way the platform compares them - ignoring case on Windows only.

**`RecordIndex`** applies the plan, and **`RecordIndexer`** keeps it following the folder: a
start-up scan, then a `FileSystemWatcher` whose events are coalesced into one scan after the folder
has been quiet for `SettleDelay` (2 s by default). Writing one capture raises several events and
the last can arrive well after the first, so scanning on the first would read a half-written file.
Three details that are deliberate:

- **The watcher is created before the first scan**, not after: a capture written in between is then
  either caught by the watcher or still found by the scan. The other order has a gap where it is
  neither.
- **Every change ends in a full scan.** The planner is cheap, and a scan cannot get out of step
  with the folder the way a queue of individual file events can - a dropped event, a rename, a
  watcher buffer overflow all come out the same.
- **A file that could not be read costs one file, not the scan**, and nothing is stored for it, so
  the next scan sees it as new and tries again. The folder belongs to the user, where a leftover
  from a crashed capture or something that merely ends in `.json` are normal.

**Events** - background work below the API reaches the event stream through
`IBridgeEventPublisher` (in `Service.Core`), which `BridgeEventStream` implements explicitly. Only
a scan that actually changed something publishes `records.changed`; the frontend reloads the list
on it, so an unreadable file must stay silent.

**`/api/records`** serves the index, newest first, with `search` (game or process, case ignored,
and `%` is a character rather than a wildcard because it comes from a search box), `skip`/`take`
and a total; `/api/records/{id}` returns one record or a 404 problem. `AverageFps`, `P1Fps` and
`P99Fps` are **deliberately null**: the index computes no metrics, and their definitions belong to
B3 over `CapFrameX.Statistics.NetStandard`, where parity with 1.x is pinned. Inventing them here
would risk two different answers to the same question.

Both hosts register the index after the database - hosted services start in registration order and
the migration step runs to completion before the next one begins, which is what keeps the first
scan from meeting a schema that is not there yet.

Verified: `dotnet test` over all five shared suites - Shared 64, Api 27, Data 33, Records 46,
Application 19 - all green. The watcher tests were checked for vacuity by disabling
`EnableRaisingEvents`: the three that claim to need it then fail.

Known gaps: `RecordsController` exposes no detail endpoint yet (frame data for the analysis view is
B3), and the record list shows no FPS until B3 lands. A leftover `CapFrameX.Service.Windows.exe`
from an earlier session holds its own build output, so the host was verified with `-t:Compile`
rather than a full solution build.

## 2026-09-20 - Schema diagram

`dev-plans/database/cx2-database-schema.html` - the three tables with every column, both
relationships, the indexes and what each one is for, and the two shapes a `Sessions` row takes
(indexed capture file versus recorded by the service). It follows the architecture diagram's design
so the two read as one set.

The page is generated by `dev-plans/database/generate.py` from a single column description, so the
entity boxes and the reference tables cannot drift apart and the SVG geometry is placed rather than
guessed. After a migration, update the description in that script and run it again; editing the
HTML by hand undoes both. The description is the schema as `CapFrameXDbContextModelSnapshot`
states it, which is the file to re-read when they disagree.

## 2026-09-20 - B3: the analysis over the 1.x statistics

`CapFrameX.Service.Analysis` turns a capture into what the analysis view shows. **It computes no
statistic of its own.** Every number comes from `FrametimeStatisticProvider` and every sequence
from `SessionExtensions`, the same code the desktop app runs, so the two cannot disagree about what
a percentile is. What the project does own is the mapping - which window, which run, which sequence
a metric needs - and that is what the tests are about.

**`MetricCatalog`** names the metrics for the frontend (`p95`, `onePercentLowAverage`, …) and marks
which sequence each needs. Two things it settles:

- **Three metrics need the GPU-active column, not the frame times.** `GpuActiveAverage`,
  `GpuActiveP1` and `GpuActiveOnePercentLowAverage` share their branch inside the provider with
  `Average`, `P1` and the 1% low average. Feeding them frame times returns the plain metric under a
  GPU label - a wrong number that looks right. They are computed from
  `GetGpuActiveTimeTimeWindow` and come back empty where the capture has no such column.
- **The catalogue lists exactly what the provider can compute**, pinned by a test that walks every
  `EMetric` and compares "the provider returns a number" against "the catalogue offers it". Frames
  per watt needs power data and a coefficient, so the provider answers `NaN` and the catalogue
  leaves it out rather than offering a tile that never fills.

**Frame pacing** is the split 1.x draws as a pie: stutter and low-FPS time percentages from the
provider, smooth as the remainder. The **spikes are the very frames the stutter percentage is made
of** - same rule, same moving average - so the count on the card and the share behind it can never
tell different stories.

**Parity tests** compare the adapter against the provider called directly, per metric, under
outlier removal and inside a window, plus the frame pacing, the L-shape and the rounding digits.
One of them pins something subtle: the service reads frame times **as points**, because a spike has
to keep the time it happened at, and that path has to select the same frames as the plain value
path. They also run over **every capture in the running user's folder**, where dropped frames,
several runs and a first frame time of zero are real rather than imagined. Checked for vacuity by
dropping the outlier argument in the adapter: four of them turn red.

**Two deliberate departures from 1.x**, both recorded because they are deviations rather than
oversights:

- **Latency is read from the capture data**, not through `GetPcLatencyPointTimeWindow`, which
  returns nothing unless every run also carries sensor data. Latency comes from PresentMon and
  sensor readings do not, so that guard hides the latency of every capture recorded without
  hardware monitoring - and the record list, which reads the column directly, would promise a value
  the analysis never delivers.
- **Only two outlier methods are offered.** `ERemoveOutlierMethod` declares five; the provider
  implements `DeciPercentile` and returns the sequence unchanged for interquartile range, three
  sigma and two-and-a-half sigma. Accepting those would put a setting in the UI that does nothing,
  so the API refuses them and names the two that work.

**`SessionCache`** keeps parsed captures in memory between calls, bounded by **frame count rather
than entry count**: captures differ in length by two orders of magnitude, so counting them would
either waste memory on short ones or run out on long ones. The file's size and modification time
are part of the key, so a capture rewritten on disk is a different entry rather than a stale one,
and a capture larger than the whole cache is served but not kept.

**`/api/records/{id}/analysis`** and **`/api/records/{id}/series`** serve it, through
`RecordAnalyzer`, which is the one place that puts index row, file and analysis together. The
series is **columnar** - one time axis, every other column indexed by it - and **removes no
outliers**, because dropping frames from one curve would misplace every point after the first. A
column that has no value for a frame carries `null` there, which a chart draws as a gap rather than
a drop to the floor. Everything a caller names is returned or refused: an unknown metric or curve
is a 400 listing the known ones, an unknown record a 404, and a record whose file has gone a 409
rather than an empty chart.

**The record list now carries its metrics.** `Sessions` gained `AverageFps`, `P1Fps` and `P99Fps`
(migration `20260920164331_AddRecordMetrics`), filled by `RecordIndex` **through the same analysis
adapter** - this is what B2 left open on purpose, and a second calculation here is exactly how the
list and the open record would come to disagree. `RecordIndexPlanner.CurrentIndexVersion` went to
**2** with it, so every already-indexed row is re-read on the next scan without a schema migration
filling in numbers that were never computed. That path has its own test.

Verified: all six shared suites green - Shared 64, Api 39, Data 33, Records 46, Application 22,
Analysis 114 - and both solutions build. The real-capture parity test ran rather than skipped, over
the 306 captures in this machine's folder.

Known gaps: `RecordDetailDto`, `PATCH` and `DELETE /api/records/{id}` are still open from section
5.4, as are the settings endpoints; the analysis is reachable only by record id, so a capture that
is not indexed cannot be analysed.

## 2026-09-20 - 5.4: the record detail, editing and deleting

**`GET /api/records/{id}` now returns `RecordDetailDto`** - the summary from the index row, the
machine and the runs from the capture file, and the chips above the chart. It reads the file, so a
record whose capture has gone answers 409 rather than a view that renders and says nothing.

`RecordDetailFactory` composes the chips server-side. Two rules worth keeping:

- **A platform switch has three states.** CapFrameX writes "Enabled", "Disabled", or an empty
  string where it could not find out, and "not known" is not the same claim as "off" - so the DTO
  carries `bool?` and the unknown case is `null`.
- **The switches that are on share one pill**, and a capture with none gets no pill at all. They
  are only interesting together, and a row of "Disabled" chips says nothing a reader wants.

**`PATCH /api/records/{id}`** writes the correction into the capture file, because the file is the
record: the change has to survive being copied to another machine, and CapFrameX 1.x reads the same
files. The editable set is exactly the one 1.x allows - game name, comment, processor, graphics
card, memory, motherboard, resolution. Four things the tests pin:

- **A field left out is left alone; an empty string clears it.** That is what makes it a patch, and
  a client editing only the comment must not blank hardware it never sent.
- **A patch that changes nothing does not write.** Rewriting would change the file's timestamp,
  wake the watcher and re-index a record for no reason.
- **The edit is read fresh, not from the session cache.** The cached parse is shared with whoever
  is looking at the record right now, and mutating it would show them an edit that has not been
  written - or, if the write fails, one that never will be.
- **The index re-reads that one record immediately** (`RecordIndex.RefreshAsync`), storing the new
  size and time, so the next request answers with the edit and the watcher's next scan finds
  nothing to do.

The file is serialised into memory and moved into place rather than written over directly: this
overwrites something the user cannot get back, and a failure halfway through a direct write would
leave a truncated capture where a whole one was. The scratch file ends in `.tmp` so a scan cannot
pick a half-written capture up as a record.

**`DELETE /api/records/{id}` never unlinks a capture.** A record is hours of benchmarking that
cannot be recaptured, so it goes to the platform's trash behind a new port, `IFileTrash`:

- **`WindowsFileTrash`** calls `SHFileOperation` with `FOF_ALLOWUNDO`. Worth recording: the first
  attempt declared the struct with `Pack = 1`, which is what most examples on the web show, and it
  crashed the test host with an access violation inside `shell32`. The shell reads the fields at
  the offsets its own compiler chose; with default alignment it works. The test goes through the
  real recycle bin, because a fake would only assert that we wrote the call we wrote.
- **`XdgFileTrash`** implements the freedesktop layout: `files/` and `info/` under the trash home,
  a percent-encoded `.trashinfo` record, and a fresh name when one is taken. The info file is
  created with `CreateNew` *before* the move, which is how two programs trashing the same name at
  once each get a name of their own - the file system decides rather than a check-then-write. All
  of it is testable on Windows, since it is file operations and a text format.
- **A capture that refuses to move keeps its record.** Dropping the row while the file is still in
  the folder would only bring it back on the next scan, under a new identity and without its
  history.

`AddCapFrameXRecordIndex` was split from `AddCapFrameXRecordWatcher`: editing a record needs the
index to re-read it, while a background scan is a thing a host runs and a test would rather not.

Verified: Shared 64, Api 57, Data 33, Records 70, Application 25, Analysis 114, Windows platform
13, Linux 35 - all green, both solutions build.

Known gaps: the record list still takes only `search`, `skip` and `take`, not the `game`, `from`,
`to` and `sort` filters section 5.4 sketches; the settings endpoints and `settings.changed` are
untouched. Four projects still carry package references NuGet reports as redundant
(`System.Text.Json`, `System.Threading.AccessControl`, `System.Security.Principal.Windows`,
`System.Runtime.CompilerServices.Unsafe`); they predate this work and were left alone.

## 2026-09-20 - The record list filters

`GET /api/records` now takes `game`, `from`, `to` and `sort` beside the `search`, `skip` and `take`
it already had, and `GET /api/records/games` returns the games the index holds - the filter chips
are built from that rather than from whatever happens to be on the current page. The querying moved
out of the controller into `RecordLibrary`, next to the other record services.

- **`search` and `game` are different questions.** `search` is the box the user types in, matched
  against game and process as a substring with `%` and `_` as ordinary characters. `game` is a chip
  they picked from the games that exist, so it matches the whole name - "Portal" must not drag in
  "Portal 2".
- **`to` is inclusive.** A capture taken at the exact second the user asked up to is inside the
  range they asked for.
- **`sort` is one parameter**, with a leading `-` for descending, because a sort is one decision
  and that is the convention someone typing a URL will guess. An unknown field is a 400 listing the
  known ones, like the metrics and series parameters before it.
- **A record whose metric was never computed sorts last either way.** "Not measured" is not
  "slowest", and a record the index has not caught up with must not head a list sorted by frame
  rate. Pinned by a test that fails if the null handling goes.

The order ends in the record identity so it is total. Worth being precise about what that is worth:
the test pins that identity is the last key - reversing it fails - but **it does not prove the list
would be wrong without it**, because SQLite returns these rows in the same order either way. The
clause is there so the order does not depend on that happening to stay true.

Verified: Shared 64, Api 76, Data 33, Records 70, Application 25, Analysis 114 - all green, both
solutions build.

## 2026-09-20 - Settings, and what changing one does

`GET` and `PATCH /api/settings` carry three sections: the analysis options, the capture folder and
the theme. The defaults are the ones CapFrameX 1.x ships, so a record analysed in either
application gives the same numbers until the user says otherwise.

**A setting that does nothing until the next start is not a setting.** The store is a singleton
holding the *live* objects: `AnalysisSettings`, which the statistics provider reads on every call,
and `RecordIndexOptions`, which the indexer watches. Both are updated in place rather than
replaced, because the provider and the indexer hold them by reference and a new instance would
leave them on the old one. Three tests pin the effect rather than the value - changing the
stuttering factor changes the next frame pacing, changing the rounding changes the next metric, and
changing the folder moves the index.

**The folder can move while the service runs.** `RecordIndexOptions.CaptureDirectory` is no longer
fixed at start-up; setting it raises `Invalidated`, the indexer disposes its watcher and takes up
the new folder, and the scan that follows drops the records of the old one - the index describes
the folder being observed, and the files themselves are untouched. `Invalidate()` is the same
channel without the move, for when what the index *stores* has to change.

**Changing the rounding digits marks every record for re-reading.** The list shows numbers the
index computed once; without this it would disagree with the record it opens in the last decimal
place until each capture happened to be touched again. `IndexVersion` goes to 0 and the watcher
rescans - the version column doing what it was built for. Only the rounding digits get this
treatment, because everything else is read per request.

**Validation is where the quiet failures are.** Rounding digits outside 0 to 15 make `Math.Round`
throw, which the statistics provider catches and turns into a metric that is silently absent; a
stuttering factor of one or less reports a perfectly even capture as stuttering the whole way
through. The whole patch is checked before any of it is kept, so one bad value changes nothing
rather than half of what was asked for, and the answer names every fault at once rather than making
the user find them one request at a time.

Two smaller decisions:

- **A separate file**, `ServiceSettings.json`, beside 1.x's `AppSettings.json` rather than inside
  it. Two applications rewriting one file would each drop what the other had added.
- **The capture folder is stored as null when it is the platform's own**, not as the resolved path,
  so a portable installation moved to another drive does not carry a path that no longer exists.

The analysis endpoint now starts from the configured options and lets the query override them, so
the tile row and the L-shape a user chose are what they get without the frontend repeating them on
every request. `OutlierMethods` moved out of the API into the analysis project, where the settings
validator needs it too.

Verified: Shared 64, Api 89, Data 33, Records 70, Application 50, Analysis 114 - all green, both
solutions build. The two live-effect tests were checked for vacuity by not applying the stuttering
factor and by skipping the re-projection; both turn red.

**Not a gap, a decision** (confirmed the same day): section 5.2 originally had the indexer read
1.x's `ObservedDirectory` out of `AppSettings.json`. It does not, and it will not. The index is
what the service knows about records now, and a second place to configure where they live is one
that can disagree with it - silently, because nothing would say which of the two won. The capture
directory is the service's own setting, starting at the platform's folder; a user who keeps
captures elsewhere says so once. Section 5.2 and 5.4 were corrected to match.

## 2026-09-20 - Import, and the reversal of D3

**An imported record carries its capture.** Decision D3 - "the legacy capture file stays the source
of truth, SQLite is an index over it" - was reversed deliberately, with the cost stated: the
running user's 306 captures are 483 MB of JSON that now go into the database. What it buys is that
a record opens whether or not the file it came from still exists, has been moved, or sits on a
drive nobody plugged in.

The folder scan stays for the service's own capture directory, so both shapes exist side by side
and the schema already had room for them: a record either **carries** its capture (imported, or
recorded by the service) or **points at** one (found by the scan). `StoredRecordFactory` rebuilds
the first into the same model the reader produces from a file, which is why the statistics, the
detail view and the chart need to know nothing about where a record came from.

**Identity comes from the capture, not from the file.** `Session.Hash` - what CapFrameX computes
over the runs - is now stored by both paths and unique. One capture is one record however it
arrived: through a scan, through an import, or as the same file copied under another name. The
folder the service already watches is never offered as an import source, for the same reason.

**The payload is stored as the text the file held**, not serialised back from the parsed model.
Several parts of the 1.x model - sensor data most of all - go through converters that are lossy in
one direction, so a round trip would quietly store something other than what the user captured.
Text in, text out.

**What the real captures caught.** The generated fixtures are single-run and carry the handful of
columns the tests assert on. Importing 25 real captures and comparing every metric against the same
analysis run on the file found a difference in exactly one: the adaptive deviation, the only
order-dependent metric in the list. EF returns a capture's runs in whatever order suits it, and
several of these captures have three. Runs now carry a `RunIndex` and are read back in it. Worth
recording twice over: the bug was invisible to every synthetic test, and the metric that exposed it
was the one nobody would think to check.

A second thing the fixtures were lying about: every generated capture carried the same `Hash`, so
the first dedupe test looked like a bug in the dedupe. Generated captures now get their own.

**The first import is offered once.** `GET /api/records/import/sources` suggests folders out of
CapFrameX 1.x's `AppSettings.json` - `CaptureRootDirectory` and `ObservedDirectory`, resolving the
`MyDocuments\` token it writes - with a capture count each. That is the whole of the legacy
relationship: read once, to make a suggestion, never followed afterwards. `settings.import.offered`
remembers the answer, whichever way it went, and importing sets it too.

Verified: Shared 64, Api 102, Data 33, Records 70, Application 69, Analysis 114 - all green, both
solutions build. The run-order test was checked for vacuity by reversing the ordering; it turns
red.

Known gaps: a single capture file can be imported by naming it, but there is no upload endpoint -
the path has to be reachable by the service, which is true for a local frontend and not for
anything else. Nothing removes an import source from the suggestions once it has been imported;
re-importing it is cheap and answers "already known", but the count still shows.

## 2026-09-20 - The frontend: F0, F1 in part, F3, and the analysis slice

**F0 - the toolchain.** Angular 18 to **21**, `application` builder (esbuild), **zoneless**, ESLint
and Vitest. All three acceptance commands are green: `npm run build` (initial **72 kB** transfer
against a 300 kB budget), `npm test` (15), `npm run lint`.

Angular **22 was not reachable**: it wants Node 22.22.3, 24.15.0 or 26, and this machine has Node
24.13.0. 21 is the highest that installs here. Also worth knowing for anyone repeating the upgrade:
`ng update @angular/cli --name <migration>` always fetches the *latest* CLI, so the optional
migrations could not be run either - the `application` builder configuration was written by hand
instead, which the build then proved correct.

Removed on the way: `ngx-echarts` (an unused wrapper that pinned Angular 18 - `echarts` itself
stays, lazily, for M4), the Tauri packages from `package.json`. `src-tauri/` stays: removing it is
WP-H3 and depends on H1 confirming the CEF host, which decision D1 still has open.

**The hard-coded API URL is gone.** `RUNTIME_CONFIG` reads `window.__CX__` - base URL and session
token - which the desktop host sets before any page script. Under `ng serve` there is no host, so
the token is empty and the service refuses and says so, which is better than a frontend that
silently talks to nothing. One interceptor adds the token, prefixes `/api/...` and turns
problem-details bodies into the service's own sentence; "Http failure response for ..." helps
nobody. A `SILENT` context exists for the health poll, because a poll that exists to notice the
service is gone must not report that news every five seconds.

**F1 in part.** `tokens.css` holds the token table from section 2.3, light and dark, plus the
eight-colour series palette and the type, spacing and control scales the mockup implies. The
mockup's own `--color-*` names are mapped onto the `--cx-*` tokens at the bottom of the file, which
is what will let the reference page render the mockup markup unchanged. The icon sprite is built
from Tabler by `tools/build-icon-sprite.mjs` and committed - 4.5 kB for fourteen icons.

**F3 - the shell.** Rail, workspace, status bar, nine lazy routes. The rail is declared once in
`rail-items.ts` and the routes follow it, so adding a view is two entries and nothing else. The
status bar carries the service light and the error surface.

**The analysis slice (F5, and F7 apart from the chart).** `RecordLibraryStore` and `AnalysisStore`
are signal stores, no state library. The library reloads on `records.changed` from the event
stream, so an import or a deletion - in this window or another - appears without a refresh. The
workspace shows the chips, the configured stat tiles, the frame pacing card and the latency card,
all from the service's own analysis, so the numbers are the ones the desktop application computes.

`BridgeEvents` replaces the old SSE service: the token goes in the query string because
`EventSource` sends no headers, `Last-Event-ID` is carried across our own reconnects because the
browser only sends it on its own, and the backoff resets on a connection that actually **opened**
rather than on the attempt - a service that accepts and immediately drops would otherwise spin.

Not done, and not claimed: **F2** (the kit exists as the components this slice needed - icon,
sparkline, chip, stat tile, card, record card, search field, page header - but there is no
`/dev/kit` showcase and no axe check), **F4** (no mockup reference page, so no screenshot diff),
**F6** (no chart - the workspace has a placeholder where it goes), **F8** (the settings view is
still a placeholder, although the endpoints behind it are finished), and the Inter font is not
bundled, so the type falls back to the system face and the screenshot test F4 needs cannot be
stable yet.

## 2026-09-20 - Running the two halves together for the first time

The service and the frontend had never been started at the same time. Every layer had tests and
every layer passed; the seams between them had never been touched. Three of them were broken.

**The default port was CapFrameX 1.x's.** 1.x serves its metrics API on 1337 by default
(`WebservicePort`), so with 1.x running - which is the whole premise of the transition - the 2.0
service refuses to start, correctly and uselessly. The default moved to **17337**, the port is
overridable through `CAPFRAMEX_SERVICE_PORT` the way the token already was, and the service now
writes the port it actually bound to `service.port` beside the token, so a frontend that did not
start the process can still find it. A test pins the default against 1337 and against the copy of
the number the test host needs as a compile-time constant - which is how the first attempt was
caught, as ninety-nine API tests failing for one hard-coded `127.0.0.1:1337`.

**The record list never asked for anything.** The store built its request out of
`toObservable(computed(...))`, whose emission is driven by an effect. Nothing ticked the
application, so the effect never ran, and the list sat on "Loading…" forever while the status bar
beside it said the service was connected. It is an `httpResource` now, which is eager and re-runs
when its request computation changes - no effect in the path at all. The unit test that reproduces
it passed against the old code as well, because `TestBed.tick()` supplies exactly the thing the
browser did not; what settled it was counting requests in the service log across a browser run.

**Every component's outermost style rule missed its element.** `host: { class: 'cx-thing' }` puts
the class on the host, but a `.cx-thing` rule inside the component's styles is scoped to the
template, which the host never matches - so every width, padding and display meant for a host
element was silently dropped, and the 188px record list spanned the whole window. All twelve
components now use `:host`. No test would have caught this; it took looking at the rendered page.

Also added: `tools/dev-host.mjs`, which serves the built frontend on port 4200 - the origin the
guard accepts - and injects `window.__CX__` from the token and port the running service published.
It is what the CEF host will do, so the frontend can be run against a real service today and needs
nothing from the build to find it.

**What the run proved.** 306 real captures indexed and listed, analysis, series, detail, settings
and import endpoints answering, the guard giving 401 without a token, 403 for a foreign origin and
200 for the dev origin, the CORS preflight allowing `x-capframex-token`, the event stream
delivering heartbeats with an id to resume from, and every one of the eleven DTO shapes the
frontend reads matching the service's JSON key for key - no missing field, no extra one. The
analysis view shows the real numbers for a real capture: 113 avg, 128 P95, 30 1% low, 97.9% smooth,
three spikes, and "this capture does not carry latency" where it does not.

## 2026-09-20 - F6 and F8: the chart, and settings

**The last of the fragile pattern is gone.** `AnalysisStore` still read the open record through
`toObservable(...)`, the construction that left the record list loading forever. It worked in the
browser - but by luck: `selectedId` is set by an effect after construction, and by then the
application happened to be ticking. Two `httpResource`s now, which are eager and skip the request
by returning `undefined` while nothing is selected. Its tests include the case that matters for
resources: reading `value()` in an error state throws, so both are read through `hasValue()`.

**F6 - the chart.** uPlot on canvas, four tabs (frame times, FPS, L-shape, distribution), drag to
zoom, the theme bridge, and the spike annotation. Three things worth keeping:

- **The x axis is not time.** uPlot reads an x scale as unix timestamps by default, so twenty
  seconds of capture came out labelled 1/1/1970. `scales.x.time = false`.
- **The annotation is a DOM element, not canvas.** It is a label with text in it, and text drawn on
  canvas is text no screen reader reaches and nobody can select.
- **It is placed from uPlot's own hooks** (`ready`, `setScale`, `setSize`), not after the
  constructor. Measuring right after `new uPlot(...)` put the "374 ms spike" pill outside the plot,
  clipped to "4 ms spike" - which looked like a wrong number from the service and was not: the
  service had 373.77 ms at 17.58 s, exactly where the peak is.

Only the two curves that need frame data are fetched, and only while one of them is showing: the
L-shape and the distribution already arrive with the analysis, and a series is a megabyte.

**The chart gate is not measured**, and no number is claimed for it. It needs automation that can
drive a pan and count frames; headless screenshots fire at an unpredictable moment - three of six
attempts here caught the page before its data arrived. Playwright, which WP-F4 needs anyway, is the
prerequisite.

**F8 - settings and import.** The service owns every value: a patch goes out, it validates the
whole thing, and the answer is what the settings now are - so the form cannot show something the
service rejected. The import section lists what `import/sources` suggests, and it suggested the
real thing: CapFrameX 1.x's observed folder, 13 captures, read out of its `AppSettings.json`.

Importing it reported **13 already known, 0 imported**, and the count stayed at 306 - the dedupe
holding across both paths on real data, because that folder sits inside the one the scan watches.

Verified: Api 109, Shared 64, Data 33, Records 70, Application 69, Analysis 114, frontend 20; build
and lint green; the analysis view drawing a real 2254-frame capture.

## Documentation Rules For Future Steps

For every meaningful backend/frontend migration step, update this log with:

- Date.
- Scope.
- Files or modules touched.
- Design decision made.
- Verification command and result.
- Known gaps or follow-up work.

Keep detailed subsystem documentation in the subsystem README. Keep cross-cutting architecture decisions and chronological progress here.

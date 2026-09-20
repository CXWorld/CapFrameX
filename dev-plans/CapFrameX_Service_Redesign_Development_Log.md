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

## Documentation Rules For Future Steps

For every meaningful backend/frontend migration step, update this log with:

- Date.
- Scope.
- Files or modules touched.
- Design decision made.
- Verification command and result.
- Known gaps or follow-up work.

Keep detailed subsystem documentation in the subsystem README. Keep cross-cutting architecture decisions and chronological progress here.

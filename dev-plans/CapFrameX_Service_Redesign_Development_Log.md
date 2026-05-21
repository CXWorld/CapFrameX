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

## Documentation Rules For Future Steps

For every meaningful backend/frontend migration step, update this log with:

- Date.
- Scope.
- Files or modules touched.
- Design decision made.
- Verification command and result.
- Known gaps or follow-up work.

Keep detailed subsystem documentation in the subsystem README. Keep cross-cutting architecture decisions and chronological progress here.

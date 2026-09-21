# Overlay compatibility report collection

The in-game overlay always probes and learns locally, for both DXGI and Vulkan. The old
`HookOverlayAutoCompatibility` setting is ignored. A failed run no longer prevents a new game
launch from probing. The UI no longer exposes a global reset or a switch to disable learning.

`ShareOverlayCompatibilityProfiles` is a separate setting and defaults to **false**, including
upgrades with existing profiles. Only the explicit checkbox in the overlay options enables
collection. It covers future probe sessions; pre-existing learned files and raw logs are never
uploaded. Disabling it cancels in-flight requests, clears sessions and unsent reports, and
retires the random participant identifier. An upload already received by the server cannot be
recalled by canceling the client request. Re-enabling creates a new identifier.

## Collected evidence

The schema is `CapFrameX.OverlayReporting.OverlayProfileReport` in
`source/CapFrameX.OSD.Integration/HookProfileReport.cs`, mirrored in the update-server repository.
Both copies must remain identical; incompatible changes require a new schema/consent review.

| Group | Fields and interpretation |
| --- | --- |
| Identity | Random participant, session and report UUIDs; checkpoint sequence; schema and consent version. No login, machine name, serial number or process ID. |
| Application | CapFrameX version/channel, Windows build and OS architecture. |
| Game and hook | Basenames, file/product versions, PE architecture, size, SHA-256, read status. SHA-256 is omitted and marked for inaccessible files or files over 512 MiB. |
| Runtime context | An allowlist of FG runtimes, DXGI/D3D/Vulkan, known overlay/proxy modules and the OSD. Versions, hashes and a system/application location category; never filesystem paths. Unchanged binaries are cached by size and modification time. |
| Hardware | GPU inventory with name, driver version and PCI vendor/device IDs. No complete PNP identifiers or serial numbers. |
| Profile | Evidence and early signatures, stage/source, flags, injection module/delay, verified/exhausted state, pending stage/flags/delay, verdict, attempts and ladder. Free-text reasons containing paths are excluded. |
| DXGI timeline | Injection results, stage actions/outcomes, visibility/fallback, native state/error/install phase, applied/pending flags, resolution/API, metric count/flags, FG technology/activity/authority, Streamline mode, queue/decline/route and coverage counters. |
| Vulkan timeline | Requested/actual composite route, result, acknowledgment revision, capabilities, bitness, generation, composited-present count, native vendor/device/driver, queue family/flags, format/color space/usage, resolution and profile outcomes. |

State transitions are retained immediately at the manager's polling cadence, including changes
after a profile was learned. DXGI and Vulkan observations are compared separately. Unchanged
states update an in-memory latest snapshot instead of appending periodic duplicate events.
Reports retain counter baselines, the last sample before a transition and the new state;
counter resets and heartbeat freshness changes also count as transitions.

The background worker runs every 30 seconds and persists/uploads new evidence. An unchanged
session produces a checkpoint only every **10 minutes** (six per hour instead of 120), plus
initial and final reports. Carrying forward a profile or status does not itself trigger a report.
Metadata is still scanned every two minutes normally, and refreshed when profile evidence
changes. Changed metadata, including read failures and newly loaded modules, triggers a report;
enumeration order and observation timestamps alone do not. `contextObservedUtc` dates the
metadata snapshot separately from the recorded events.

Each report remains self-contained in the existing v1 schema, with profile, metadata and
available status. Sequence numbers count emitted reports, so suppressed duplicates do not create
gaps. A target change closes the observed session; application exit attempts to persist final
evidence without network or hardware queries.

## Limits when interpreting data

- `Verified` means the local probe observed its success condition (currently two seconds), not
  that the route was necessary, optimal, crash-free, or validated for all FG modes.
- Evaluate the preceding decisions and metric readiness. A timeout with empty metrics can
  produce an unnecessary compatibility escalation followed by success.
- DXGI coverage counts refer to the **generic native route**. Vendor-proxy drawing is not fully
  covered by those counters. Counters can reset or wrap; compare adjacent samples accordingly.
- During unchanged states, counters describe the interval between retained samples, not a
  ten-second timeline. A forced application exit can lose the unreported portion of the current
  ten-minute stable interval; it does not establish a crash or a clean game exit.
- DXGI hardware is an inventory; the current native status does not identify the active GPU on
  systems with multiple adapters. Vulkan carries native device/vendor/driver identifiers.
- FG observations describe actual native telemetry, including its authority/unknown state.
  They do not prove the user's intent, capture every very short transition, or constitute
  validation of a provider that was merely loaded.
- `target-changed` and a last checkpoint do not establish either a crash or a clean game exit.
  No crash dumps, raw logs or user screenshots are collected.
- Read failures, missing metrics, absent status, dropped events and sequence gaps must remain
  visible during analysis. Do not fill missing context by assuming a healthy or default state.
- The random participant identifier distinguishes installations, not authenticated independent
  users. Reports are untrusted observations and are **not** distributed as recommendations.

## Transport and storage

The endpoint is `/api/v1/overlay-reports` on the HTTPS origin of `UpdateCatalogUri`, currently
`https://updates.capframex.com`. Redirects and cookies are disabled. No account token is attached.
Success is acknowledged with HTTP 202 (200 is also accepted by the client). Offline failures,
429 and server errors retain the same report UUID and back off. The server deduplicates identical
retries and rejects conflicting payloads using the same UUID.

The client outbox is `Configuration/OverlayProfileReports` (portable paths are respected):
maximum 128 reports, 256 KiB each, seven days; at most eight uploads per flush. In-memory
limits are four observed targets, 32 pending checkpoints and 256 events per checkpoint. Event
truncation is counted, with initial decisions and recent states retained. These limits can
discard old evidence during extended outages; sequence gaps make missing reports apparent.

The server accepts only the v1 contract, rejects unknown properties, paths/control characters,
oversized or malformed data, and limits collector traffic independently of update downloads.
Each report is stored as an atomic JSON file with a server receipt time and payload hash. It
does not persist IP addresses, authorization headers or request bodies in application logs.
Network infrastructure still necessarily receives the source IP; this is pseudonymous reporting,
not a claim of network anonymity. The supplied Caddy site has no access-log directive.

Server defaults: `/var/lib/capframex-updates/overlay-reports`, 10 GiB, 100,000 reports, 180-day
retention. Expiration is checked on collection after startup and hourly under traffic. A full
or unavailable collector returns 503 with backoff; release downloads remain available. There
is no public report listing/download API. Analysis uses administrative access to that directory.
See `E:\Code\CapFrameX.UpdateServer\COLLECTION.md` for deployment and operational details.

## Verification

Client MSTest coverage includes consent default/migration, no collection without consent,
private-field exclusion, FG changes after learning, cancellation and purge, immediate off/on,
durable retries, backoff, bounds and endpoint restrictions. Simulated stable sessions verify
the ten-minute cadence, independent DXGI/Vulkan comparisons, counter baselines/resets, brief
FG and heartbeat transitions, metadata changes and final reports. Existing DXGI/Vulkan learning and
live-switch regressions run alongside these tests.

The update-server test project exercises validation, duplicate/conflicting UUIDs, storage
quotas, retention and real local HTTP requests including chunked oversized bodies. All test
reports are synthetic and go to local test servers or in-memory handlers.

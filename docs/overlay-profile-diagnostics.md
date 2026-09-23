# Overlay diagnostics and render verification

The opt-in collector now emits schema 2 to `POST /api/v2/overlay-reports`.
Consent remains opt-in; disabling sharing cancels collection/uploads and removes unsent reports.
Local compatibility learning does not require sharing.

With sharing disabled (the default), reporting returns at a cached consent flag before
allocating snapshots, formatting status strings, taking collection locks or reading timestamps.
Report-only host-state and process-bitness queries are skipped. No report timer is created;
revocation disposes it and queued callbacks return immediately. Cleanup of unsent reports runs
once at startup/revocation, rather than on a periodic worker while disabled.

The allocation regression covers 10,000 iterations of all disabled reporting entry points,
including DXGI/Vulkan status and host observations, both from startup and after revocation.
Native status/progress needed by the local overlay and compatibility learner remains active;
it does not create diagnostic reports or depend on the sharing option.

## Evidence and traffic

- Native samples include cumulative Present and successful overlay-draw counts, the last draw's
  age, render-context generation, route and applied compatibility flags/sequence. D3D11 and all
  D3D12 render paths contribute successful draws. No frame-by-frame trace is uploaded.
- Counter/heartbeat movement updates the latest local snapshot without creating a report.
  Transitions, counter resets and generation changes retain both sides of the change.
- `host-state` records the selected runtime, requested overlay visibility, a coarse window state
  (`unknown`, `foreground`, `background`, `minimized`) and fixed fallback source/reason codes.
  Window queries occur only with consent, at most once a second. No titles, handles, other PIDs,
  filesystem paths or free-form fallback messages are transmitted.
- `module-change` contains names added, removed or updated between successful context scans.
  The event time is **first observed**, not an exact DLL load/unload time. The existing two-minute
  scan and evidence-triggered refresh are retained. No additional modules are collected.
- A stable session sends an initial report and one checkpoint every ten minutes. Changed state
  can be flushed by the existing thirty-second worker; shutdown/target changes retain final data.
  The existing 256-event, 256-KiB/report, 128-file and seven-day outbox bounds remain in effect.
- Schema 2 omits default/null JSON values. Each checkpoint still carries enough profile and
  context information to analyze it independently if earlier segments are missing.

The deterministic stable-hour test currently transfers seven reports totaling about 16 KiB for
its synthetic context. Actual size varies with the module inventory and number of transitions.

## Local proof of rendering

`Local\CfxOsdHookProgressV1_<pid>` is a separate 64-byte, read-only-to-host mapping. It leaves
the existing 64/128-byte native status ABI unchanged. The header is magic `0x31505243`, version,
PID and size. An odd/even publication word protects the generation, 64-bit Present/draw counters,
last-draw tick, route, applied flags/sequence and pending-restart flags across both bitnesses.
Readers discard incomplete or changing publications.

Present counts include hooked Present entries. Draw counts advance only where the renderer
reports a successful overlay draw. The render generation changes when the drawing swapchain or
route changes or renderer targets are retired. Internal swapchain pointers never leave the hook.
Nested vendor Presents that do not draw do not change the drawing context.

Verification requires new successful draws spanning at least two seconds, at least two draws
beyond the initial baseline, fresh output and acknowledgement of the applied stage. Re-reading
a frozen `Rendered` flag/heartbeat cannot verify a profile. Pauses, missing proof, counter resets
and context changes invalidate the current confirmation and require fresh proof without
exhausting the route. Genuine failures continue through the existing escalation/fallback policy.
Old hooks without the side channel remain displayable but cannot establish a new verification.
This is proof of observed output, not a guarantee of future stability or all FG modes.

## Rollout and validation

Build and distribute matching x64/x86 hook binaries with the managed host. The tracked prebuilt
payloads are required when the optional OSD submodule is not checked out.

Deploy the updated UpdateServer and its Caddy matcher before distributing the new client. The
server continues accepting schema 1 at `/api/v1/overlay-reports`; legacy serialized fields are
unchanged for retry idempotency. Schema-2 fields are allowlisted and bounded. Old queued reports
continue using v1; a new report sent to a server without v2 receives 404/405 and is retained under
the normal retry/expiry policy instead of being discarded by v1's strict unknown-field rejection.
No automatic downgrade silently removes diagnostic evidence.

Regression coverage: `HookRenderProgressTest`, `HookCompatibilityProbeSessionTest`,
`HookProfileReportServiceTest`, native `cfx_hook_status_test` on x64/x86, and
`CapFrameX.UpdateServer.Tests`. Real-game pause, resize and FG-switch testing remains necessary
when promoting individual game profiles.

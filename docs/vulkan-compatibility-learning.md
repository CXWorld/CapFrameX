# Vulkan compatibility learning

Vulkan uses a separate learner/channel from the DXGI injection ladder. It does not publish
DXGI routing flags and does not force an interop handoff while Vulkan is presenting.

## Search space

The layer advertises supported native composite routes for the current present queue and
swapchain. Graphics requires a graphics queue and color-attachment image usage. Compute requires
a compute queue, transfer source/destination image usage and a supported copy/storage format.
HDR10 advertises only the color-correct compute route. Queue capability requirements are never
overridden by a learned profile.

The host starts with a previously verified route when available; otherwise graphics precedes
compute. It verifies **new composited presents** over at least two seconds, including a growing
success counter and acknowledgement of the exact requested revision. Present heartbeat alone,
old counters, stale/torn status, dormant overlays and minimized applications cannot learn success.
An active route that fails for five seconds advances to the remaining supported route. Exhaustion
suspends native compositing for this context and requests the existing hook-free fallback.

Resources are cached per queue family **and composite route** until normal swapchain retirement;
live changes do not destroy previously submitted GPU work. Vulkan renderer-arbitration safety
continues to apply, including while native compositing is suspended. Without an active learner,
the existing failure/fallback policy is retained. An explicit, context-matching native probe can
re-arm a previous failure, but cannot evict a DXGI renderer that already owns its present lease.

## Persistence and revalidation

Entries use the existing `HookCompatibilityProfiles.learned.json` store and reset UI. Their
`vulkan-v1:` evidence signature includes architecture, GPU vendor/device, driver, queue family
and flags, format, color space, image usage and supported routes. The running Vulkan layer
reports its own source-derived build identity, independent of the staged DXGI hook binary.

Every launch and every swapchain generation/context change verifies fresh output. Already learned
routes remain monitored, including failures without a module-signature change. Failed attempts
are recorded, but a new swapchain/process can retry; no transient error permanently blacklists
a Vulkan title. Profile reset and disabling automatic compatibility release the current probe.

## Channel

`Local\CfxOsdVulkanCompatibilityV1_<pid>` is 192 bytes of aligned 32-bit words. Header words
0..3 are magic `0x31564C43`, version 1, target PID and size. Host command words 4..15 and native
status words 16..47 have independent publication/commit counters. Writers announce a publication
before changing payload and commit last; readers reject incomplete or changing publications.
64-bit values are split low/high, protected by these sequence locks on both architectures.

Host heartbeats retain a request revision. Requests apply only to their exact evidence context
and expire after 3.5 seconds without a heartbeat, restoring native automatic selection. Status
contains requested/actual routes, revision, context, generation, build, timestamp and successful
composited-present count. The count resets when context, generation or request revision changes.

Legacy Vulkan layers without this channel keep their existing status/rendering behavior; no
profile is learned from their less detailed heartbeat. Missing layer registration remains a
deployment issue, not something the learner attempts to repair.

## Verification

- Managed regression tests: `HookVulkanProbeTest` in `source/CapFrameX.Test`.
- Native CTests: `vulkan_compatibility_policy_channel` and `renderer_arbiter` in each
  `vk_layer` build tree.
- Real GPU regressions: sibling `CapFrameX.Test/tools/verify-osd-vulkan-learning.ps1`.

The current scope is native Graphics/Compute selection. Automatic DXGI interop selection and
real Vulkan frame-generation provider-switch coverage are not implemented by this change.

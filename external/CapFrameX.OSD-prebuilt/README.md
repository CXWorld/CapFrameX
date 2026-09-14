# CapFrameX.OSD prebuilt binaries

Fallback binaries for the CapFrameX in-game OSD, built from the private
[CXWorld/CapFrameX.OSD](https://github.com/CXWorld/CapFrameX.OSD) repository.

The build uses these automatically when the `external/CapFrameX.OSD` submodule is not
checked out (developers without access to the private repo). With the submodule present,
the OSD is built from source instead and these files are ignored.

## Build provenance

The current managed bridge was built in `Release` with the VS 2026/v145 toolset from private
CapFrameX.OSD revision `e907ea965fb28cb56d82f59064a58579329c6568` (per-entry text scales; it
also carries the hook-free stall diagnostics of `a2b5bb83` and the replay pacing fix of
`2da4f0a6`). Its sources are unchanged in the native revision below.

The core, x64/x86 hook pair, and x64/x86 Vulkan layer pair were rebuilt on 2026-09-13 in
`RelWithDebInfo` from private OSD revision `573dbe28d0fc607d782ce5f2d3a1fea9575f65d7`.
Every native tree was configured with `-G "Visual Studio 18 2026"` (toolset v145) and built
with `--clean-first`. This revision adds native Vulkan compatibility probing and retains the
DXGI compatibility-channel snapshot fix and live routing updates from `ffd1609`.

The Vulkan builds used the locally installed Vulkan SDK `1.4.335.0` headers and its bundled
glslangValidator `16.0.0`. Both `hook_poc` and `vk_layer` compile the core sources into
themselves, so all five DLLs were rebuilt together. All 41 native CTest cases passed: seven
core tests, 15 hook tests per architecture, and two Vulkan tests per architecture. The copied
payloads were checked against their build outputs with SHA-256, and their PE architectures
and identical, manifest-relative Vulkan manifests were verified.

The hook pair was subsequently rebuilt on the same date from the source state now committed
as private OSD revision `f8fd7f7114d77df0282f07f3a6cfa4d5f04ecc9c`, containing the
Dying Light FG queue-capture corrections in `hook_poc`. Factory proxies are unwrapped, and
each factory/ResizeBuffers1 callback is classified before hooking: only a callback in the
Windows system DXGI image can establish a native presentation queue. Streamline can patch
the native factory's shared vtable directly, so COM identity alone does not establish this.
DXGI private data retains proven queues per swapchain and updates them after native
`ResizeBuffers1`. An interposed factory argument cannot overwrite an inner native binding;
without such a binding, native OSD drawing is suspended and observed-queue fallback is blocked.
The hook also intercepts the underlying native DXGI methods when Streamline has replaced
their shared-vtable entries before attachment. It resolves those methods from the matching
system DLL's PE data (validated headers, sections, pointer relocations and executable targets),
then hooks their live addresses with separate trampolines. The file is never executed, and
there are no version-specific offsets. The inner capture records the queue actually passed
to DXGI; the outer callback preserves that binding after its vendor work completes.
Device comparisons and captured application queues resolve Streamline's native interfaces.
Interposed factory calls suppress OSD work while releasing the lifecycle lock so that a
runtime waiting for another thread's Present can complete.
DXGI bootstrap now creates a WARP software device/swapchain to discover native method
addresses. It never opens a hardware D3D11 device or falls back to one: the startup crash
dump showed the early hook worker entering EOS's D3D11 wrapper and NVIDIA's hardware-device
initialization before an access violation. Hardware game swapchains still use the shared
native DXGI methods discovered through WARP.
All 38 hook CTests passed (19 per architecture), including WARP queue-binding regressions,
native factory/resize capture, and a real-hook integration test with a patched native factory
vtable that substitutes another same-device queue and waits for a presenter thread. The
queue-substitution regression now requires the substituted inner queue. It fails with the
preceding hook build `89761881` (exit 18) and passes with the current DLLs. PE validation also
covers mismatched/truncated images, foreign slots and non-executable/out-of-image targets;
both ASLR header adjustment and the x86 vtable's executable-section layout are exercised.
An additional test intercepts the real D3D11 export, rejects hardware bootstrap calls, and
requires software bootstrap to install the hooks. It fails against hook build `2e0d7b9d`
(one hardware call, no software call) and passes with the current DLLs.
Queue-state value 5 now distinguishes an unproven replacement binding from initial queue
discovery. The managed probe keeps a recoverable hook-free fallback instead of scheduling
early injection for this condition or for queue loss after confirmed rendering. While hidden,
the generic native route continues checking its current swapchain's proven binding, without
submitting GPU work. A fresh heartbeat and explicit queue allow a retry; new submissions
must confirm recovery. Dying Light in-game verification on 2026-09-13 (PID 3648,
15:14-15:17 local time) confirmed DLSS FG -> off -> FSR FG -> DLSS FG. The hook retained
the inner DXGI queue when it differed from the Streamline argument, resumed OSD submission
251-371 ms after queue capture, and logged 30,960/30,960 generic-route submissions with
zero misses. No restart or HookFree fallback was recorded; the hidden-fallback recovery
branch was not exercised by this game run. A separate late-host-attachment run timed out
and advanced the compatibility stage while host metrics were still empty; this needs a
separate probing review.

- managed bridge SHA-256: `615838E43AADFEBEB009B17DA3ABBC53A0B47BE1969F5CD75D2BF96B04C21DA9`
- core SHA-256: `0881FA4119CD2100132B8D75463AFC46C4A1E14521F1116FB33EF8E706953E6E`
- hook x64 SHA-256: `899ED06543634A060B95D44B7808E07B33EC8C4F32A73BC3D200F0C896B0491A`
- hook x86 SHA-256: `FF6A5FC7091C9CA38F10AB3D07BDC318B0AB6987CBA444B568613EEC5F77B59F`
- Vulkan x64 SHA-256: `5C24161417EFC03B770A346F253BEEFE833B3A033B6867DC761375580CFB3BD3`
- Vulkan x86 SHA-256: `20860D4016C0B9A3097B900FA674C61CC05B87216E35961145F0863F3C31521B`

## Contents

- `net9.0-windows/` — `CapFrameX.OSD.Interop.dll` (managed P/Invoke bridge, x64)
- `native/cfx_osd_core.dll` — native renderer (x64, RelWithDebInfo)
- `native/cfx_osd_hook.dll` — x64 DXGI hook, including exact swapchain capture through
  Streamline's factory methods, FidelityFX frame-generation creation APIs, and XeSS-FG's public
  `GetSwapChainPtr` API, with RTTI fallback. `IDXGISwapChain::Release` is never hooked. Instead,
  each concrete swapchain has a generation-qualified DXGI private-data sentinel that only queues
  destruction notifications; renderer cleanup runs later outside the COM destruction callstack.
  Every native/vendor `ResizeBuffers*`, FidelityFX replacement/destruction, and generic DXGI
  `CreateSwapChain*` boundary waits for submitted overlay work and releases all overlay backbuffer
  references. Native DXGI calls retain lifecycle exclusion across the original call. Vendor proxy
  resizes and interposed factory calls keep an external-mutation guard active while dropping the lifecycle
  lock: real Presents can satisfy a runtime rendezvous, but they skip OSD work until the mutation
  and lifetime-generation update complete.
  Proxy rendering is bound to the application queue supplied during initialization and rejects
  queues whose native D3D12 device does not own the swapchain. The generic D3D12 route uses the
  exact factory/resize queue when the captured callback belongs to system DXGI. An interposed
  callback's application queue does not prove which queue its native output swapchain uses;
  without an inner native binding, the generic route declines drawing until a native capture
  establishes one. DXGI owns this binding, so replacing the swapchain cannot carry a stale
  queue into the next object. Late attachment to
  an uncaptured swapchain may still use an observed DIRECT queue. The route retires resources
  when its selected queue changes and waits for a bounded, buffer-count-sized run of subsequent
  Presents before rebuilding and submitting on the replacement queue. It retains FidelityFX
  creation/destruction hooks as swapchain-lifecycle boundaries even while other vendor presentation
  and status hooks are disabled, unless a compatibility profile explicitly keeps the generic native
  route authoritative across FidelityFX transitions. FidelityFX module basenames and caller
  identities are activation-neutral, so a late attach that missed creation may use an eligible
  native DXGI Present with an observed, device-matched DIRECT queue. Once a captured FidelityFX
  replacement swapchain exists, its proxy Present and API-provided queue are exclusive;
  independently driven native output Presents are suppressed. Frame-generation telemetry is a
  separately compiled provider-control component with no DXGI, D3D, queue, or renderer
  dependency. It observes
  Streamline DLSS-FG, FidelityFX FSR-FG, and XeSS-FG through their explicit control/status APIs;
  Streamline late attach resolves the two documented DLSS-FG entry points and removes those hooks
  at the documented feature-unload boundary. Compatibility profiles can therefore disable every
  optional vendor factory, Present, Resize, or swapchain-lifecycle hook without suppressing
  telemetry. V1 compatibility bits 0 and 4 are reserved and ignored: avoiding the shared Release
  hook and intercepting generic DXGI factory lifecycle boundaries are now universal invariants,
  not per-title switches.
  The hook publishes status block **version 2** (`Local\CfxOsdHookStatusV1_<pid>`, 128 bytes; the
  first 64 bytes are the unchanged V1 layout): the InstallHooks phase plus a FidelityFX
  module/export detail, the compatibility flags it applied and the channel version they came
  from, present coverage of the profiled route, packed frame-generation telemetry, the D3D12
  queue state, the last decline reason and the route source of the most recent present.
  Compatibility flags arrive through channel **version 2** (`Local\CfxOsdHookCompatibilityV2_<pid>`,
  64 bytes, publication marker at byte 44 written first and sequence counter committed last;
  matching, stable counters protect the payload; older V2 hosts with a zero marker remain
  readable but require an upgrade for this guarantee; the 16-byte V1 mapping is still honoured
  when no V2 mapping exists). The hook keeps the V2 view mapped and polls it from the Present
  path every 250 ms: the XeSS-FG queue-route bit flips both ways, while the generic D3D12 route
  and the FidelityFX lifecycle switch only ever turn on. Turning the generic route on makes the
  Streamline/XeSS-FG proxy detours pass straight through; turning the FidelityFX lifecycle
  switch on revokes a captured replacement swapchain's presentation ownership and makes its
  proxy Present pass through as well, so a frame-generation change inside the running game no
  longer costs the overlay. Both rebuild the renderer on the native route. Neither can be taken
  back in process, because a hook that was never armed cannot be armed afterwards and a released
  ownership claim cannot be reconstructed; requesting that is reported as `pendingRestartFlags`.
  `liveReloadCapabilities` is therefore `0xE`. `CFX_HOOK_TEST_HANG_PHASE=<phase>` (test seam)
  stalls the install at that phase for 60 s so a host can be tested against a hung install.
- `native/x86/` — x86 DXGI hook. The hook DLL alone: `HookInjector` resolves the target's 32-bit
  `LoadLibraryW` from the x64 app, so no separate 32-bit injector is shipped
- `native/MinHook.LICENSE.txt` — BSD license for MinHook, statically linked into both DXGI hooks
- `native/vk/` — x64 Vulkan implicit layer + versioned loader manifest
- `native/vk/x86/` — the same pair for 32-bit Vulkan games

Both manifests are byte-identical; only their folder decides which DLL the loader picks up,
because `library_path` inside them is relative. Keep each DLL next to its manifest.

The WiX installer registers each manifest in its **own** registry view under
`HKLM\SOFTWARE\Khronos\Vulkan\ImplicitLayers` — the x64 one in the 64-bit view, the x86 one in
WOW6432Node. That split is load-bearing, not cosmetic: the Vulkan loader identifies a layer by
the name inside the manifest, so a manifest reachable by processes that cannot load its DLL
shadows the correct registration and disables the layer for that bitness. Portable builds stage
the files but do not change the registry.

## Updating (requires access to the private repo)

```powershell
# From the private CapFrameX.OSD repository root
dotnet build .\CapFrameX.OSD.sln -c Release

Push-Location .\CapFrameX.OSD
cmake --preset vs2026
cmake --build build --config RelWithDebInfo

cmake -S .\hook_poc -B .\hook_poc\build -G "Visual Studio 18 2026" -A x64
cmake --build .\hook_poc\build --config RelWithDebInfo
ctest --test-dir .\hook_poc\build -C RelWithDebInfo --output-on-failure

cmake -S .\hook_poc -B .\hook_poc\build-x86 -G "Visual Studio 18 2026" -A Win32
cmake --build .\hook_poc\build-x86 --config RelWithDebInfo
ctest --test-dir .\hook_poc\build-x86 -C RelWithDebInfo --output-on-failure

cmake -S .\vk_layer -B .\vk_layer\build -G "Visual Studio 18 2026" -A x64
cmake --build .\vk_layer\build --config RelWithDebInfo
cmake -S .\vk_layer -B .\vk_layer\build-x86 -G "Visual Studio 18 2026" -A Win32
cmake --build .\vk_layer\build-x86 --config RelWithDebInfo
Pop-Location
```

Then copy the managed bridge, core, x64/x86 hooks, and both Vulkan layers/manifests from their
`RelWithDebInfo` outputs into the matching folders above, and bump the submodule to the matching
commit. The Vulkan manifest is renamed on the way in: the build emits `cfx_osd_vklayer.json`,
this tree keeps the versioned `cfx_osd_vklayer_v1.json`.

Copy those files one by one, never a whole `RelWithDebInfo` folder: the build outputs also
hold the ctest executables and `cfx_inject.exe`. That injector was deliberately dropped when
`HookInjector` learned to resolve the target's 32-bit `LoadLibraryW` itself, and a bulk copy
is how it silently reappeared under `native/x86/` once already.

Configure every native tree with the **same** toolset the core preset pins (`-G "Visual Studio 18
2026"`, toolset v145, matching the `.vcxproj` projects in this repo). `hook_poc` and `vk_layer`
take no preset, so passing `-G` explicitly is what keeps them in step: core, hook and layer all end
up loaded in the same game process, and a silent toolset split between them is hard to spot.

Rebuild **all** native trees, not just the core: `hook_poc` and `vk_layer` compile the core sources
into themselves (`${CFX_OSD_CORE_SRC}`), so a core change that is not followed by a rebuild of
those two leaves them silently behind.

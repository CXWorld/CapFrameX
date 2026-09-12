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
`2da4f0a6`). The core, x64/x86 hook pair, and x64/x86 Vulkan layer pair were rebuilt on
2026-09-09 in `RelWithDebInfo` from the same revision, every native tree configured with
`-G "Visual Studio 18 2026"` (toolset v145).

The x64/x86 hook pair was rebuilt again on 2026-09-12 from that revision **plus the not yet
committed status-block V2 and compatibility-channel V2 work in the OSD repository's working
tree** (InstallHooks phases, present coverage, frame-generation telemetry and decline reasons in
the status block; the `Rendered` bit re-publishes after a stand-down; FidelityFX exports are
resolved before the install lock is taken, forwarded exports are skipped, and a fault inside that
arming no longer takes the DXGI Present hook down; the hook keeps the 64-byte
`Local\CfxOsdHookCompatibilityV2_{pid}` channel mapped, polls its sequence counter every 250 ms
from the Present path and applies the XeSS-FG queue-route, the generic-route and the FidelityFX
lifecycle bits live — the two routing bits only ever turn on — and advertises exactly that in
`liveReloadCapabilities`). The core and both Vulkan layers are byte-identical to the 2026-09-09
build. Bump this paragraph's revision once that work is committed.

The Vulkan trees are unchanged since the 2026-09-07 build and still pin Khronos Vulkan-Headers
`vulkan-sdk-1.4.357.0` (commit `e3b1eec08173d6b825cd3ac88c885a63b621504a`) and glslang `16.5.0`
from the official `main-tot` Windows x64 release archive, SHA-256
`6BA807EF1D697EC66A34D9D666F842F863FFF4F5612EE95C1CC88F5DE5A362C2`; the layer binaries change
with the core because `hook_poc` and `vk_layer` compile the core sources into themselves.

- managed bridge SHA-256: `615838E43AADFEBEB009B17DA3ABBC53A0B47BE1969F5CD75D2BF96B04C21DA9`
- core SHA-256: `F851659EF7F8A41154E39B2CB1446BD8E8C5B3DBA2A8471F29573A9DC84896B6`
- hook x64 SHA-256: `E071B20EEBA95884048FEBD13464788F064F6CCE2A4272896A1DA4C2E8D6773A`
- hook x86 SHA-256: `C7E8BB15BCC278C1BA483F2A11BC82D1AD7A7337CE3CA21F6D1DBBE5D3C42E36`
- Vulkan x64 SHA-256: `654B8750E132F17962A72A3947BE07EA4D3A9F596D8C92647F7E3B53C578646E`
- Vulkan x86 SHA-256: `4F71DCDCF9DC5580BD7980785E3CB73D7907C52AE0D439E0A2DAE4379B16ABE6`

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
  resizes instead keep an external-mutation guard active while temporarily dropping the lifecycle
  lock: real Presents can satisfy a runtime rendezvous, but they skip OSD work until the mutation
  and lifetime-generation update complete.
  Proxy rendering is bound to the application queue supplied during initialization and rejects
  queues whose D3D12 device does not own the swapchain. The generic D3D12 route retires resources
  when its observed queue changes and waits for a bounded, buffer-count-sized run of subsequent
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
  64 bytes, sequence counter bumped last by the host; the 16-byte V1 mapping is still honoured
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

# CapFrameX.OSD prebuilt binaries

Fallback binaries for the CapFrameX in-game OSD, built from the private
[CXWorld/CapFrameX.OSD](https://github.com/CXWorld/CapFrameX.OSD) repository.

The build uses these automatically when the `external/CapFrameX.OSD` submodule is not
checked out (developers without access to the private repo). With the submodule present,
the OSD is built from source instead and these files are ignored.

## Build provenance

The current managed bridge was built in `Release` with the VS 2026/v145 toolset from private
CapFrameX.OSD revision `a2b5bb831990b477485c8b8dc26a0cfa7cf0c464` (hook-free stall diagnostics:
`src/core/Diagnostics.h`, timestamped `[diag]` log lines, `cfx_osd_set_verbose_log`,
`OsdHost.Diagnostic`). The x64/x86 hook pair and x64/x86 Vulkan layer pair were rebuilt on
2026-09-07 in `RelWithDebInfo` from the same revision, every native tree configured with
`-G "Visual Studio 18 2026" -T v145` (MSVC 19.51.36256, Windows SDK 10.0.26100.0). The core was
rebuilt later that day from that revision plus the hook-free pacing fix (`ConsumeRepaintSlots` in
`src/core/RenderPacing.h`: `OsdInstance::tick` handed the replay clock the whole accumulator while
also retaining the sub-slot remainder, so the clock ran at 1.0-2.0x wall time, drained its
cushion and froze the chart in a rebuffering hold every few seconds) — OSD revision
`2da4f0a695954d0d3bd72108f133a833bd464c5d`. The fix compiles into the hook and layer trees
(verified) but only affects the hook-free window, so those were not reshipped.

The Vulkan build used Khronos Vulkan-Headers `vulkan-sdk-1.4.357.0` (commit
`e3b1eec08173d6b825cd3ac88c885a63b621504a`) and glslang `16.5.0` from the official
`main-tot` Windows x64 release archive, SHA-256
`6BA807EF1D697EC66A34D9D666F842F863FFF4F5612EE95C1CC88F5DE5A362C2`.
All 37 native CTest cases passed (7 core, 15 per hook architecture). PE architectures,
preservation of existing DLL exports, and identical Vulkan manifests were verified.

- managed bridge SHA-256: `0268C5543F99E730CA67179A4F0F3662954186036BE5AA2DDF294F2DF881DD94`
- core SHA-256: `EE483C91D249F64AD069C84DD0952DF66A6D62BD62968CBC4D57439BE504255F`
- hook x64 SHA-256: `A90F5116F115B47478A33707C92A496EC9FBA0ABA525C9239FFA16DE5E714051`
- hook x86 SHA-256: `F8A7279A7832647826CE2DD2F861D80102BCE1CB083AAA87C502803FC4CA4AAC`
- Vulkan x64 SHA-256: `CC1B2134651B706E38ECA29A7832B6A8A62FED8E329B8F819C44D35BC06A83AA`
- Vulkan x86 SHA-256: `082286190E70E04EC522FC89E0551147E80A9E738A0FBA84216F868C0BB174DB`

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

Configure every native tree with the **same** toolset the core preset pins (`-G "Visual Studio 18
2026"`, toolset v145, matching the `.vcxproj` projects in this repo). `hook_poc` and `vk_layer`
take no preset, so passing `-G` explicitly is what keeps them in step: core, hook and layer all end
up loaded in the same game process, and a silent toolset split between them is hard to spot.

Rebuild **all** native trees, not just the core: `hook_poc` and `vk_layer` compile the core sources
into themselves (`${CFX_OSD_CORE_SRC}`), so a core change that is not followed by a rebuild of
those two leaves them silently behind.

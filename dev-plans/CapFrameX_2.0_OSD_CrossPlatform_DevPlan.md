# CapFrameX.OSD on Windows and Linux - Development Plan

Last updated: 2026-09-20

> **Requirement:** the code of `CapFrameX.OSD` is carried over into 2.0 as far as possible. In
> particular the **Vulkan layer** work (composite pipeline, swap-chain/queue handling, HDR formats,
> the compatibility and activation fixes) must be **reused for the Linux version**. Where necessary,
> `CapFrameX.OSD` is split into a Windows and a Linux part.
>
> This plan replaces the Linux approach of the overlay plan (its section 9 assumed a new ImGui
> renderer inside the old `capframex-linux` layer) and the "layer" parts of the Linux service plan.
> It concerns the repository `CXWorld/CapFrameX.OSD` (local: `..\CapFrameX.OSD`); a copy of this
> plan belongs into that repository's `dev plans/` folder.

## 1. Finding: most of it is already portable

Survey of `CapFrameX.OSD/CapFrameX.OSD` (main, `4e362b1`, 2026-09-20):

| Part | Size | Platform coupling | Verdict |
|---|---|---|---|
| `vk_layer/src/vk_compositor.cpp` | 1,597 lines | pure Vulkan: staging upload, graphics + compute composite pipelines, per-swap-chain state, semaphores/command pools, format variants (`composite.comp`, `composite_rgb10a2.comp`, `composite_rgba16f.comp`); one call into the renderer arbiter | **portable as is** - this is the hard-won part and it has no Win32 in it |
| `vk_layer/src/layer.cpp` | 457 lines | loader/dispatch-chain code; `VK_USE_PLATFORM_WIN32_KHR` defined in `vk_layer_priv.h` | portable; platform define becomes a build option |
| `vk_layer/shaders/*` | 5 shaders | GLSL -> SPIR-V | portable |
| `vk_layer/src/osd_feed.cpp` | 488 lines | reads CapFrameX data through named file mappings (`OpenFileMapping`) | logic portable, transport not (section 4.2) |
| `vk_layer/src/vk_compatibility.*`, `layer_log.cpp` | ~220 lines | file mapping for the status block, `GetModuleFileName` | small platform shims |
| shared hook modules used by the layer: `frame_ring`, `overlay_metrics_shm`, `overlay_placement_shm`, `overlay_frametime_shm`, `present_stats`, `renderer_arbiter` | small | 1-4 Win32 references each (mappings, events) | portable behind the IPC shim |
| core **scene model**: `Scene`, `Widget` (layout/measure/arrange), `Config` (JSON templates), `Animator`, `MetricsSource`, `ReplayBuffer`, `RenderPacing`, `ChartGeometry`, `ScrollChart`, `Common` | ~2,500 lines | `Scene`, `Animator`, `MetricsSource`, `ReplayBuffer`, `RenderPacing`: **zero** Direct2D/Win32 references; `Widget.cpp`: 20, all through the `Renderer` class; `Config.cpp`: string conversion | portable |
| core **`Renderer`** | API: `fillRect`, `fillRoundedRect`, `drawRoundedRect`, `drawLine`, `arc`, text, opacity layers | implemented on `ID2D1RenderTarget` + DirectWrite (56 references) | **the porting seam** (section 4.1) - the abstraction already exists, only its implementation is Direct2D |
| core **`CpuTextRaster`** + `cfx_osd_create_cpu` (CPU frame producer) | Direct2D *software* rasteriser onto a WIC bitmap -> premultiplied BGRA -> callback | Windows-only implementation of a platform-neutral idea | the Vulkan layer already consumes exactly this: a CPU-produced BGRA frame, no D3D device involved. Linux needs a different rasteriser behind the same callback |
| `GraphicsDevice`, `OverlayWindow`, `ChartLayer` (raw D3D11), `MpoDiag`, `PreviewInstance`, `ColorManagement` | | D3D11, DXGI, DirectComposition, HWND | **Windows-only** - the hook-free desktop window and the D3D11 paths |
| `hook_poc/` (DXGI hook, MinHook, XeSS-FG/Streamline/FidelityFX routing) | | D3D11/D3D12 | **Windows-only** |
| `CapFrameX.OSD.Interop`, `.Controls`, `.Editor.Demo` | C#/WPF | | Windows-only; the WPF editor is superseded by the Angular editor |

Why this works out: the in-game paths were deliberately moved to a **CPU raster on a background
thread** (the comment in `CpuTextRaster.h` documents the measurement behind it - a second GPU
device delayed about 1 in 100 presents by ~1 ms). The Vulkan layer therefore never touched
Direct3D; it receives pixels and composites them with its own Vulkan pipelines. On Linux the same
layer needs the same pixels from a rasteriser that is not Direct2D.

One more consequence: under **Proton** the game is a Windows binary, but its Vulkan calls (DXVK,
VKD3D-Proton, native) leave Wine through `winevulkan` into the **Linux** Vulkan loader. The layer
that sees them is the Linux layer. So the Linux build of this layer serves native *and* Proton
titles, including D3D9-12 games - far more than the Vulkan-only share it has on Windows.

## 2. Decision: one repository, split by platform inside it - not two repositories

A fork into `CapFrameX.OSD.Windows` and `CapFrameX.OSD.Linux` would duplicate exactly the files
whose fixes were expensive (`vk_compositor.cpp`, the dispatch code, the compatibility logic, the
scene model). Every future swap-chain or queue-family fix would have to be made twice. The split
happens **inside** the repository, mirroring the folder convention of the main repository:

```
CapFrameX.OSD/
|-- shared/                                   builds with MSVC, GCC, Clang; no OS headers
|   |-- include/cfx/osd/                      public C ABI, shared-data contracts (layouts only)
|   |-- core/                                 Scene, Widget, Config, Animator, MetricsSource, ReplayBuffer,
|   |                                         RenderPacing, ChartGeometry, ScrollChart, Common,
|   |                                         Renderer (interface) + Canvas abstraction (4.1),
|   |                                         CpuFrameProducer (platform-neutral part of OsdInstance)
|   |-- vk_layer/                             layer.cpp, vk_compositor.cpp, vk_compatibility (logic),
|   |                                         osd_feed (logic), shaders/, capture/ (section 5)
|   |-- ipc/                                  frame_ring, metrics/placement/frametime blocks, renderer arbiter,
|   |                                         present_stats - data layouts + logic, on the platform shim (4.2)
|   |-- platform/                             interface only: SharedRegion, Signal, Clock, Log, ModulePath
|   |-- templates/                            JSON presets (overlay plan 4.2)
|   `-- third_party/                          nlohmann json, (portable rasteriser + font stack, 4.1)
|-- windows/
|   |-- platform/                             Win32 implementation of shared/platform
|   |-- raster_d2d/                           Direct2D/DirectWrite/WIC canvas (today's Renderer.cpp, CpuTextRaster)
|   |-- desktop/                              GraphicsDevice, OverlayWindow, ChartLayer (D3D11), MpoDiag,
|   |                                         ColorManagement, PreviewInstance, OsdInstance (window paths), demo host
|   |-- hook/                                 today's hook_poc (DXGI hook, MinHook, FG routing)
|   |-- vk_layer/                             Win32 glue: manifest, x64/x86 builds, register_layer.cmd (HKLM only)
|   `-- managed/                              CapFrameX.OSD.Interop, .Controls, .Editor.Demo
|-- linux/
|   |-- platform/                             POSIX implementation: memfd + Unix socket, eventfd/futex, clock, log
|   |-- raster/                               portable canvas backend wiring (4.1)
|   |-- vk_layer/                             Linux glue: manifest(s), x86_64 + i386 builds, install rules
|   `-- tools/                                headless preview renderer, test host
|-- tests/                                    golden images, layout tests, vk_compatibility_test, IPC layout tests
`-- CMakeLists.txt + CMakePresets.json        presets: vs2026-x64, vs2026-x86, linux-gcc-x86_64, linux-gcc-i386, linux-clang
```

Rules:
- `shared/` includes no `<windows.h>`, no POSIX headers, no Direct2D. It compiles on both CI runners
  on every commit - that is what keeps it shared.
- The restructuring is done with `git mv` in mechanical commits without content changes first;
  behaviour-preserving refactors (extracting the platform shim, the canvas interface) follow as
  separate commits. **The Windows binaries must stay byte-for-byte equivalent in behaviour**: they
  are production code in 1.9.1 and the most compatibility-sensitive code CapFrameX ships.
- The main repository keeps consuming the OSD as the optional submodule `external/CapFrameX.OSD`
  with prebuilt fallback (`external/CapFrameX.OSD-prebuilt/`); the prebuilt folder gains a
  `linux/` subtree (layer `.so` x86_64 + i386, manifests). The services consume the OSD through the
  `IOverlayBackend` and `IFrameSource` ports of `CapFrameX.Service.Shared`; the data layouts of
  `shared/ipc` are the contract between the OSD repository and the two service folders.
  `CapFrameX.Service.Linux/native/layer`
  from the architecture plan becomes a thin CMake wrapper that builds `linux/vk_layer` from the
  submodule or stages the prebuilt binaries - same pattern as on Windows.

## 3. What is reused, what is Windows-only, what is new

| | Windows | Linux |
|---|---|---|
| Scene model, widgets, layout, JSON templates, animation, replay/pacing | shared | shared |
| Vulkan layer: dispatch, compositor, shaders, compatibility logic, feed logic | shared | shared |
| 2D rasteriser | Direct2D/DirectWrite (unchanged) | portable CPU rasteriser (**new**, 4.1) |
| IPC / synchronisation | named file mappings + events (unchanged) | memfd + Unix socket + eventfd (**new**, 4.2) |
| Present capture inside the layer | not needed (PresentMon) | **ported in** from `capframex-linux/src/layer` (section 5) |
| Hook-free desktop overlay window (DirectComposition) | yes | **no equivalent planned** - Wayland offers no portable always-on-top overlay surface; the in-game layer is the Linux delivery path (gamescope is examined in X5) |
| DXGI hook (D3D11/D3D12) | yes | not needed - DXVK/VKD3D-Proton titles arrive as Vulkan |
| OpenGL titles | DXGI/GL not covered today either | not covered in 2.0 |
| Editor preview | today HWND child window; 2.0: offscreen via the CPU frame producer | offscreen via the CPU frame producer - **same code path** (section 6) |
| Managed interop / WPF controls | 1.9.x editor | none - the Angular editor talks to the service |

## 4. The two porting seams

### 4.1 Canvas: a portable rasteriser behind `Renderer`
`Renderer` already exposes what widgets need. Step one is to make that explicit: a `Canvas`
interface (rects, rounded rects, lines, arcs, paths for the new widgets of the overlay plan G4-G6,
text measure/draw with font family/size/weight, clip, opacity layer, solid/alpha colours), with
`Renderer` and all widgets using only `Canvas`. Direct2D becomes `windows/raster_d2d`. Text is the
demanding part: widgets measure text for layout (value-column alignment was tuned recently -
`4e362b1 Align overlay value columns`), so metrics must come from the same backend that draws.

Backend candidates for Linux - all statically linked into the layer, because the layer lives inside
arbitrary game processes and must not depend on (or clash with) system libraries:

| Candidate | Licence | Assessment |
|---|---|---|
| **Blend2D** (+ its built-in OpenType text, FreeType not required) | zlib | fast JIT-based CPU rasteriser, rich path/stroke API, small; text shaping is basic (no HarfBuzz) - sufficient for Latin UI strings and digits, which is what an OSD draws. **First choice for the spike.** |
| ThorVG (SW engine) | MIT | small, designed for embedded UI, TTF loader included; less mature text metrics |
| Skia | BSD | complete, but huge for a DLL/SO injected into every Vulkan process |
| Cairo + FreeType + HarfBuzz | LGPL/MPL/MIT | proven quality, but larger, LGPL static-linking obligations, slower rasteriser |
| Dear ImGui (the old Linux plan) | MIT | an immediate-mode GPU UI toolkit - would bypass scene model, widgets and compositor, i.e. discard the code this requirement says to keep. Rejected. |

Spike **X1** (about a week): implement `Canvas` on Blend2D, render the default template and the
`CX Benchmark` mock-up through the CPU frame producer on Linux *and* on Windows, compare against the
Direct2D output (layout identical, glyph edges within tolerance), measure raster time per frame for
a full-size overlay at 4K scale. Pass -> Blend2D; fail -> ThorVG, then Cairo.

Follow-up question, decided after X1 with numbers, not now: should the **Windows in-game paths**
(hook + Windows Vulkan layer) later switch to the portable rasteriser as well? Pros: pixel-identical
overlays on both platforms, one text stack to tune, bundled fonts (overlay plan G10) solved once,
no COM/WIC inside game processes. Cons: a regression risk in code that is stable today. Default:
**no change on Windows** until the Linux backend has shipped and been compared.

Portability details to handle in `shared/core`: `std::wstring`/`wchar_t` is UTF-16 on Windows and
UTF-32 on Linux - move the core to UTF-8 `std::string` internally and convert at the Direct2D
boundary; `HRESULT`-returning `Renderer::end()` becomes a neutral status; `Utf8ToWide` leaves
`Config.cpp`.

### 4.2 Platform shim: shared data and signalling
Windows keeps its named mappings and events exactly as they are (names, layouts, `Local\`
namespace, the V1/V2 compatibility mappings) - the 1.9.1 managed side and the hook depend on them.

The shim abstracts only *how a region is obtained and signalled*:
`SharedRegion::open(name|fd)`, `Signal`, monotonic `Clock`, `Log`, `ModulePath`. The data layouts in
`shared/ipc` (frame ring, metrics block, placement block, frametime block, arbiter, status block)
are plain structs already; they gain `static_assert`ed sizes/offsets and an explicit version field
where missing, so a Linux service written in C# can be tested against them (generated constants, as
in the Linux service plan 3.2).

Linux transport: **one Unix socket + memfd regions passed over it** (`SCM_RIGHTS`).
- The socket lives under `$HOME` (`~/.config/capframex/` today) because Proton's container shares
  `/home` but not `/tmp` or `$XDG_RUNTIME_DIR` - a constraint the existing Linux layer already
  discovered. File descriptors passed over the socket are valid inside the container, so **no
  shared-memory name has to be visible across the sandbox boundary** - which sidesteps the
  `Local\`-style namespace problems the Windows side had to work around.
- Control messages (hello/version, target selection, profile JSON, show/hide, capture subscribe) go
  over the socket; high-rate data (metrics block, frame ring to the service) through the memfd rings
  with eventfd wake-ups.
- `SO_PEERCRED` same-uid check, versioned hello, bounded messages (Linux service plan 3.2).

## 5. One Linux layer: capture moves into the OSD layer

Today there are two unrelated Vulkan layers: the OSD layer (C++, Windows, overlay only) and
`capframex-linux/src/layer` (C, Linux, capture only). Shipping both on Linux means two implicit
layers in every game, two dispatch chains, two IPC clients and an ordering problem (the capture
layer must see the present *after* the overlay has been composited, or it times the wrong thing).

**The OSD layer is the base; capture is ported into it** as `shared/vk_layer/capture/`:
- from `timing.c` / `swapchain.c`: CPU-side present timestamps, `VK_EXT_present_timing` handling
  (actual present time, render-complete, displayed), swap-chain info events;
- `FrameDataPoint` becomes the record of the shared frame ring; the old binary socket protocol and
  `ipc_client.c` are retired with the daemon (Linux service plan L3);
- the module is compiled on Windows too but disabled by default - PresentMon is the Windows source -
  which keeps it building and makes A/B comparisons against PresentMon possible on one machine,
  a cheap way to validate the Linux capture numbers;
- layer name on Linux: `VK_LAYER_CAPFRAMEX_overlay` is kept for both platforms; the old
  `VK_LAYER_capframex_capture` manifest is removed by the package upgrade.

Idle rules (the layer loads into **every** Vulkan process): until the service selects this pid, the
present hook does one atomic load and forwards; no thread, no allocation, no socket traffic beyond
the hello; ignore-list check happens at instance creation. On-demand policy of the architecture
plan, section 8, applies inside the layer: capture timing only with a capture/frame-metric lease,
compositor resources only with an overlay lease, rasteriser thread only while visible.

## 6. Editor preview on both platforms

The CPU frame producer (`cfx_osd_create_cpu`) already turns a template + metrics into BGRA frames
without any window or GPU. A small C API addition - render one frame of a given template with
synthetic or supplied metrics into a caller buffer - gives the service the offscreen preview the
overlay plan asks for (G15, option A), **identically on Windows and Linux**, and removes the HWND
child-window preview from the 2.0 path. On Linux it is also the unit-test harness for the rasteriser
(golden images in CI without a GPU).

## 7. Phases

| Phase | Scope | Acceptance |
|---|---|---|
| **X0** Restructure | folders of section 2 via `git mv`; CMake presets; Linux CI runner builds `shared/` (compile-only at first, platform stubs); no behaviour change | Windows outputs (`cfx_osd_core.dll`, hook x64/x86, layer x64/x86) pass the existing tests and a manual smoke test in one D3D11, one D3D12 and one Vulkan title; main repo builds from the submodule unchanged |
| **X1** Canvas + rasteriser spike | `Canvas` interface, Direct2D backend = today's behaviour, Blend2D backend, UTF-8 core strings | golden-image comparison report; raster time budget met; decision recorded |
| **X2** Platform shim | `SharedRegion`/`Signal`/`Clock`/`Log`; Win32 implementation = today's behaviour; POSIX implementation; layout `static_assert`s and generated C# constants | Windows regression suite green; POSIX shim unit-tested (fd passing, wake-ups, reconnect) |
| **X3** Linux layer bring-up | `linux/vk_layer`: build, manifest, loader negotiation, compositor on Mesa (RADV, ANV) and NVIDIA proprietary; test host feeding synthetic metrics | overlay visible in `vkcube`, one native Vulkan title, one DXVK and one VKD3D-Proton title; HDR swap-chain formats verified where available; no validation-layer errors |
| **X4** Capture in the layer | section 5; frame ring to the service; `VK_EXT_present_timing` | Linux service phase L3 can drop the daemon; frametimes match the old capture layer on the same run; idle-cost rules measured |
| **X5** Parity and robustness | swap-chain recreation, multiple swap chains, queue-family cases (the Doom fix), minimise/resize, gamescope session, Steam overlay and MangoHud coexistence, 32-bit build | test matrix documented; known-incompatible list seeded |
| **X6** Overlay-plan widgets on both backends | G1-G14 of the overlay plan implemented against `Canvas`, so both rasterisers get them at once | the five presets render on both platforms within the golden-image tolerance |

Dependencies: X0 needs the `release/1.9.1` merge only on the main-repo side (submodule wiring); the
OSD repo work itself can start immediately. X3 needs a Linux machine with each GPU vendor. X4 pairs
with Linux service L1-L3. X6 replaces the platform-specific reading of overlay plan phase O2: the
widgets are written **once**, in `shared/core`.

## 8. Risks

| Risk | Mitigation |
|---|---|
| Restructuring destabilises the production Windows paths | mechanical moves first, refactors separately, Windows regression gate on every phase, no functional Windows change bundled with Linux work |
| Text quality/metrics differ between Direct2D and the portable rasteriser -> layouts shift between platforms | layout uses backend-provided metrics; bundled fonts only (no system font lookup); golden-image tolerance agreed in X1; optional later unification on Windows |
| Statically linked third-party code inside game processes (symbol clashes, size, anti-cheat heuristics) | hidden visibility, no exported symbols besides the Vulkan entry points, static libstdc++/libgcc, size budget for the `.so`, staged loading (rasteriser only when an overlay lease exists) |
| Two layers' worth of history in one (capture port) introduces timing regressions | A/B against the old C layer before it is deleted; capture module testable on Windows against PresentMon |
| Proton / pressure-vessel / Flatpak cannot reach the service | socket under `$HOME` + fd passing; Flatpak Vulkan-layer extension (Linux service plan section 8); breadcrumb file for diagnostics |
| NVIDIA proprietary driver quirks in layered present paths | X3/X5 matrix includes it from the start; the queue-family and activation-race plans in `dev plans/` are the checklist |
| gamescope composites differently (nested, its own overlay plane) | examined in X5; fall back to in-swap-chain composite, which is what the layer does anyway |

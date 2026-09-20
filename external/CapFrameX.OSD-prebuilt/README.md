# CapFrameX.OSD prebuilt binaries

Fallback binaries for the CapFrameX in-game OSD, built from the private
[CXWorld/CapFrameX.OSD](https://github.com/CXWorld/CapFrameX.OSD) repository.

The build uses these automatically when the `external/CapFrameX.OSD` submodule is not
checked out (developers without access to the private repo). With the submodule present,
the OSD is built from source instead and these files are ignored.

## Build provenance

The current five native DLLs were rebuilt on **2026-09-20** with VS 2026/v145 in
`RelWithDebInfo` from the source state now committed as private OSD revision
`4e362b1aac895ea8681752ea1ea028f3559a08ac`. This includes the row-alignment changes
described below, along with the preceding graph-source corrections and OSD log
directory changes.

These native payloads are **unsigned development builds**. The existing
`net10.0-windows` managed bridge retains its Certum signature from the rebuild below.

### Fixed overlay value columns (2026-09-20)

Fixes [CapFrameX issue #441](https://github.com/CXWorld/CapFrameX/issues/441).
All metric rows share a column grid: short rows start in the first value column,
and complete numbers align at their right edge. Each unit follows its number with
one space at the cell's font size, including when other rows use more decimal places.
Spare unit space follows the unit instead of separating it from its number.

Every column reserves the same unit width. The standard is `MT/s`, the widest built-in
sensor/metric unit in the default font (46.416 px at a 20 px font size), measured at
the actual font scale. Wider units expand that reserve across the whole grid.
Numeric column widths account for the widest actual digit and each value's precision
and font scale. Live numeric updates retain the same positions. The additional gap
between value columns is 8 px at 100% zoom and scales with zoom.

Labels and ordinary text remain left-aligned. Text-only rows such as model names
span the available value area without widening the first numeric column. Mixed
text/numeric rows preserve entry order, and live text growth expands the shared
grid without overlapping the next column or shrinking again on shorter values.

All twelve core CTests passed on x64, along with `row_alignment` on x86.
The alignment test runs 597 checks per architecture for uneven row
lengths, all built-in units, standard unit reserves, custom units, precision, font
scales, zoom, text rows and live text growth. CPU raster reproductions of the issue
and the reported GPU/CPU sensor layout were visually checked. The existing CPU
raster and external D3D11 rendering tests also passed.

All five DLLs were rebuilt, checked for PE architecture, and staged in both this
folder and the Release app output with matching SHA-256 hashes. Restart CapFrameX
and the game to load them. These DLLs supersede the builds below.

Renderer input SHA-256 at build time:

- `src/core/Widget.cpp`: `286C6CF6046B747923664E05093516FE71EDBF27AD58030B9606F42E428B1ED4`
- `src/core/Widget.h`: `9E76627ABE4BF1DA936D76E83227775615530B05D5FFAE770503D210F961FE45`

| Native DLL | SHA-256 |
| --- | --- |
| Core x64 | `C1CF40AC660AF30388B192C66CF32741A18F84AE75DB45C4B652DA46D8E8EBAE` |
| Hook x64 | `CB0E57DB144CBBF91D3D7BEC4273A1B180252EDBC55CD8AD8B9D052391CFA4C6` |
| Hook x86 | `69709E607B54521F9E38CA035C9A0C09B9E80EDECB96B190B5FDF2021B8A72E4` |
| Vulkan x64 | `7A156E099DA456EEF8D9024EC2C905F1C38F2B1C220E4DFDE2FF6C5D4FF96AF6` |
| Vulkan x86 | `A4E17AFC98F5BFA5B18AAA17E8996F49979160CC9D1ACB454007EFE6F2A29515` |

### OSD log directory (2026-09-20)

All native log writers now use `%TEMP%\cfx-osd-logs`: `cfx_osd.log`, `cfxhook.log`,
`cfx_vklayer.log`, and `cfx_present_stats.log`. A shared native helper creates the
directory when needed, validates path capacity and handles directory creation failures.
The writers use Unicode paths. The Overlay tab's **Open OSD log folder** command creates
and opens the same directory, including when extended logging is disabled. This path
also applies in portable mode. Restart CapFrameX and the game to load the updated DLLs.

All 16 existing logging CTests passed (seven hook logger tests and the present-statistics
concurrency test per architecture). A temporary smoke host using the actual core, Vulkan
and statistics writer sources verified a new Unicode directory, reuse of the directory,
safe failure when a file occupies the directory name, and rejection of paths exceeding
the buffer capacity. All five staged DLLs match their build outputs by SHA-256 and PE
architecture. These DLLs supersede the builds below.

| Native DLL | SHA-256 |
| --- | --- |
| Core x64 | `78125B6722862469123614D0BBF7D7D161D2B9460DCECF14CB466BE17095015A` |
| Hook x64 | `2C86B0E7DA887E6F24E8FCD8677FF1EFE14E16EE0BB5D48E0100445A4E0EC3C8` |
| Hook x86 | `AFDD6D4F16EF506838A4E7E8DC43579F0796896E5C9F195A52B6D21168D90455` |
| Vulkan x64 | `7C3D307A63E0F0B65AC2BD98FFE82B8CA1C898305842299100BEDDB8824C7E6D` |
| Vulkan x86 | `785E9F6F8BBE1046843A076F669A8CB544D2EC4FE2C82FACD73532C6620FFC87` |

### Local Presents and live graph-source switching (2026-09-19)

Swapchain Presents now draw through the newest sample, independent of the PresentMon
replay-buffer setting. Both the DXGI hook and Vulkan layer explicitly select the graph
source through the additive `cfx_osd_set_graph_source` API. PresentMon remains the default
for existing hosts, preserving hook-free replay behavior.

Changing the source clears frametime/display-time history, synthetic timestamps and the
delivery-cadence estimate. The render thread resets its replay clock and graph scales
when it snapshots the source generation, including quick A -> B -> A switches between
render ticks. The entry values refresh on the switch, and each recreated renderer
receives its source again. This prevents old QPC timestamps and display samples from
keeping the local synthetic timeline outside the visible graph window.

All **70 native CTest invocations** passed: eleven core tests on x64, 27 DXGI tests per
architecture, two Vulkan tests per architecture, and the new `graph_source` test on x86.
The new test runs 70 checks per architecture against the real feed/snapshot/clock code,
covering local chart geometry without prebuffering, replay settings from 500 to 10000 ms,
PresentMon replay, both switch directions, rapid/repeated switches, history retention,
invalid input and concurrent feed/render snapshots. All five staged DLLs were checked
against their build outputs by SHA-256 and PE architecture; both Vulkan manifests match
the build manifests.

| Native DLL | SHA-256 |
| --- | --- |
| Core x64 | `D4261EF8467A6D1CDA7A0E8181A59496ACC66EBCB77485666C13A6607ED5639A` |
| Hook x64 | `D8C84435A5E4F544098B2780787354EC8A015753D8AB6C3915989EA9E585491C` |
| Hook x86 | `FBC73CD0D2F5FDC144A373D2EA360D55343BE4A650B354E23178CA025E250317` |
| Vulkan x64 | `48403F665AC4EC5F272B42F188EBAE27FD09794E5DB7604795F41B2FC011628B` |
| Vulkan x86 | `45FB4941B01023389BBFCE61E13B3157883E71BA601FEC0691F5D420B3F268B8` |

### Signed .NET 10 rebuild (2026-09-19)

The preceding binaries were rebuilt on **2026-09-19** from private CapFrameX.OSD
revision `0104ab98e694ff3eac37cd3bc5319402c93b8f3f`, with the migration of
Interop, Controls and Editor.Demo to `net10.0-windows` now committed as
`142ad1d5c46cbd5688068d03a48b893e4bb3a17c`. The managed bridge uses
.NET SDK 10.0.401 in `Release`; all five native DLLs use VS 2026/v145 in
`RelWithDebInfo`, from fresh x64/x86 build directories. All 68 native tests passed.

That bridge and those five native DLLs were Authenticode-signed with the Certum code-signing
certificate `C0D5481E2ACBB9DD104A825A81EECF55557BA783` and RFC 3161 SHA-256 timestamps.
The hashes in the monitor-bounds section below describe those signed files. The former `net9.0-windows` bridge
has been replaced by `net10.0-windows`; all consuming project paths were updated.
The following sections retain the history of the native fixes in this revision.

### Hook-free monitor bounds (2026-09-18)

All five native DLLs were rebuilt with Visual Studio 2026/v145 in `RelWithDebInfo` from
the source state now committed as private OSD revision
`0104ab98e694ff3eac37cd3bc5319402c93b8f3f`, fixing
[CapFrameX issue #440](https://github.com/CXWorld/CapFrameX/issues/440). These payloads
supersede the native builds below; the managed bridge and Vulkan manifests are unchanged.

`OverlayWindow::positionFixed` now anchors every placement to the selected monitor's
`rcMonitor`, including negative virtual-desktop coordinates. The primary-display fallback
also uses the full screen dimensions and no longer queries `SPI_GETWORKAREA`. Taskbar
position and size therefore cannot offset the overlay's anchors. Margins and the native
API remain unchanged.

The new `overlay_position` regression compiles the actual placement code with simulated
Win32 monitor queries. Its 439 checks cover all five anchors, taskbars on every edge,
nonzero margins, multiple monitors, negative origins, scaled overlay dimensions, relayout,
hidden panels and monitor-query failures. It failed 54 checks against the original source
and passes with the fix. `overlay_position_live` passed another 77 checks using a hidden
window on the current 3840x1600 display at 125% scaling. This live check does not change
the taskbar or display settings; additional monitor layouts are covered by simulation.

All 68 native CTests passed in both `RelWithDebInfo` and `Debug`: ten core tests, 27 hook
tests per architecture and two Vulkan tests per architecture. Each staged DLL was checked
against its build output using SHA-256 and PE architecture validation; both Vulkan
manifests still match the build output.

Renderer input SHA-256 at build time:

- `src/core/OverlayWindow.cpp`: `69480C5D60CA877C45F94A868E82EC4AE6F2CCAF490CA38DF5D3EE7165102B23`
- `src/core/OverlayWindow.h`: `CCD5FF65E9E3525C47C196C365DD070E3EFB7B7CBDFAB90B8D3441D77ADB82A7`

| Native DLL | SHA-256 |
| --- | --- |
| Core x64 | `A0ABAD043AA750E8D710F4375D9B42CB8D313FCCEB8ED7BB012561AA545BA4C8` |
| Hook x64 | `9EAD2288F5ECDC52B16137572734F8095AE7F0EAA2B916B3157809E6B1DD28EC` |
| Hook x86 | `6F716E0A273F4DBB67E9F0DC1F0514C6EB1ED1D6FFF60C0B4AE72E490DEE2593` |
| Vulkan x64 | `E23295F58837BF9DF7DF94B21FE206C17E746D5D53279D5D55DE606F14EDD2D6` |
| Vulkan x86 | `D55B25FCD8668507D156952F94F68496ED44077C17553482334217CE2A15AA26` |

### Complete native rebuild (2026-09-15)

All five native DLLs were rebuilt from clean private OSD `main` revision
`ad9ed5a4aa4b65f38849079d3020cdaf3fe773f8` using Visual Studio 2026/v145 and
`RelWithDebInfo`. Every CMake tree was freshly configured and built with `--clean-first`:
core x64, DXGI hook x64/x86, and Vulkan layer x64/x86.

All 66 native CTests passed: eight core tests, 27 hook tests per architecture, and two
Vulkan tests per architecture. Each staged DLL was checked against its build output using
SHA-256 and PE architecture validation. Both Vulkan manifests match the build manifests
byte for byte and retain their manifest-relative library paths.

These DLLs supersede the native payloads documented below.

| Native DLL | SHA-256 |
| --- | --- |
| Core x64 | `EF7B2D9D28F12FD6641DA366C18C43FEA86C4F609B3E79291A263CA75A733229` |
| Hook x64 | `7F8DE0379BF9943A70C5BD6E2045E68887EEB2353D7020F1424B0CBA0B4FF26B` |
| Hook x86 | `3DFBB81BB0D930CB33307A168A54B2ADEB69D938D47A3D8CAF86447E568A9254` |
| Vulkan x64 | `483C4C226C5CD39747B3B2BD44598A2E7D7EFAFE27683C59A6EC77CFA9BC420C` |
| Vulkan x86 | `2D4E8A5CBC16A280A34890E57EC47D05B535BA6D6F19E312B04211D602EF8891` |

### Asynchronous HookLog file output (2026-09-15)

The x64 and x86 DXGI hooks were built in `RelWithDebInfo` with Visual Studio
2026/v145 from the source state now committed as private OSD revision
`ad9ed5a4aa4b65f38849079d3020cdaf3fe773f8`. This revision includes FSR control log filtering
and asynchronous HookLog output. These hooks supersede the builds below.

With `CFX_HOOK_LOG=1`, HookLog captures the timestamp and formatted arguments in a fixed
2,048-entry buffer. A single writer on a private Windows threadpool performs all file opens,
batched writes and closes. Producers use interlocked lists and never wait for disk access or
queue space. Queue overflow and file failures are counted and reported on a successful write.
The writer preserves the existing PID/timestamp prefix and supports concurrent log readers.

Initialization runs on the hook's init worker, outside the loader lock. Enabled logging pins
the module so queued callbacks remain valid. The writer runs finite callbacks with no permanent
queue-wait loop; process detach only disables new log calls and never joins or flushes. Pending
diagnostics can therefore be lost at process termination.

All 54 native hook CTests passed (27 per architecture), with logging enabled for the real-hook
integration tests. Seven logger regressions cover a blocked writer, concurrent FIFO delivery
and original timestamps/arguments, overflow accounting, file-error recovery with a tail reader,
disabled logging, disabling during a write, ExitProcess with a blocked writer, and last-thread
exit after the writer is idle. The overflow test delivers 2,048 entries and reports all 14,337
losses from 16,385 calls while the producers finish before the writer is released.

| Hook | SHA-256 |
| --- | --- |
| x64 | `DE56205256B0BBCF2216683A5DA548711F8058F32139B14CF387475F3E32FDEA` |
| x86 | `601C6429F94C503EF7A00518F23B7B1838BC6A5126C5C9337ADC2ECBD638CDE2` |

### FSR control log filtering (2026-09-15)

The x64 and x86 DXGI hooks were rebuilt in `RelWithDebInfo` with Visual Studio 2026/v145
from private OSD revision `e6458355fe44f1ca6944a3ccff15ef3c090ef923` plus the local FSR
control log filter changes. These hook builds supersede the pair described below.

Accepted FSR control calls are grouped by their opaque context handle. The first state and
every ON/OFF transition are logged immediately; identical calls produce one summary per
context every ten seconds, including the suppressed-call count. A state transition includes
the pending count for the previous state. Successful context lifetime boundaries reset the
filter for reused handles. Filtering occurs before formatting/file I/O and does not gate
frame-generation telemetry or presentation work.

All 40 native hook CTests passed (20 per architecture). The new regression simulates the
AC Shadows pattern: 11,090 accepted ON calls at approximately 68 Hz produce 17 entries
(one initial state and 16 summaries). It also covers immediate transitions, interleaved
contexts, handle reuse, bounded context storage, and concurrent calls without lost counts.

| Hook | SHA-256 |
| --- | --- |
| x64 | `B42DF7B867243B4D71B919314B60407B63DDC65A2117D3DD1234AC83989AC12D` |
| x86 | `74C892361C1EDE4C13BF3A884A26D799FB4DE91FD61E45817775E9A51F7016D8` |

The core, managed bridge, and Vulkan payloads retain the provenance documented below.

### Framerate graph correction (2026-09-15)

All five native DLLs were rebuilt in `RelWithDebInfo` with Visual Studio 2026/v145 from the
source state now committed as private OSD revision `e6458355fe44f1ca6944a3ccff15ef3c090ef923`.
It contains the framerate graph correction in `OsdInstance`, `Widget`, and `ScrollChart`,
plus the `apps/framerate_graph_test` regression.

The renderer now honors `ShowGraph` for the `Framerate` entry, deriving per-frame FPS as
`1000 / frametime_ms` on the existing replay timeline. FPS has its own adaptive scale, while
Frametime and Displaytime retain their shared millisecond scale. Each enabled graph is drawn
even when several entries share a group. CapFrameX's `OsdOverlayBridge` also feeds timestamped
frametimes when only the FPS graph is enabled, with no duplicate samples when both are enabled.

All 50 native CTests passed: eight core tests, 19 DXGI hook tests per architecture, and two
Vulkan tests per architecture. The new regression checks timed and legacy samples, FPS values,
independent scaling, invalid samples, and graph visibility in separate/shared groups. A CPU
render of all three graphs was visually checked, and the existing raster/scale regression also
passed. All five payload copies were checked with SHA-256 and PE architecture validation.
The managed interop DLL and Vulkan manifests are unchanged.
The user subsequently confirmed the Vulkan in-game graph after the registered x64 and x86
layer DLLs in the installation folder were updated to these builds.

### Earlier native builds

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

### Current SHA-256

- managed bridge SHA-256: `21541CEB337F4DCC3F05192D27029279BDFA0F19057EAC764EBBC071E6E8A17C`
- core SHA-256: `62AA80B30A363C740EE8921F79620A07BF705AFF99AEB6B460E754BDB3F56FB3`
- hook x64 SHA-256: `0EAA7B90A728379062A057B4828076D41DE1F7ADCAD604544E8EE5C431F12824`
- hook x86 SHA-256: `E4C6D78144560351E65C327883B7F3686728BAC933AB43E8129973EED34C681E`
- Vulkan x64 SHA-256: `B2934C46A4B69FEEFFCF3F0304136D6911E03EADB29E695E74C136656B5DBC1B`
- Vulkan x86 SHA-256: `D09B67C8E2D560F5286F21A226AAFE2C5CB80E5403F5636FE54B70B9A1FA44AF`

## Contents

- `net10.0-windows/` — `CapFrameX.OSD.Interop.dll` (managed P/Invoke bridge, x64)
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

For Vulkan runtime tests, also check the manifest paths registered in both HKLM registry views.
The loader uses the DLL beside each registered manifest, which can still be the installed copy
under `Program Files (x86)` even when CapFrameX runs from its build output. Updating this prebuilt
folder and rebuilding the app does not update that installed copy. Use an updated installation
or register the rebuilt staging folder with the private repo's `register_layer.cmd <folder>`
for development, keeping one registration per bitness. Restart the Vulkan target to load the
updated layer; a process already running retains its loaded DLL.

Configure every native tree with the **same** toolset the core preset pins (`-G "Visual Studio 18
2026"`, toolset v145, matching the `.vcxproj` projects in this repo). `hook_poc` and `vk_layer`
take no preset, so passing `-G` explicitly is what keeps them in step: core, hook and layer all end
up loaded in the same game process, and a silent toolset split between them is hard to spot.

Rebuild **all** native trees, not just the core: `hook_poc` and `vk_layer` compile the core sources
into themselves (`${CFX_OSD_CORE_SRC}`), so a core change that is not followed by a rebuild of
those two leaves them silently behind.

# CapFrameX.OSD prebuilt binaries

Fallback binaries for the CapFrameX in-game OSD, built from the private
[CXWorld/CapFrameX.OSD](https://github.com/CXWorld/CapFrameX.OSD) repository.

Development builds prefer a sibling `CapFrameX.OSD` checkout, for example
`E:\Code\CapFrameX.OSD` beside `E:\Code\CapFrameX`.
Without that source checkout, builds use these prebuilt managed and native binaries.
Set `CfxOsdSourceDir` to use a different source location; the existing
`CfxOsdSubmoduleDir` override remains supported. Set `CfxOsdFromSource=false` to use
only prebuilts even when OSD sources are available.

## Build provenance

### Frame-generation pairs in frametime graphs (2026-10-31)

All five native trees were rebuilt clean (VS 2026/v145, `RelWithDebInfo`, `--clean-first`) from the
working tree committed as OSD `efcc9f8c6c3d60412ab06fa390b9e8e6e6678d67`. The only native change is in
`src/core/ChartGeometry.h`: `AppendColumnExtremes` keeps both extremes of a pixel column that share a
timestamp but not a value. PresentMon gives a frame-generation pair one CPU start time (the generated
frame ~10 ms after the previous pair, the real frame ~0.1 ms after it); the core kept only the
earlier extreme, so every frametime graph drew the 0.1 ms line with a regular row of false ~10 ms
spikes wherever a column held two pairs (RE Requiem with FSR FG: 10.4 ms pairs, 11.2 ms columns, a
spike every ~14 px, in the rows' graphs and in tile charts such as DUOLINE's). Such a stream now draws
the band it is. Hook and Vulkan layer compile the core sources, so they changed too; the Vulkan
manifests and Interop (still the signed build) are unchanged. The installed layers in
`C:\Program Files\CapFrameX\vulkan` and `vulkan\x86` were **not** updated (no elevated shell).

Verification: all 16 core CTests pass, among them `chart_geometry` with the new
`TestColumnExtremesKeepBothValuesOfOneTimestamp`, as do the 33 hook and 10 Vulkan CTests on x64 and
x86. A capture of the game (whose `NormalizeTimes` spreads equal start times by 0.1 ms) fed back with
its live timestamps reproduced the spikes with the previous core and draws the band with this one.

| Updated component | SHA-256 |
| --- | --- |
| native\cfx_osd_core.dll | `CD22CAFE0FD27D040956A1A5EEFFF7AD0D42B6AD01A7546E8D2EE423D2484909` |
| native\cfx_osd_hook.dll | `5C23CD2641EE5D303552181E7AB6621B6C3DAF39F8224F941A47F3FB2B2E2740` |
| native\x86\cfx_osd_hook.dll | `D887A56EC97E1EF5D16CB1FA9B3A8B4470B28B1D4C79BB04E476FACD72AB456C` |
| native\vk\cfx_osd_vklayer.dll | `1703ED01C75CC010C7172ABAAF15F68F5B05CB9F749BDCA6B4623143DBA7B3C3` |
| native\vk\x86\cfx_osd_vklayer.dll | `7CDB1849FE723310BB3C8822E59DBB4720A9C56F06BED7A5EE45824B547DBA7A` |

### Row blocks that follow their rows (2026-10-31)

Controls was rebuilt in `Release|x64` for .NET 10 (`--no-incremental`) from the OSD working tree
committed as `66ecd20` and `d6bcc79711c10ca4da621dd699985d72ade0601e`. A row block (`classicRows`) with
`classicRowsAutoHeight` ("Height follows the rows", `OsdDesignTile.FollowsRows`) exports height 0 and,
on a canvas, no panel `frameHeight`, so the renderer measures every row and graph of the linked
profile instead of clipping them; the native core has measured a row block without a height since
`5dde08e`. The tile's frame height only reserves room in the editor. `Validate` rejects such a block
in a container (`Editor_RowsAutoHeightNested`) and any tile at or below its top within its columns
(`Editor_RowsAutoHeightBelow`). The three hybrid templates end with such a block, and Hybrid Thread
Deck draws its thread loads in its cyan accent on the neutral panel instead of magenta on a dark red
one. `Editor_RowsAutoHeight`, `Editor_RowsAutoHeightHint` and the two errors were added in all five
languages and the Hybrid Benchmark description changed; `Editor.en.json`, `Editor.ko.json`,
`Templates.en.json` and `Templates.ko.json` are copied here. The native DLLs, Interop (still the
signed build) and both Vulkan manifests are unchanged.

Verification: the editor demo's `--verify` (1764 checks) and `--verify-native` (1241 checks, among them a
row block that grows from 2 to 24 rows without moving the tile above it) pass. A CapFrameX Release
build with `CfxOsdFromSource=false` stages exactly this DLL into the test output and passes the 799
tests of the overlay design classes, among them the 11 of `OverlayHybridStarterTest`. The full
`Debug` run from source passed 3447 of 3456 tests outside the `Integration` category; the 9 record
file tests failed only because that output lacked `TestRecordFiles`, and pass in the Release output.

| Updated component | SHA-256 |
| --- | --- |
| net10.0-windows\CapFrameX.OSD.Controls.dll | `1CD9A62BEA3D5F2C0232EDBF9F384C8D83C6E92ECDD91E87E74F0DEB84484760` |

### CapFrameX logo tiles (2026-10-27)

Controls was rebuilt in `Release|x64` for .NET 10 (`--no-incremental`) from OSD commit
`60dbc5b0e754d31d3de932c148f81fa60143ff64`, which follows `beeed24`. The palette offers a CapFrameX
banner and a square CapFrameX logo (`OsdLogoPresets`): image tiles in the brand blue `#0271F9` that
draw the white and lime mark with fit `contain`, so a frame that no longer matches the image's ratio
shows more blue instead of a distorted logo. The images are embedded as `Resources/Logos/CapFrameX-Banner.png`
(1280 × 256) and `CapFrameX-Logo.png` (512 × 512), rendered by CapFrameX's
`source/CapFrameX/Icons/New-OverlayLogoTiles.ps1`. In a grid the banner spans every column and is as
tall as the width the preview laid out ÷ 5, the logo takes one column; on a canvas the banner takes the
whole width and the logo 96 × 96. `Editor_CapFrameXBanner` and `Editor_CapFrameXLogo` were added in
all five languages; `Editor.en.json` and `Editor.ko.json` are copied here. The native DLLs, Interop
(still the signed build), both Vulkan manifests and the template catalogs are unchanged.

Verification: a CapFrameX Release build with `CfxOsdFromSource=false` stages exactly this DLL and the
prebuilt core into the test output and passes all 3434 unit tests outside the `Integration` category,
among them the 10 tests of `OverlayLogoTileTest` (grid, canvas and container placement, checked in
native renders).

| Updated component | SHA-256 |
| --- | --- |
| net10.0-windows\CapFrameX.OSD.Controls.dll | `50F077556C69DA462D3DAA0C0C3E4B0F3630CEB48C8093E7C46DD19BDBC3BD46` |

### Native D3D11 swapchains under the generic route (2026-10-25)

All five native trees were rebuilt clean (VS 2026/v145, `RelWithDebInfo`, `--clean-first`, on a
machine with an RX 9070 XT) from the OSD working tree on `dcf0bf99bff0a169cfd14fd9f7d7e341bbfa0638`
that was committed after the build as `beeed2462b1e260747417f6a1e8a34c6f3fd00cd`:
`hook_poc/src/frame_generation_state.h`, `frame_generation_state_test.cpp`, `overlay.cpp` and
`present_hook.cpp`, documented in `hook_poc/README.md` and the OSD repo's CLAUDE.md. The
device behind each swapchain now picks the renderer under the generic D3D12 route (bit 2) as well, as
with RTSS: a native swapchain that exposes an `ID3D11Device` keeps the D3D11 renderer
(`PresentTakesD3D12OnlyRoute`, log `generic D3D11 profile Present draw #1 submitted`). Metro 2033
Redux with FSR presents D3D11 with `d3d12.dll` resident; the previous hook skipped D3D11 under that
route and drew nothing. Core and Vulkan layer sources are unchanged since `dcf0bf9`. The layers differ
from the previous ones in the build timestamps and in `CFX_VK_COMPATIBILITY_BUILD`, which hashes every
`hook_poc` header. Controls, Interop (still the signed build), both Vulkan manifests and the template
catalogs are unchanged.

Verification: all 16 core CTests pass, as do the 33 hook and 10 Vulkan CTests on x64 and x86. A
CapFrameX build with `CfxOsdFromSource=false` was not run.

| Updated component | SHA-256 |
| --- | --- |
| native\cfx_osd_core.dll | `87191C4E21699D3160E11C93DFC324B03299C16DCA18272038F95FD1B964462D` |
| native\cfx_osd_hook.dll | `E5592624E6DFC81EA88B75B862651242C5768493681F23CC989F9A9B5D03EBB3` |
| native\x86\cfx_osd_hook.dll | `63193092CCEF7FD4F65575092323C2188C0483F71477374A2EEE09B2C3119719` |
| native\vk\cfx_osd_vklayer.dll | `979CE2C3D674A179BB64CEFCCB4C5E175508674510AB11650683A86C23A965F7` |
| native\vk\x86\cfx_osd_vklayer.dll | `B55E28590D94A83456F4B9CA2762A5E35F875BC3773CC3B10B34CF503405194B` |

Learned compatibility profiles are bound to the hook build hash, so CapFrameX re-verifies learned
routes once with these hooks. These are unsigned development binaries.

### Widget colors and tile styles (2026-10-24)

All five native trees were rebuilt clean (VS 2026/v145, `RelWithDebInfo`, `--clean-first`) and
Controls in `Release|x64`, .NET 10 (`dotnet build CapFrameX.OSD.sln -c Release --no-incremental`),
from the OSD working tree on `c84f7679bb8398da3ce1db40e018ab1a4787ab28` that was committed right
after the build as `7191f320ab9d803372fbe0e1eef22799477e509d` (native widget colors) and
`dcf0bf99bff0a169cfd14fd9f7d7e341bbfa0638` (editor tile styles): `src/core/Config.cpp`, `Widget.cpp`
and `Widget.h`, the Controls sources (`OsdDesignDocument*`, `OsdDesignPresets.Composite`,
`OsdEditorControl`, `OsdEditorState*` with the new `OsdEditorState.Styles.cs`, `OsdTileStyle`,
`OsdTemplateThumbnail`), the five `Editor.*.json` catalogs and the OSD repo's CLAUDE.md. Widgets
take per-element colors, each with a theme default: `bar` and `gauge` `labelColor`/`valueColor` (the gauge also `trackColor`), `chart` `labelColor`, `valueColor`,
`backgroundColor` and `borderColor`, `heatmap` `labelColor` (legend and core type labels). The editor
lists per tile kind which of them it draws (the Tile style rows), fades panel colors by the design's
tile opacity and writes the design font as `theme.fontFamily` (Style tab, one font per design;
designs saved with tile fonts are migrated by `FromJson`). `Editor.en.json` and `Editor.ko.json` are
copied here. Interop (still the signed build), both Vulkan manifests and the template catalogs are
unchanged. Older hooks and layers ignore the new keys and draw the theme color.

Verification: all 33 hook CTests pass on x64 and x86, and all 10 Vulkan CTests pass on x64 and x86.
15 of the 16 core CTests pass. `chart_gpu_raster` ("retracing a line must not accumulate opacity")
fails on this machine (RTX 5090, driver 32.0.16.2101) and fails the same way on a clean export of
`c84f767`, so it does not come from this change; `chart_gpu_raster_warp` passes.
CapFrameX built against these sources (`CfxOsdFromSource`, Release) passes all 3206 runnable unit
tests (6 skipped), among them `OverlayTileStyleTest` and `OverlayTileDocumentTest`; a build with
`CfxOsdFromSource=false` was not run.
A clean rebuild of the five trees from `dcf0bf9` (2026-10-25, on a machine with an RX 9070 XT)
differs from these DLLs only in the build timestamps, a few swapped incremental-link thunks and one
hash constant; its code sections match byte for byte for the core and the x64 hook. There all 16 core
CTests pass, `chart_gpu_raster` included, as do the 33 hook and 10 Vulkan CTests on x64 and x86.

| Updated component | SHA-256 |
| --- | --- |
| native\cfx_osd_core.dll | `2F7F182A7D29E7B979B6EF9ED82C99557933A6E05DB47F659572C62617F0A128` |
| native\cfx_osd_hook.dll | `3EB6B3637F7FFDB8CE3EA2AC0BEFD4AD8A09983AB44F961E64B1041E328B44AE` |
| native\x86\cfx_osd_hook.dll | `DF121CE631093C3758B191AA341F428A4F85BBB50B43CE20BBA1EE5482D9FC2B` |
| native\vk\cfx_osd_vklayer.dll | `EEFF5E853EBCDCE0DD82CC2543728F4C05867A09D7AFC69BB28A646651386C32` |
| native\vk\x86\cfx_osd_vklayer.dll | `2D9098552F2DA45F1B630AF86783E5A49AC09189B9C390AA71857BC82B11F411` |
| net10.0-windows\CapFrameX.OSD.Controls.dll | `C0CFAAFB704E3CE636667BF53D0C2CB7F6E609CAF68F3F2F10AF97561D820038` |

Learned compatibility profiles are bound to the hook build hash, so CapFrameX re-verifies learned
routes once with these hooks. These are unsigned development binaries.

### Multi-line charts and stutter thresholds (2026-10-22)

The five native DLLs (VS 2026/v145, `RelWithDebInfo`) and Controls (`Release|x64`, .NET 10) were built
from OSD commit `c84f7679bb8398da3ce1db40e018ab1a4787ab28`, which follows `d958d92`; the build tree
differed from it only in the OSD repo's CLAUDE.md. A chart may carry `lines` (1..4 on one Y axis): the values of a
frame series or its adaptive stutter threshold, factor x the moving average of the analysis chart
(`StutterThreshold.h`). A chart with a threshold gets its own scale that keeps the threshold in view,
the CPU producer composes one copy set per chart from all its lines, and the editor gains the line
list (`chartLines`), the "Stutter charts" palette entry and 20 strings per catalog (`Editor.en.json`
and `Editor.ko.json` copied here). Interop (still the signed build), both Vulkan manifests and the
template catalogs are unchanged. A Controls build without this change rejects designs with
`chartLines`; an older native renderer ignores `lines` and draws the chart's single series.

Verification: all 16 core CTests pass, with multi-mask composition and the moving average's parity
with the analysis chart in `chart_geometry` and the `lines` schema, shared range and one CPU copy set
per two-line chart in `design` (415 checks). All 33 hook CTests and all 10 Vulkan CTests pass on x64
and x86. CapFrameX built against these sources (`CfxOsdFromSource`) passes all 3177 runnable unit
tests (6 skipped), among them `OverlayChartLinesTest`, `OverlayTileDocumentTest` and
`OverlayDesignRuntimeTest`; a build with `CfxOsdFromSource=false` was not run.

| Updated component | SHA-256 |
| --- | --- |
| native\cfx_osd_core.dll | `1B2E72108A2B4808316F9F6B00367D43C2A2D0A25796F8956096932BD02428C8` |
| native\cfx_osd_hook.dll | `D7338D1C0F1FE76C3B6CDEDE45335D678EE66E93EC0391EED462440C622084DB` |
| native\x86\cfx_osd_hook.dll | `F9EAFD885C7539A291AC84848589D7B221853D9EE649395EBAD5555DB5909661` |
| native\vk\cfx_osd_vklayer.dll | `DFDAB9497B5D4F82B742C439BE91C0340A4F744C1E409A395E9140A6FB8E4992` |
| native\vk\x86\cfx_osd_vklayer.dll | `08D4EF36798F97FE1779F0DC55382C77784410191F724EE2CB7B0EB9209D6002` |
| net10.0-windows\CapFrameX.OSD.Controls.dll | `B63EC525A3EF73DC9CC2CA6673194C542715342214BB1F9963E7BF766B597FD9` |

Learned compatibility profiles are bound to the hook build hash, so CapFrameX re-verifies learned
routes once with these hooks. These are unsigned development binaries.

### Chart lines past the window end (2026-10-21)

The five native DLLs were built with VS 2026/v145 in `RelWithDebInfo` from OSD commit
`d958d92d15b6de6f2b70c40289dd914eb9c91b6a`, which follows `28c40f2`. The native sources did not change
between `6d963d5` and `28c40f2`. The chart-layer hosts (D3D11 external-v2, the
D3D12 hook's CPU producer, the Vulkan layer) scroll a cached chart by up to ~4 px between two rebuilds
(5 s on 300 px). The line stopped at the window end, so its last columns opened and closed at the
plot's right edge, in classic rows and tiles alike. The masks of `RasterizeChartLines` now continue
the line past the window end by the variant horizon (`RenderContext::chartLineEnd`): the PresentMon
replay draws the samples it trails, the local graph keeps its last height past the newest present,
and a line that ends inside the window stays open. Their clip ends at the window's last possible
column (`floor(plot.x) + ceil(plot.w)`) instead of the stroke's reach. Controls, Interop, both Vulkan
manifests, the localization files and the template catalogs are unchanged.

Verification: all 16 core CTests pass (`chart_geometry` checks that every pre-scrolled copy covers
every plot column for replayed and local charts and, as a negative control, that a line ending at
the window end leaves the scrolled columns open). All 33 hook CTests and all 10 Vulkan CTests pass on
x64 and x86. A CapFrameX Release build with `CfxOsdFromSource=false` stages exactly these DLLs into
the test output and passes all 3122 runnable unit tests (6 skipped).

| Updated component | SHA-256 |
| --- | --- |
| native\cfx_osd_core.dll | `9F6E5A61B993252B7715F3B01B7955D42A82656D4B9191FD1E0220DDCE1ECFFE` |
| native\cfx_osd_hook.dll | `3B29A8FBB38476EF97EADCE393F235E7C6B98D70E2E191A6F9D45FCAC47FFC85` |
| native\x86\cfx_osd_hook.dll | `9F51C9A54E5E296B2F7DFB5EF8F8B242570E804D450879D05EDB7A4C61A7EE78` |
| native\vk\cfx_osd_vklayer.dll | `37B16B440AC0077D7D3D0CF4489A656A8FD4D11935288B7264B9614451F304D5` |
| native\vk\x86\cfx_osd_vklayer.dll | `4847DBE42715174295EB14A16545A422A506C1921267780A48B1F46EF0128960` |

Learned compatibility profiles are bound to the hook build hash, so CapFrameX re-verifies learned
routes once with these hooks. These are unsigned development binaries.

### Core-type group roles (2026-10-20)

Controls was rebuilt in `Release|x64` for .NET 10 from OSD commit `28c40f25893fe924d357251dc43fa467e3d61b94`, which follows
`cf0901b`. A group role may name one CPU core type: `role:cpu-loads|P` binds only the P-cores of the
family (`OsdDesignDocument.BindPresetSources`, like CapFrameX's `OverlayRuntimeSourceBinder`) and keeps
the type in the bound key (`device|family|unit|P`), so refreshing the members adds that type only. The
FUSION ARL and RPL starters use it for their P-CORES and E-CORES tiles; a Controls build without this
change leaves those tiles unbound in the designer. The native DLLs, Interop, both Vulkan manifests, the
localization files and the template catalogs are unchanged.

Verification: a CapFrameX Release build with `CfxOsdFromSource=false` stages exactly this DLL into the
test output and passes all 2543 runnable unit tests (6 skipped), among them
`CoreTypeRolesBindOneCoreTypeOfTheFamilyLikeTheEditor` and `RefreshOfAGroupOfOneCoreTypeAddsOnlyReadingsOfThatType`.

| Updated component | SHA-256 |
| --- | --- |
| net10.0-windows\CapFrameX.OSD.Controls.dll | `ACD87991C11ED0C1639AE797C47A24867252D4544F97B83A57ECCA6CAA2761B1` |

### Inspector source search hint (2026-10-19)

Controls was rebuilt in `Release|x64` for .NET 10 from OSD commit `cf0901b94f823b8fb430c8163eb24f1bb41c19e4`, which follows
`6955d6c`. The search field under the inspector's "Telemetry source" label filters the source picker
below it and never shows the tile's own source. While it is empty it now says what it searches
(`Editor_078`, "Search hardware, sensor or source ID", the field's existing tool tip in all five
languages), so it no longer reads as a tile without a source. The native DLLs, Interop, both Vulkan
manifests, the localization files and the template catalogs are unchanged.

Verification: a CapFrameX Release build with `CfxOsdFromSource=false` stages exactly this DLL into the
test output and passes all 2488 unit tests, among them `InspectorSourceSearchNamesItsPurposeUntilSomethingIsTyped`.

| Updated component | SHA-256 |
| --- | --- |
| net10.0-windows\CapFrameX.OSD.Controls.dll | `EA17C6ADADC5CE87F28C27128F41416A9B75A828693D847726CFDBCBD3177963` |

### 10 s heatmap window default (2026-10-18)

Controls was rebuilt in `Release|x64` for .NET 10 from OSD commit `6955d6c10c649af4791d18d88db27bd4cd988a34`, which follows
`6d963d5`. A heatmap group without an explicit window now averages over 10 s instead of 60 s
(`DefaultHeatmapWindowSeconds`). Designs saved under the former default stored no window and exported
their cells as `avg:60`; `FromJson` gives those groups 60 s explicitly and then requires the round trip
to match the saved scene with the same change, as it already does for pre-font families. A Controls
build without this change rejects designs that rely on the 10 s default, among them the FUSION starter.
The native DLLs, Interop, both Vulkan manifests, the localization files and the template catalogs are
unchanged.

Verification: a CapFrameX Release build with `CfxOsdFromSource=false` stages exactly this DLL into the
test output and passes all 2442 runnable unit tests (6 skipped).

| Updated component | SHA-256 |
| --- | --- |
| net10.0-windows\CapFrameX.OSD.Controls.dll | `199AF0762F664E1A023A0ABE99A214D0417E474B469AA5725574B2AA8D0A774D` |

### Chart edges and 1 s heatmap window (2026-10-18)

The five native DLLs were clean-built with VS 2026/v145 in `RelWithDebInfo` and Controls was rebuilt
in `Release|x64` for .NET 10 from OSD commit `6d963d59a6709d4069be4df7f5a8a08f4baa941c`, which follows `6237256`. Every
chart line, classic rows included, now meets both edges of its window: `AppendChartLine` adds one
point at each edge, interpolated between the samples around it. Before, the line ended at the sample
nearest each edge, whose column moves by up to the frame spacing while the window scrolls, and its
outermost 1-3 px flickered. Controls lowers the heatmap window minimum from 10 s to 1 s and draws the
inspector's validation frame inside each field, so the inspector no longer clips its left edge.
`Editor_HeatmapWindowHint` changed in all five languages; the en/ko copies in `Localization/` are
updated. Interop, both Vulkan manifests and the template catalogs are unchanged.

Verification: 15 of 16 core CTests pass (`chart_geometry` checks that a scrolling line meets both
edges at every window position and, as a negative control, that the samples inside the window alone
start up to 2 px late); `chart_gpu_raster` fails on the hardware adapter as documented below. All 33
hook CTests and all 10 Vulkan CTests pass on x64 and x86. A CapFrameX Release build with
`CfxOsdFromSource=false` stages exactly these DLLs into the test output and passes all 2439 runnable
unit tests (6 skipped).

| Updated component | SHA-256 |
| --- | --- |
| native\cfx_osd_core.dll | `F4C776AE77DE9E913676C58FB42F8771B70D529B53072C4113BE054397C53BAF` |
| native\cfx_osd_hook.dll | `C15DFC2835A2B8B5FB9B9685F9CAA208AFEB39C28A1CCC8D33F4736AB7AD70F5` |
| native\x86\cfx_osd_hook.dll | `2B98C708EC5CE8D38ACF82F00935EEDCCB36E7B0DBBEA95DBE4E23789B961C22` |
| native\vk\cfx_osd_vklayer.dll | `1AE5774A993BF510190839D0AAF43759E1D6AD5DA1F74F86075054CDB0760ED5` |
| native\vk\x86\cfx_osd_vklayer.dll | `60E39D73F0070267336A48C0393C47D22695C508FDC0D9171498268694B458B7` |
| net10.0-windows\CapFrameX.OSD.Controls.dll | `FE7E1F011395E73ADEA669733C0474FE3D68AADDCB4CFF592F77BEC28FEC39DA` |

Learned compatibility profiles are bound to the hook build hash, so CapFrameX re-verifies learned
routes once with these hooks. These are unsigned development binaries.

### Core load heatmap (2026-10-18)

The five native DLLs were clean-built with VS 2026/v145 in `RelWithDebInfo` and Controls was rebuilt
in `Release|x64` for .NET 10 from OSD commit `62372562f310e27c54074c04a8bab0b4ddfdfaff`, which adds the heatmap to
`d102d9b`. The new `heatmap` widget draws one cell per key, shaded from idle to `color`, under a
header with the title and an idle-to-maximum legend. Every core class forms a labelled block of its
own (P-Cores, E-Cores, ...), the blocks side by side; up to 16 cells share one row, more take the
fewest rows at which the blocks
need at most 16 columns (8 P + 16 E: 4x2 beside 8x2). Controls adds the heatmap group display
(`GroupDisplay = "heatmap"`, `GroupWindowSeconds` 10..600, default 60), which exports each member as
`avg:{seconds}:{key}`; CapFrameX supplies those averages. A Controls build without this change
rejects designs with a heatmap group, among them the FUSION starter. `Editor_GroupHeatmap`,
`Editor_HeatmapWindow`, `Editor_HeatmapWindowHint`, `Editor_HeatmapColor` and `Editor_HeatmapHint`
were added in all five languages; the en/ko copies in `Localization/` are updated. Interop, both
Vulkan manifests and the template catalogs are unchanged.

Verification: 15 of 16 core CTests pass (`design`: 385 checks, including the heatmap block layout and labels,
round trip and raster checks); `chart_gpu_raster` fails on the hardware adapter as documented below.
All 33 hook CTests and all 10 Vulkan CTests pass on x64 and x86. A CapFrameX Release build with
`CfxOsdFromSource=false` stages exactly these DLLs into the test output and passes all 2437 runnable
unit tests (6 skipped).

| Updated component | SHA-256 |
| --- | --- |
| native\cfx_osd_core.dll | `27734C4C75C8E6A83B3054E3DD02DC54C2E005D80ECCC70D6B64530A6CFF90F2` |
| native\cfx_osd_hook.dll | `CBA0BF711265053962F8803D0F146AB9F4FD8A3FED77E1AE1F360623E5D74702` |
| native\x86\cfx_osd_hook.dll | `C81ABBE926C962E5EF9CD4B230F19205FED6057C46233510A02F97C8B7165778` |
| native\vk\cfx_osd_vklayer.dll | `766FEAE838963ED7506F8FC23A17BE34BC46C24AF95A0905C0AFB64C072264A5` |
| native\vk\x86\cfx_osd_vklayer.dll | `BCB091D33F2591F9A1D12333B063F0F401ABED9A38E2B8FA744BBB86034B57A5` |
| net10.0-windows\CapFrameX.OSD.Controls.dll | `D3512C84F5422FDBF80763EADEC2E39E8741A6DC47873B803F8A81C83356AB6E` |

Learned compatibility profiles are bound to the hook build hash, so CapFrameX re-verifies learned
routes once with these hooks. These are unsigned development binaries.

### Rebuild diagnostics only with extended logging (2026-10-17)

The five native DLLs were clean-built with VS 2026/v145 in `RelWithDebInfo` from OSD commit
`d102d9b4e2b985162ded31728ab97cbdc92db3f0`, which follows `38a42e1` with logging only. The in-game
producer (both hooks, both Vulkan layers) logs every scene rebuild as `[diag] scene rebuild #N
(producer)` with its duration, panel size and layout scale, and the hook-free line gains the
scale. Without extended logging neither path nor `PopulateClassicRows` formats a line, reads the
clock or counts a rebuild; the two `[scene] rebuild` / `[scene] built` lines that used to be written
on every classic rebuild now need the switch as well. Interop, Controls, both Vulkan manifests and
the localization files are unchanged.

Verification: 15 of 16 core CTests pass (`design`: 363 checks); `chart_gpu_raster` fails on the
hardware adapter as documented below. All 33 hook CTests and all 10 Vulkan CTests pass on x64 and
x86. A CapFrameX build with `CfxOsdFromSource=false` stages exactly these DLLs into the test output
and passes all 2405 runnable unit tests (6 skipped). In CapFrameX.Test's font suite the D3D11, D3D12 and Vulkan targets log one
producer rebuild per change with `CFX_OSD_VERBOSE_LOG=1`; without it the core log keeps only its
four start-up lines.

| Updated component | SHA-256 |
| --- | --- |
| native\cfx_osd_core.dll | `778F25196EB465E80886A2FAD04DAE57CD948CB0232D9D18686845EE8DF400DC` |
| native\cfx_osd_hook.dll | `F4E604DE00ECAE05F1CA04AEC7B4534F56DF6F7AF081060F24F42361FCCCF4FC` |
| native\x86\cfx_osd_hook.dll | `7F394AB6FF58C3BDDD512590781882A8CA959984288C319276EC1427DEC90A4E` |
| native\vk\cfx_osd_vklayer.dll | `0CC42D682B518F0180721606D0E068717F050FC2393C6C49D2C44EFBBE1707AC` |
| native\vk\x86\cfx_osd_vklayer.dll | `AAF339ABFE20C77D320CD54F87CC31449F09478B367BBB52EA026EF3B578C59A` |

Learned compatibility profiles are bound to the hook build hash, so CapFrameX re-verifies learned
routes once with these hooks. These are unsigned development binaries.

### Overlay fonts (2026-10-17)

All seven bundled DLLs were rebuilt from OSD commit `38a42e137baaeb25dc5275cf825f286c3fb81ab0`, which
adds fonts to `bf2e0bc`. The five native DLLs were clean-built with VS 2026/v145 in `RelWithDebInfo`;
Interop and Controls were rebuilt in `Release|x64` for .NET 10 (SDK 10.0.401). Bars, charts, gauges
and linked classic blocks take a `family`, every other text widget already did, and all of them fall
back to `theme.fontFamily`. The new `cfx_osd_set_font_family` sets the classic layout's font; both
hooks and both Vulkan layers read it from metrics header bit6 plus a 128-byte UTF-8 field at
`0x18000` and log `classic font -> ...`. Interop adds `OsdHost.SetFontFamily`. Controls writes the
design font (`OsdDesignDocument.FontFamily`) and a font per text tile; a Controls build without this
change rejects designs whose theme font is not Segoe UI. `Editor_099` changed and
`Editor_DesignFont`/`Editor_DesignFontHint` were added in all five languages; the en/ko copies in
`Localization/` are updated. Both Vulkan manifests and the template catalogs are unchanged.

Verification: 15 of 16 core CTests pass, including the font checks in `design` (363 checks);
`chart_gpu_raster` fails on the hardware adapter as documented below. All 33 hook CTests (including
the new `metrics_shm`) and all 10 Vulkan CTests pass on x64 and x86. A CapFrameX build with
`CfxOsdFromSource=false` stages exactly these DLLs (SHA-256 below) into the test output and
passes all 2405 runnable unit tests (6 skipped). With these native builds, CapFrameX.Test's
`verify-osd-fonts.ps1` passes on D3D11, D3D12 and Vulkan, x64 and x86.

| Updated component | SHA-256 |
| --- | --- |
| native\cfx_osd_core.dll | `B4A90985675576FC966C9A0ED37C115292BEB96B6C7EDCF8EE54425420194856` |
| native\cfx_osd_hook.dll | `097490EC33DE7ED0AD5AB24B1B387BDA2481A84AFF2513C463444434F4433572` |
| native\x86\cfx_osd_hook.dll | `EB709C1E0CC10A2A615C2C6CEFAC34248F093F733EB7DFE4578D6E7D3706B653` |
| native\vk\cfx_osd_vklayer.dll | `35E7A75795FF90D875005F99FA2EA929362EDF494068EF66494E51DC6E80DCB8` |
| native\vk\x86\cfx_osd_vklayer.dll | `4CC3FA5FF93354161AB367E2DD5F8F6DD11B9D99363691C5B5A0D0141DDF38D5` |
| net10.0-windows\CapFrameX.OSD.Interop.dll | `3028F70E51F40C8C3658F6DFC82C5E8A24FEBA952CC52CCC2BDE15E5A5668590` |
| net10.0-windows\CapFrameX.OSD.Controls.dll | `581BD860FA4C9E53489F96E9308F5959C66E2F25AB17BAB701140855BA8186AB` |

Learned compatibility profiles are bound to the hook build hash, so CapFrameX re-verifies learned
routes once with these hooks. These are unsigned development binaries.

### Resolution-relative sizing (2026-10-14)

The five native DLLs were clean-built with VS 2026/v145 in `RelWithDebInfo` and Interop in
`Release|x64` for .NET 10 (SDK 10.0.401) from OSD commit
`bf2e0bcf1be173cf7147942e98f32891de6c4c87`, which adds resolution-relative sizing to `4465d13`.
Once a host calls the new `cfx_osd_set_reference_height`, the core builds the scene with zoom x
target height / 1080 and scales the anchor margins by the same factor; `cfx_osd_set_target_size`
reports the swapchain height of CPU producers. Both hooks and both Vulkan layers turn it on from
metrics header bit5. Interop adds `OsdHost.SetReferenceHeight` and `OsdHost.DesignReferenceHeight`,
which CapFrameX's "Scale with resolution" option calls, so a build without OSD sources needs this
Interop. Controls, both Vulkan manifests and the localization files are unchanged.

Verification: 15 of 16 core CTests pass, including the new resolution checks in `design` (345
checks) and `overlay_position` (455 checks); `chart_gpu_raster` fails on the hardware adapter as
documented below. All 32 hook CTests and all 10 Vulkan CTests pass on x64 and x86. A CapFrameX
build with `CfxOsdFromSource=false` stages exactly these DLLs (SHA-256 below) into the app and test
outputs and passes 2067 of 2069 unit tests; the two failures were PresentMon/vkcube capture tests
that pass on a rerun, and that class fails a different subset on every run. The user confirmed the
scaling live in a game with the sibling build of the same sources.

| Updated component | SHA-256 |
| --- | --- |
| native\cfx_osd_core.dll | `6F396251B6D84E3EE1F32B3A722C7041BE4BF706A9D615CD4677C363EB0A8B83` |
| native\cfx_osd_hook.dll | `3D7068D357ACFB85C149C3731B48CFC959BB6405F38768EF46DEF42547424118` |
| native\x86\cfx_osd_hook.dll | `5678CA0F42A3C3C4F26D998E5C463FB218CF33C39B43DE47A4E1D192771C19EA` |
| native\vk\cfx_osd_vklayer.dll | `5A1AC8C1958A2F9570C08A0F5B1C78ED99C9CFA593BECA98DD9558E40FDB4DF1` |
| native\vk\x86\cfx_osd_vklayer.dll | `DD198F888B49695D6796804FEE63D6D2BE9E8D3629C8F8637B004D232919D474` |
| net10.0-windows\CapFrameX.OSD.Interop.dll | `E9B5C7CC1BE3D8942F9F2DF99AAF345B9FF3463A57F32CA5E4D3F381FDF3F8DF` |

These are unsigned development binaries.

### Scaled minimum tile width (2026-10-14)

The Controls DLL was rebuilt in `Release|x64` for .NET 10 (SDK 10.0.401) from OSD commit
`4465d13f0e65442fb2ae004953d1c1c44411ee8b`, which adds the tile-width change to `612161f`.
`OsdDesignDocument.Validate` required 64 rendered pixels of inner width for top-level tiles, while the
canvas bounds and the editor's resize clamp work in design units. Lowering the design scale could
therefore invalidate a tile that fitted at 100 %: the APEX, REDLINE, FUSION, Panther Lake and OLED
Minimal starters could not be exported at 50 %. Below 100 % the minimum now scales with the design
(`64 * Scale`); above 100 % it stays at 64 rendered pixels, so grid profiles saved under the old rule
remain valid. Interop, the native payloads and the localization files are unchanged.

Verification: a CapFrameX build with `CfxOsdFromSource=false` picks up this DLL (SHA-256 below) and
passes all 110 starter, binder, bar-balance and design-publisher tests, including
`EveryStarterBindsTheSameSourcesAndGeometryAsTheEditor` at 50 %, which fails under the previous rule.

| Updated component | SHA-256 |
| --- | --- |
| net10.0-windows\CapFrameX.OSD.Controls.dll | `4AF36891B3D6C58EC7D306A843D6AE1B73B6170B0D6111F4993AB9E623E8FAEC` |

### Frame-generation telemetry after runtime reload (2026-10-14)

All seven bundled DLLs were rebuilt from OSD commit
`612161fe4a7ba4d81a9d0eb36cc90c8d1c5664aa`, which adds the telemetry unload fix
(`hook_poc/src/frame_generation_telemetry.*`, `present_hook.cpp`, the telemetry test and the hook
README) to `fbf561f`. The five native DLLs were clean-built with VS 2026/v145 in `RelWithDebInfo`; Interop and
Controls were rebuilt in `Release|x64` for .NET 10 (SDK 10.0.401). Only the two hook DLLs contain
source changes. The core, both Vulkan layers, Interop and Controls come from unchanged sources;
their bytes still differ from the previous payloads.

The hook's provider telemetry (Streamline resolver and lifecycle, the DLSS-G SetOptions/GetState
feature functions, `ffxConfigure`, `xefgSwapChainSetEnabled`) used to be armed once per process.
When a game or test unloaded a runtime and loaded it again, the install guards still saw the old
originals, MinHook kept its entry for the old address, and the telemetry kept reporting the vanished
provider: in a real FSR -> DLSS -> FSR run the last phase showed DLSS-FG. A reloaded
`sl.interposer.dll` also made the hook call the Streamline resolver through a trampoline into the
unloaded image. The DLL unload notification now removes every telemetry hook in the unloading
image while it is still mapped and returns that provider to N/A, so the next load arms it afresh.
The FidelityFX creation and swapchain-instance hooks still arm once per process.

Verification: all 32 hook CTests pass on x64 and x86, including the extended
`frame_generation_telemetry` case (install, unload and re-arm for all three providers; it fails on
these defects with the fix stubbed out). All 10 Vulkan CTests pass per architecture. 15 of 16 core
CTests pass; `chart_gpu_raster` fails on the hardware adapter ("retracing a line must not accumulate
opacity", which expects a maximum alpha of exactly 128) while its WARP variant passes. The core
sources are unchanged since `465cccc`, so that failure does not come from this rebuild. With this
hook source and the real CapFrameX on an RTX 5090, the CapFrameX.Test FG switch suite logged the
drops and re-arms; `dlss-fsr-resident` and `fsr-dlss` passed all three phases, and `dlss-fsr-late`
stopped when Streamline's second initialization failed, which also happens without any hook. Both
Vulkan manifests are unchanged. The seven external DLLs match the tested build outputs by SHA-256.

| Component at this rebuild | SHA-256 |
| --- | --- |
| native\cfx_osd_core.dll | `06C9C932F52C2B53FF62ABD05FD4C68F9D4B398241610F13938294E68137D94A` |
| native\cfx_osd_hook.dll | `2886D763C2C9C962DD547AA724C32972CE7E1EE241B92EEFACDB169D83EE4350` |
| native\x86\cfx_osd_hook.dll | `E6D6B1FF244167C85A95FC731AD8D43E10BEC7889331F3EC58AA10E6B34292D3` |
| native\vk\cfx_osd_vklayer.dll | `D5FB129C6D232355F8280D7FAE379AE6FAD56C6199AC0108FC3921828CA5A7BE` |
| native\vk\x86\cfx_osd_vklayer.dll | `11F4A56E433020ED94610492F970475119972614A5C4F61E1E146486A5A70D27` |
| net10.0-windows\CapFrameX.OSD.Interop.dll | `8153F08BB906CBB14EFAD8530DBAE78D5913A83851D79D2BC044A437EC1B5A43` |
| net10.0-windows\CapFrameX.OSD.Controls.dll | `E128D12BFE8B3A3E2FABAE6A007E6B948A60F87F3E16D58CD649CD4C72A761FC` |

These are unsigned development binaries.

### OLED pixel orbit (2026-10-12)

The Controls DLL was rebuilt in `Release|x64` from OSD commit
`fbf561f419be8d8ded443c66b68fd5b381c64a82`, which follows the Ryzen badge PNG from the
entry below with the pixel orbit editor change. `OsdDesignDocument` round-trips an optional
top-level `pixelShift` object (`intervalSeconds` 10..3600, `radius` 1..32), validates it and exports it
only when enabled, so designs without an orbit keep their exact exports. The Layout tab edits it. Five
`Editor_PixelShift*` strings were added in all five languages; the en/ko copies in `Localization/` are
updated. Interop and native payloads are unchanged.

Verification: with this DLL, the generator reproduces `overlay-templates/OLED-Minimal.json` byte for
byte, reopens it through `OsdDesignDocument.FromJson` and renders it through the native preview.

| Updated component | SHA-256 |
| --- | --- |
| net10.0-windows\CapFrameX.OSD.Controls.dll | `CE1910C85157713B5B09185652DCBB0DECA06BE25949D742776906FDBF596F9A` |

### Ryzen badge highlight correction (2026-10-12)

The Controls DLL was rebuilt in `Release|x64` from the sibling OSD checkout at
`465ccccada514ed4450e05a52b52aee9f024bcb4` with its embedded
`Resources/HardwareBadges/Ryzen-Badge-Template.png` regenerated from the corrected
SVG. The three horizontal highlight paths above the Ryzen wordmark were removed
from the shared frame and complete badge SVGs. The palette badge, both portable
badge profiles and REDLINE now use the corrected graphic. REDLINE also aligns
AMD and PERFORMANCE at the same right edge.

Catalog version 5 updates the three affected saved starters only when their
name, ID and complete content fingerprint match the original version-4 export.
Edited, renamed and deleted profiles remain authoritative.

Verification: the Debug x64 application/test build succeeds in an isolated output
directory; all 125 focused profile, editor, badge and native-layout cases pass.
The three actual version-4 exports migrate to the corrected designs without
changing selection or profile count. The native-renderer preview is recorded in
`images/1.9.2/overlay-redline.png`.

| Updated component | SHA-256 |
| --- | --- |
| net10.0-windows\CapFrameX.OSD.Controls.dll | `BD01AF782642F14E08D810D96EE6E6812C0ECE231F1DF42B5D7CB933E498C051` |
| Embedded Ryzen-Badge-Template.png | `00123D9394D2EF3F401E5970D971F51927793D85C82700BF626207EE9AA5B2D1` |

### Committed hardware graphics snapshot (2026-10-12)

The managed and native payloads below correspond to OSD source commit
`465ccccada514ed4450e05a52b52aee9f024bcb4`. Their component hashes are recorded in
the feature entries below. The application now packages 28 graphics (26 SVGs and
two PNGs), including the additional Ryzen 7 9800X3D model, tier and complete badge.
Five portable examples in `overlay-templates` cover both Ryzen badges, REDLINE
(Ryzen 7 9800X3D/Radeon), APEX (RTX 5090/Ryzen 7 9800X3D), and FUSION
(RTX 5090/Core Ultra 9 285K). FUSION uses the original NVIDIA, GeForce RTX and Intel
Core Ultra 9 vector assets and aligns both right-hand header captions.

Final verification: the prebuilt-only Release application/test build succeeds,
all 138 selected editor, native-layout, localization and CPU-sensor regression
cases pass, all 28 graphics import through the editor, and all five example
profiles pass editor roundtrip and runtime validation. The normal source-project
restore graph was restored after checking the prebuilt build.

### Modular Ryzen hardware badge (2026-10-12)

The Controls DLL was rebuilt in `Release|x64` for .NET 10 from the sibling OSD
working tree at `265b991480f92fb4d18368d6540c70f54e896632` plus the pending editor
changes. The new Ryzen badge palette item inserts a standard container with three
independently replaceable image layers: the branded frame, vertical 9600X model
mark and orange Ryzen 5 tier mark. Its portable PNGs are embedded at twice the
default tile size (640x640, 80x310 and 90x80), with no external image dependencies.
The model and tier marks are graphic layers rather than live text fields.

Six SVG assets, including modular components and a complete badge, are available
through the existing graphics folder, bringing the bundled collection to 23 SVGs and
two PNGs. `overlay-templates/AMD-Ryzen-5-9600X-Badge.json` provides a ready-to-import
example. Provenance and the reference-inspired adaptations are documented beside
the graphics.

Verification: Release Controls and prebuilt application/test builds pass. All ten
badge integration cases pass, covering portable serialization/native loading,
Undo/Redo, the four-node tile budget, narrow and short canvases, nested containers,
independent copies, moving and resizing. The 49 existing selected editor/import/
localization regressions also pass. All 25 graphics import through the editor;
the composed badge was visually checked with the native renderer. Source, external,
Release app and test Controls DLLs match by SHA-256. No native or Interop refresh
is required for this palette preset.

| Updated component | SHA-256 |
| --- | --- |
| net10.0-windows\CapFrameX.OSD.Controls.dll | `B019052FB031703057BEF21D0950F2B846ED23A46A8C8CD1AC6837DB38F0E987` |

### Bundled hardware graphics and SVG import (2026-10-12)

The Controls DLL was rebuilt in `Release|x64` for .NET 10 from the sibling OSD
working tree at `265b991480f92fb4d18368d6540c70f54e896632` plus the pending editor
changes. Image import and file drop now accept self-contained static SVGs. The
import dialog starts in `Resources\OverlayGraphics` beside the application, with
the application directory as fallback. The main app packages 17 SVGs and two
original PNGs, including manufacturer/product logos and transparent variants;
their provenance and third-party notices travel with the files. Two additional
Radeon color variants use AMD red `#ED1C24`: red on transparent and white on red.
Their original path geometry is retained, and the adaptations are documented.

SVGs are rendered with SharpVectors.Wpf 1.8.6 and embedded as PNG using the existing
1024-pixel / 128-KiB budget. The SVG viewport preserves physical units, aspect ratio
and transparent margins. DTDs, scripts, external resources and recursive references
are rejected before rendering. Existing PNG/JPEG imports keep their behavior.
`CapFrameX.View` carries the managed package dependency for prebuilt-only builds;
central package management pins its version. No native or Interop refresh is needed.

Verification: the Release Controls build and prebuilt-only Release application/test
build pass, together with all 49 selected editor/import/localization regressions.
The 19 import cases cover portable embedding, transparency, physical units,
gradients, local references, Undo, rejected inputs and raster compatibility. All 19
bundled graphics import through the actual editor and were visually inspected on
light/dark backgrounds. The two physical-unit regressions fail before the viewport
fix. Runtime resources, the dialog start folder and managed dependency copies are
checked; source, external, app and test Controls DLLs match by SHA-256.

| Updated component | SHA-256 |
| --- | --- |
| net10.0-windows\CapFrameX.OSD.Controls.dll | `F2134F60228BB42672CF428ACF84F21AA6BF2A89309808E154B53B301C380EBC` |

### Native icon centering (2026-10-12)

All five native DLLs were rebuilt in `RelWithDebInfo` with VS 2026/v145 from the
sibling OSD working tree at `265b991480f92fb4d18368d6540c70f54e896632` plus the
pending icon-centering fix. A vertical panel containing one icon now gives it the
entire padded interior. Icons center their visible DirectWrite glyph bounds using
cached overhang offsets; the clipping rectangle stays fixed. Ordinary text keeps
its existing alignment. Existing profile JSON needs no migration.

Verification: all 35 selected managed icon-editor, icon-pixel and group-bar-pixel
tests pass against the staged core. The ten new icon-pixel cases cover two preset
glyphs, padding, rectangular frames, nested containers and scales 0.5/1/2,
including 72-pixel glyphs at scale 2. Eight cases fail with the previous core;
all ten pass with the new core, with at most 0.5 pixel of measured center error.
Fresh native `design`, `row_alignment` and the standalone CPU raster tests also
pass. Source build outputs, external payloads and Release app copies match by
SHA-256. The managed payload remains the preset-only editor below. Installed
Vulkan registration and installed layer files were not changed by this staging.

| Updated component | SHA-256 |
| --- | --- |
| native\cfx_osd_core.dll | `053F75691C6083FB5A29D2B07E1FC47AAA3F13708492383DC5C72CF27AA6CC5F` |
| native\cfx_osd_hook.dll | `89BCBE5E73AE886A24202A970D928B5201D7979525C386B172FD1AAF2E70C068` |
| native\x86\cfx_osd_hook.dll | `036F438C4CF13A90763E556BCC46D23542E08C78F4E651F233E548A3F432B371` |
| native\vk\cfx_osd_vklayer.dll | `248CB776107CFCCCD3781207CBDFE9BDABB3F71DD996FA091716A46D9DBF3AED` |
| native\vk\x86\cfx_osd_vklayer.dll | `5D82C539697E8A619804CE9C6FA7BEEBCAF36113ADE0881454D6C899159EEFF3` |

### Icon sizing and preset-only symbol selection (2026-10-12)

The Controls DLL was rebuilt in `Release|x64` for .NET 10 from the sibling
`CapFrameX.OSD` working tree at `265b991480f92fb4d18368d6540c70f54e896632`
plus the pending icon-editor fix. Icons now expose a size slider; canvas handles
and frame fields resize the glyph together with its frame. Slider changes respect
parent bounds and minimum dimensions, and each resize remains one undo operation.
Icons are selected exclusively from the existing symbol dropdown; the custom
glyph field and free icon-font picker are removed. All five embedded editor
catalogs and the English/Korean review copies include the size label and remove
the obsolete custom-symbol labels.

Verification: the Release Controls and prebuilt-only test builds pass, as do all
30 selected icon, localization and text-commit regression cases. The 11 icon cases
cover resizing at both canvas scales, padding, nested boundaries, undo, locking
and correct font/serialization when selecting a preset. An offscreen WPF rendering
confirms the simplified inspector and correct Network glyph (`U+E968`) in the
symbol dropdown and native preview. The external, app and test Controls DLLs
match by SHA-256.
Interop and all five native DLLs retain the previous payload below.

| Updated component | SHA-256 |
| --- | --- |
| net10.0-windows\CapFrameX.OSD.Controls.dll | `847F24980453E02C697A7F7AA25327A3180BFBE73AFAFA51B6ED5951A3EDE03D` |

### Full rebuild with Vulkan Auto SR lifetime fix (2026-10-12)

All seven bundled DLLs were refreshed from the sibling `CapFrameX.OSD` source tree
now committed as `265b991480f92fb4d18368d6540c70f54e896632`, including the Vulkan
Auto SR/ONNX lifetime fix and its native regression tests. The five native DLLs were
clean-built with VS 2026/v145 in `RelWithDebInfo`; Interop and Controls were rebuilt
in `Release|x64` for .NET 10. `CapFrameX.OSD.Integration` remains source-built in the
main repository and is not an external payload.

The Vulkan fix retains one ordinary reference to the already-loaded ONNX runtime
for the affected Intel Auto SR package, preventing its OpenVINO provider from
outliving that runtime. It checks the package path and loaded file versions and
does not preload ONNX. Real DOOM validation remains outstanding.

Verification: all 100 native CTests pass (16 core, 32 DXGI per architecture and
10 Vulkan per architecture). All 26 Vulkan ABI/lifetime integration cases pass
against these DLLs and again against the matching installed DLLs. The registered
x64/x86 layers are enabled in their matching HKLM registry views; each loader smoke
test completed 30 frames with three requested resizes and verified the loaded DLL
path. The seven external DLLs match the tested build outputs by SHA-256. Both
Vulkan manifests match the source manifest and each other; the English/Korean
review catalogs match their source files.

| Component at this rebuild | SHA-256 |
| --- | --- |
| native\cfx_osd_core.dll | `E051DD649C0669AB541341BA6B7E2084A6D15F2CEF24EB3C41BA24B4275117E2` |
| native\cfx_osd_hook.dll | `03E6EC5AA40A758C9BB6FAE30E9ABAB4D8596F195DAADC4C557454F9D773E077` |
| native\x86\cfx_osd_hook.dll | `FD5270CA2C9626163C8A3BCB758F40CA3FCA12384D7875A43248AC27C110248C` |
| native\vk\cfx_osd_vklayer.dll | `3B561A766B3A61E869A8E980868831F656523178D21FD9BE66D7F7DEF8522EB2` |
| native\vk\x86\cfx_osd_vklayer.dll | `AB4033F56290E3053E39D9A541A35B9ED53307DDA50CF4CC3DFA0EEC6686AF31` |
| net10.0-windows\CapFrameX.OSD.Interop.dll | `218867D9BE90E97B926F83AE1CB6528F67239DBBC06B60DB44363E36B550D3F3` |
| net10.0-windows\CapFrameX.OSD.Controls.dll | `C1D5ADF5789AFFF0BACAF806CFA21D685E476101AE700034785DA16A81ADAC53` |

These are unsigned development binaries. The entries below describe earlier payloads.

### Five-language overlay editor (2026-10-12)

The Controls DLL was rebuilt in `Release|x64` for .NET 10 from the sibling
`CapFrameX.OSD` sources committed as `89f86a02b4c97e83fac9e9515b778521dc05ee6c`.
Its embedded catalogs cover English,
German, Spanish, Russian and Korean: 447 editor strings and 148 gallery strings per
language, including the names and descriptions of all 68 templates. Language changes
refresh open controls and error messages without rewriting user profile names, overlay
text, source identities or pending text edits. The native and Interop DLLs are unchanged.

Verification: the Release source build and 304 selected CapFrameX regression tests pass,
including catalog/placeholder parity and preservation of profiles and pending edits during
language changes. Thirty WPF renderings cover the editor, sources, group inspector and
gallery in all five languages, including the 1080-pixel editor width. A prebuilt-only Release
application build with a fresh restore also passes; all seven bundled DLLs match that output
by SHA-256, and its restore graph contains no OSD source-project references. The normal
source-project restore graph was restored afterward.

| Updated component | SHA-256 |
| --- | --- |
| net10.0-windows\CapFrameX.OSD.Controls.dll | `1B33AAD249EB4A56B3497A4D081D25B9D34C001E51869DB67CBE2FCE2E70106D` |

This Controls DLL is an unsigned development build.

The English and Korean [editor and template catalogs](Localization/) are included beside
the prebuilt for public translation review. They match the resources embedded in the Controls
DLL. Update these copies when rebuilding that DLL; the editor loads its embedded resources.

### Composite tile editor development payload (2026-10-09)

The current binaries were built and verified from the sibling `E:\Code\CapFrameX.OSD`
working tree before committing. The corresponding source changes are now published on
`main` through `6faebc589e6fc57f836f54bcf101511c888d3afc`: native rich widgets in `542f107`,
composite editing and topology-aware bar layouts in `4d4debc`, and CPU architecture templates
in `6faebc5`. The only source edit after building was removal of a trailing blank line.
Builds use VS 2026/v145, native `RelWithDebInfo`, and managed `Release|x64`, .NET 10.

The payload adds editable nested containers, local grid drag/drop, layer order and locking;
independent metric typography, paired sources and duration/scaled-number formatting;
custom gauge arcs, minimal charts with value badges, and portable embedded PNG/JPEG images.
Chart overlap preserves foreground drawing order. CPU groups grow inside containers, and
three Turquoise presets bring the gallery to 62 templates. Existing row-overlay and RTSS
gating remain covered by the CapFrameX regression selection.

Verification: 1,600 editor checks and 1,159 native-preview checks pass; the CapFrameX selection
passes 763 tests plus 100 focused checks after final source-binding changes. Core passes 16
suites, DXGI 32 per architecture and Vulkan two per architecture. All five native backends
were rebuilt after the last renderer change, with six focused native suites rerun successfully
(331 design and 1,189 row-layout assertions). Actual production-designer mouse checks cover
container entry, local move/resize, locking, pulling a child out via Back, and Undo.
Reference/Compact/Wide previews were rendered with 8, 32 and 128 threads.
Before the caption correction below, Release source and prebuilt-only application builds
passed and all seven bundled files matched the prebuilt-only output by SHA-256; the
x64/x86 Vulkan manifests are identical.

The subsequent Turquoise Reference caption fix gives the frametime chart a separate,
editable `FRAME TIME` text element while keeping axis labels hidden. Native previews
still pass all 1,159 checks; the Controls payload and embedded starter scene were refreshed.
Already saved profiles remain snapshots and are not overwritten by template updates.

The subsequent inspector/gesture correction gives inherited and explicit fields stable
geometry, separates color inputs from the reset action, and accepts decimal points or
commas without thousands parsing. Empty optional colors restore inheritance. Root-canvas
move/resize can grow right/down within native size limits; legacy flow gestures convert
their measured geometry on commit, and layer-list drag uses the tile center. The editor
verification now passes 1,638 checks, including cancellation, undo and pixel-budget edges.
All 42 selected CapFrameX WPF tests pass, including ten new inspector layout/numeric-input
cases across light/dark themes and narrow/regular sizes. Actual mouse checks cover root
translation, horizontal resizing, layer-list positioning, Undo and the color section.
The refreshed prebuilt-only Release application builds successfully; all seven bundled DLLs
match its output by SHA-256. The native payload is unchanged by these editor corrections.
Debug/Release source builds also succeed. The ten new inspector tests additionally pass
against the exact Controls DLL recorded below.

The 2026-10-11 update defers design text bindings until Enter or focus loss; Shift+Enter
inserts a newline and source searches remain immediate. Pending text participates in save,
selection and close guards without changing the model while typing. Inspector text starts
at its label's left edge, removing duplicated padding and WPF's internal text inset.
All 57 selected WPF/host tests and 1,638 editor checks pass against this Controls payload.
Debug/Release source and prebuilt-only Release application builds pass; all seven bundled
DLLs match the latter by SHA-256. The normal Debug application output has been updated.

The subsequent bar-group update adds explicit member width/track height and a synchronized
Rows selector for either orientation. Exact member frames wrap without stretching; resizing,
caption changes and hardware source expansion use matching geometry. Invalid growth retains
the accepted preview and blocks saving until corrected. Existing profiles keep their legacy
packing until a dimension or Rows is edited. All 258 selected CapFrameX tests, 1,668 editor
checks and 1,159 native preview checks pass, including pixel measurements of all 32/128 bars
at 50%, 100% and 200% scale. The Controls DLL below contains this update; the native DLLs
and Interop are unchanged.
Debug/Release source and prebuilt-only Release application builds pass; all seven bundled
DLLs match the prebuilt-only output by SHA-256. The normal Debug output is updated.

These are unsigned development binaries. Real game compatibility and release signing remain
release checks; no installer or update-server release was made.

The manual height-fit follow-up hides the action for automatic bar groups and renames it
**Fit height to content** for other canvas groups. It uses unrounded content height,
retains real collision checks, and reports a successful fit only when geometry changes.
Nested fits tolerate floating-point boundary noise without allowing actual overflow.
All 119 selected regression tests and 1,668 editor / 1,159 native preview checks pass;
the 15 new fit-height cases also pass the final container-boundary assertion. Release
source and normal Debug builds pass. The Controls prebuilt below was refreshed from the
tested Release output; the normal Debug output contains the matching Debug build.

The width-resize follow-up makes horizontal tile resizing fit bar widths to the new inner
width while preserving the current columns and rows. Only shrinking below the minimum
one-pixel bar width plus gaps forces wrapping; direct **Bar width** edits still wrap normally.
Height-only resizing preserves the requested bar width. All 145 selected tests pass,
including actual resize-handle commits, nested groups, Undo and native pixel bounds after
stretching/shrinking. The 1,668 editor and 1,159 native-preview checks also pass. Release
source and normal Debug builds pass; Controls below is the tested Release binary and the
Debug output matches the sibling Debug build. Native and Interop DLLs are unchanged.

The initial template bar-layout follow-up balances sensor families after discovery, including
Vertical Thermal Panel's 16 effective clocks as 8 + 8. Gallery templates and built-in saved
profiles share editor/runtime geometry, prefer complete even rows, and retain explicit user
dimensions. Tests cover 8–128 sensors, odd sensor counts, nested tiles, scaling and the real
template/inspector path. All 154 selected cases pass (150 in the combined run and the four
inspector cases after correcting their assertion to use native text measurements), together
with 1,668 editor and 1,159 native-preview checks against the exact Controls payload below.
Release source and normal Debug builds pass. The Debug payload matches the sibling build;
native and Interop DLLs are unchanged.

The hybrid CPU follow-up preserves P/E/LP-E/D/LP topology through telemetry, editor members
and runtime binding. New hybrid bar groups separate core types into rows automatically;
8 P + 16 E clocks start as 8 / 8 / 8, while SMT loads retain every thread. **Separate core
types** controls the behavior explicitly; row selection, resize and Undo use the same
geometry as the native preview. Existing explicit layouts retain their settings.
All 266 selected tests pass, including four hybrid native pixel cases; the exact Controls
payload below also passes 1,668 editor and 1,159 native-preview checks. Release source and
normal Debug builds pass, and Debug Controls/Integration match their project outputs by
SHA-256. The prebuilt-only Release application also builds successfully, and all seven
bundled DLLs match its output by SHA-256. Native and Interop DLLs are unchanged.

The core-type template follow-up adds six **CPU architectures** templates and three saved
starter profiles: Core Types Overview, Core Types Compact and Core Types Thermal. Catalog
version 3 adds these profiles without overwriting edits, changing the active selection or
restoring deleted older profiles. The templates support the complete native core enum:
Standard, P, E, LP-E, D and LP; optional sensor families remain unbound when unavailable.
All 317 selected CapFrameX tests pass, including actual simulation sensor builders and
catalog migration. The exact Controls payload passes 1,752 editor and 1,219 native-preview
checks across 68 templates; thirty additional native renders cover five topologies.
Release source, prebuilt-only Release and normal Debug builds pass. All seven bundled DLLs
match the prebuilt-only application output by SHA-256; Debug Controls and Integration match
their project outputs. Only Controls is refreshed in this follow-up; the native and Interop
binaries retain their hashes.

| Component | SHA-256 |
| --- | --- |
| native\cfx_osd_core.dll | `C14AC5B90283EEA62BD2BF306C23DE93CA24334CAF5BA18056E6535A18A1BB0C` |
| native\cfx_osd_hook.dll | `DC236FA36F2B756D276CA09FAE02372D5E7D35028F4BD73894C64C901DE76D97` |
| native\x86\cfx_osd_hook.dll | `3503A72863D1F1AD1E6BE988C3A1EF1264ED2438D73D694D9AC44F57E97A0540` |
| native\vk\cfx_osd_vklayer.dll | `586727F9DDDB6C37582BC9F9E15FEB1CC6F101D235D00E6B25D880A9B60644A7` |
| native\vk\x86\cfx_osd_vklayer.dll | `8BE58112D93AB4EA4D754ABE360779D0E228A036F93D60B7BDADA0BEE758634E` |
| net10.0-windows\CapFrameX.OSD.Controls.dll | `27ACE6954C0994BA5270E4FDC2825162AC418C6918A81AE491A29E2238214CB3` |
| net10.0-windows\CapFrameX.OSD.Interop.dll | `F1A65ADA6720543CF1D01A04082EEA937B30B1E925D7BF0564F84F06FA302E07` |

Payload descriptions and hashes below are historical.

### Classic rows inside grid designs development payload (2026-10-09)

These earlier binaries were built from the source state now committed as
`c96c00296225a76b92ad6ab0c0fa3765558bc13d`, published on `main`.
Builds use VS 2026/v145,
native `RelWithDebInfo` and managed `Release|x64`, .NET 10.

The `classicRows` widget reuses the classic row builder in a fixed, movable tile. It preserves
the active profile's grouping, formatting, threshold colors, bars and graphs, with independent
classic value storage and a preview entry API. DXGI/Vulkan continue delivering classic entries
while a custom design is active. Empty/stale entry batches clear linked rows; classic mode
restores the latest batch immediately. Three hybrid presets bring the gallery to 59 templates.
CapFrameX deactivates design mode when RTSS is selected and prevents activation under RTSS.

Verification: 500 selected CapFrameX tests and 54 subsequent focused checks pass; 1,448 editor
checks, 1,129 native-preview checks, seven core suites (245 design assertions), 32 DXGI suites
per architecture and two Vulkan suites per architecture pass. The actual production designer
with isolated demo telemetry was checked by mouse for template selection, row-block resizing,
moving, Undo, saving and RTSS-disabled activation. Real game/RTSS validation remains a release
check. These are unsigned development binaries; release signing is still outstanding.

The subsequent saved-profile context-menu update passes all 552 selected CapFrameX
regressions and production WPF menu/dialog checks in light/dark themes at two window sizes.
Debug and Release source builds pass; that host update does not change these OSD payloads.

| Component | SHA-256 |
| --- | --- |
| native\cfx_osd_core.dll | `25682FA382E7B32B23EB90485B48A6BDE96E6915CE1D462C885B4E0C41EB8858` |
| native\cfx_osd_hook.dll | `E61F689AF9996618D577623C112DFC75C17F8481694DA3206F4F98EECDDC4D98` |
| native\vk\cfx_osd_vklayer.dll | `9C28EB395AEE3764B7945063B7A97BCBC30A2E0FD5557C386B0429642227D63E` |
| native\vk\x86\cfx_osd_vklayer.dll | `C14ECEF88FF256602381156B382187605D6827E9C3E2C8FB4B8700C993255485` |
| native\x86\cfx_osd_hook.dll | `239423E479AFEF5A61FA18259A1E1C63EDA6B4E9CC84736DA1642D87BACB627C` |
| net10.0-windows\CapFrameX.OSD.Controls.dll | `9DAD68813C22E749D8CCE7846F723BB1D3BF305DF72CB48AA29E1C3DD6AB1925` |
| net10.0-windows\CapFrameX.OSD.Interop.dll | `1CAC1CFC6ED8DE561A6A274E0785D74D961C19935B70465AF7AB3A92BFA0043C` |

Earlier payload descriptions below are historical.

### Vertical sensor groups development payload (2026-10-09)

The current submodule and managed binaries use OSD revision
`e490bdf250bb1eceb43f6d2d192df77a411b5acb`, published on
`codex/overlay-designer-1.9.2`. The five native DLLs were built at
`55ee5fa3206abe512c2aff68b5117bf1814fb5c0`; native sources are unchanged between these
revisions. Native builds use VS 2026/v145, `RelWithDebInfo`; Interop and Controls use
`Release|x64`, .NET 10.

Sensor groups support vertical bars, optional numeric values, 24–240 px track heights,
up to 32 responsive columns and collision-safe frame fitting with Undo. Four new vertical
hardware presets bring the gallery to 56 templates. Existing horizontal profiles retain
their serialized scene and appearance. Vertical captions scale with the document and runtime
zoom; hidden-value bars distinguish unavailable readings from measured zero.

Verification: 133 focused CapFrameX tests, 1,342 editor checks, 852 native-preview checks,
seven core suites (202 design assertions), 32 DXGI tests per architecture and two Vulkan
tests per architecture pass. Actual DX11/DX12/Vulkan test hosts render the vertical design
on x64/x86, including invalid updates, visibility, PID mismatch and expiry. Vulkan pixel
readback verifies missing/zero distinction. Mouse checks in the production designer with
isolated demo telemetry cover template selection, values, collision rejection, Undo and save.

This remains an **unsigned development payload**. The previously recorded Certum private-key
access issue is unresolved; release signing is still required. No installer or update-server
release was made. Payloads documented below are historical.

| Component | SHA-256 |
| --- | --- |
| native\cfx_osd_core.dll | `CD329E532A6664D7797D28F55D84B01573B56882C80251BC6023194745683DB6` |
| native\cfx_osd_hook.dll | `6A6E376967FC03E28DA754DE34F81EE9F5A57A4910331C4C6888689508C47B43` |
| native\x86\cfx_osd_hook.dll | `1C8B5F6F42E39EB0849B97BC52AB5E0115000020F2E6B52DB8C0BBDE4C423F4D` |
| native\vk\cfx_osd_vklayer.dll | `1C0DDE67D59F49D7D235E3C0BB2903790BCDD49C64822C9EC991A108C36F3A11` |
| native\vk\x86\cfx_osd_vklayer.dll | `2C1AA3D0EE5909516291B9FB25CCFC7DE0F09904E359EE08A69C967615C01B23` |
| net10.0-windows\CapFrameX.OSD.Interop.dll | `0BD90A444A7B047219FA00FDFECD53C721170C137A0FAA65825F12A26255D9AA` |
| net10.0-windows\CapFrameX.OSD.Controls.dll | `EC8D1C0F0F867FA465E618F86078879F5DF6983F25E3CC36F4F8D95AC1F06AEE` |

### Fine-grid designer and runtime designs development payload (2026-10-09)

All seven binaries below are built from OSD revision
`468d396ee724a94ea7119ac871e57e39379256b0`, published on the private repository's
`codex/overlay-designer-1.9.2` branch. Native builds use VS 2026/v145, `RelWithDebInfo`;
Interop and Controls use `Release|x64`, .NET 10.

The editor provides independent canvas tile placement/resizing, configurable 1–128 px grid
cells, snapping, resizable sidebars, a wider grouped source picker and 52 templates. Four
hardware-group presets bind complete CPU sensor families, including 32/64/128-member layouts.
CapFrameX exposes saved designs under Overlay → Designs and supplies the full telemetry catalog
to hook-free, DX11, DX12 and Vulkan output. Design and metrics are applied atomically through a
bounded, PID-specific, heartbeat-checked channel. Runtime placement and zoom remain effective;
classic entry rendering and the legacy template/entry API remain compatible.

Verification: 129 focused CapFrameX tests, 1,121 editor checks, 629 native-preview checks for
52 templates, seven focused core suites (134 design checks), 32 DXGI tests per architecture
and two Vulkan tests per architecture pass. Live DX11/DX12/Vulkan test hosts verify rendering,
invalid-update retention, visibility, PID mismatch and expiry. Vulkan GPU readback confirms
zero overlay pixels while hidden/retargeted/expired and correct 200% zoom. Actual mouse checks
cover grid conversion, cell size, independent tile/group drag/drop, edge resizing, sidebar
resizing, undo, saving and activation in the production designer with isolated demo telemetry.

This is an **unsigned development payload**. Certum signing was attempted, but SignTool could
not access the existing certificate's private key (`After Private Key filter, 0 certs were
left`). No replacement/self-signed certificate was used. Release signing remains required;
the previous signed payloads below are historical. No installer/update-server release was made.

| Component | SHA-256 |
| --- | --- |
| native\cfx_osd_core.dll | `92D2ECDBADA5A7657AEB10051EE60759C2037C2D6F40D4D662C0BC578AB6CF20` |
| native\cfx_osd_hook.dll | `F4CD128DA7D3D1A2597904DD58919BE83A6A64D4928C8663D3AFAA26CE0BF868` |
| native\x86\cfx_osd_hook.dll | `50FD0D7A7D8F87AADF28BE34E68D24E058AAAB6E161F48A7A080470127891450` |
| native\vk\cfx_osd_vklayer.dll | `A881493061ED3CA60C3938F5A30B0F2F3A94E675AB03EC1EFF769BC105BD0E46` |
| native\vk\x86\cfx_osd_vklayer.dll | `E260C7778665205E3B1DE4B5CFE25A9A744639D82C9A21462C585FFDC3771898` |
| net10.0-windows\CapFrameX.OSD.Interop.dll | `2D2838791825DBD9974A9190F7BD831F35B588E01CAA4F73F8F6D4CF02F991CC` |
| net10.0-windows\CapFrameX.OSD.Controls.dll | `C15849BFB51E26D096CECD90CD87D8CC9D6B11E4E1CA4AE6C8B9E26D6311D2B9` |

### Overlay designer templates and grouped sources development payload (2026-10-08)

The five native DLLs are built from OSD revision `08d648d12d6870de6ea0c174027c55848787790a`.
Interop and the newly included WPF Controls assembly are built from
OSD revision `532ce0a2cc3c032a3c22dde37d6602bc58405b06`, which adds grouped telemetry tiles,
profile-host integration and fixes expanded source headers under the CapFrameX MaterialDesign
theme. Native sources are unchanged. The local branch is
`codex/overlay-designer-1.9.2`; publishing that OSD revision is required before sharing a
source-based submodule checkout. The prebuilt path is independently usable.

This unsigned development payload supersedes the signed binaries listed below. It adds grid
and horizontal panel composition, styled tiles, hero metrics, bars, configurable gauges,
live native preview, validated custom templates and the reusable visual editor. The editor now
supports direct preview drag/drop, measured hit areas, resize handles, source binding and a
gallery of 48 original templates, including 24 Troy-inspired layouts. Source categories,
search, text telemetry and fixed multiline metrics complement stable resize previews which
commit once on release. CPU load/clock groups support up to 128 readings in one configurable
tile, with shared styling and individual member selection. The host now supplies saved-design
management; activation and template transport to games are deferred to the later Overlay-tab integration.

Builds: VS 2026/v145, `RelWithDebInfo` (embedded `/Z7` debug information for the local build
environment), and `Release|x64` .NET 10. The profile/group update passes 962
editor/template/gesture/gallery/source checks and 536 native-preview checks covering all 48
templates and 32/64/128-member groups. Native code is unchanged since its prior verification:
127 native design checks, four focused core suites, all 30 DXGI tests per architecture and both
Vulkan tests per architecture. The grouped native previews and actual WPF source hierarchy were
rendered offscreen and visually checked under light/dark application themes; stable
pointer/row-edge behavior is covered by gesture regression tests. All 84 focused CapFrameX
profile/telemetry/WPF tests pass. Details are recorded in
[`dev-plans/CapFrameX_1.9.2_Overlay_Designer.md`](../../dev-plans/CapFrameX_1.9.2_Overlay_Designer.md).
Staged hashes match the build outputs. Release signing and real-game design
activation have not been performed.

| Component | SHA-256 |
| --- | --- |
| native\cfx_osd_core.dll | `7058A745D9C300807B156C6A2CE1CE6925A5FA9A22EECED7CF781ABF4F6B6A89` |
| native\cfx_osd_hook.dll | `E4ED81F971B558416A3D6EFD3C50A7341A6E5AB8CF37CFD248542A1FF3A21BC3` |
| native\x86\cfx_osd_hook.dll | `FDC814542639B5D10E6F4B9A046170770F8B11C93F990DD580609EC5E7331E49` |
| native\vk\cfx_osd_vklayer.dll | `073A3D8A3A18C73E928114C0D4EE53CD0BC9813E42A73BB53B44E0DB7468974B` |
| native\vk\x86\cfx_osd_vklayer.dll | `0E9662EA7831E32532F55021894F4881B6E95768055D352E41113538F72CEB17` |
| net10.0-windows\CapFrameX.OSD.Interop.dll | `C20E6B2412E2D6BAB782554C81D91547B60A46A375E48C900085A3D4B36AC59D` |
| net10.0-windows\CapFrameX.OSD.Controls.dll | `01BD4CF934CFE95BB8315D5857C42D8CE65EB77A5A452C98F243D606461CAC10` |

### Signed 1.9.2.1 Beta payload (2026-10-07)

All five native DLLs and the managed bridge were rebuilt from OSD revision
`153cc1a8eea614151a001d0875381d084732a2bb` for the update-server build **1.9.2.1-beta**.
Fresh VS 2026/v145 `RelWithDebInfo` builds and a `Release|x64` .NET 10 bridge passed
all 79 native tests (15 core, 30 per DXGI architecture, 2 per Vulkan architecture).
PE architectures and identical Vulkan manifests with relative library paths were verified.
All six binaries carry valid Certum signatures from
`C0D5481E2ACBB9DD104A825A81EECF55557BA783` with RFC 3161 timestamps.
The signed app-output files were copied back here without rebuilding them.

| Component | Signed SHA-256 |
| --- | --- |
| native\cfx_osd_core.dll | `81f6aad2c34305f18f865279568a468fb41080248094da2a3c485b6167a87936` |
| native\cfx_osd_hook.dll | `3214ebd7cda740550405a41c98bcd2d0dffa9259526a33cd77865ba09466ba33` |
| native\x86\cfx_osd_hook.dll | `5818a666a3adfe627418040929572af97457329e66a5a6daf28e6923ff91868d` |
| native\vk\cfx_osd_vklayer.dll | `379c49defdf00215299747011bc8f22b200e8bdb0c152e8d5641883696e2715c` |
| native\vk\x86\cfx_osd_vklayer.dll | `64cf83dc4ced545f3d0b0572b5a04fdade95cf299a24e6bba73ecddd1d41273f` |
| net10.0-windows\CapFrameX.OSD.Interop.dll | `532861ff2d38e32018187c6cc7ab52166bd9c99b1a0b43582e5b3d59def630f0` |

### Percentage bar development payload (2026-10-06)

The five native DLLs and managed bridge were rebuilt from the source tree recorded by
OSD revision `153cc1a8eea614151a001d0875381d084732a2bb`, which adds percentage bars.
These were unsigned development binaries, superseded by the signed 1.9.2.1 payload above; the older signed beta payload below is
historical. Native builds use VS 2026/v145 and `RelWithDebInfo`; interop uses
`Release|x64`, .NET 10. The submodule is pinned to the same OSD revision.

Percentage entries support text, bar, or text followed by bar. Native entry API V2 is
additive and preserves the legacy struct/API; the in-game metrics channel accepts V5
(380-byte records, display mode at byte 376) plus V1-V4 as text. Bars clamp their fill
to 0..100, preserve the raw value, use the value's threshold color and smoothing, and
keep column widths across missing samples. DXGI and Vulkan x64/x86 share this renderer.

Validation: four focused core suites (including 1,193 row/raster checks and versioned
entry/missing-sample scene checks), all 30 DXGI tests per architecture, and both Vulkan
tests per architecture passed. All five native trees were rebuilt after the final layout
fix and the four core suites passed again. The percentage-bar CPU raster was inspected.
Staged files match build outputs by SHA-256 and PE architecture; Vulkan manifests remain
byte-identical. Real-game visual checks and release signing have not been performed.

| Component | SHA-256 |
| --- | --- |
| Core x64 | `bd8058bcd82657323b7f9b82b33fddf9107a60f4857415539c66e93e0c2ded86` |
| Hook x64 | `cc0c663e2ee16b1e28c76054ff2cba9cc0c9193c4061ae6440428f26bb3e1e82` |
| Hook x86 | `a9c139cd20da752d410ceb1932b063ffeaee5b610f3744d546b046055a36ebaa` |
| Vulkan x64 | `a20d07a404071337448efb0b079d1b43182e1ca6b65a11e18c6bc494c784d269` |
| Vulkan x86 | `e1d569b375f504aed7127abe827b0fdba933d5a42c2fa29d853650010f94aa93` |
| Managed interop | `b55ac7bfdc8022f75b5a5bd03d49fabbc66e3ca2b6303cdb832fe46159ce67e1` |

### Signed 1.9.2 Beta payload (2026-10-04)

All five native DLLs and the managed interop bridge were rebuilt from OSD revision
`9f82ac1fd6651a0c8dd0671045c54fa87cb91ca5` for CapFrameX **1.9.2.0-beta**.
Native builds use VS 2026/v145, Windows SDK 10.0.26100.0 and Vulkan SDK 1.4.335.0,
`RelWithDebInfo`; interop uses `Release|x64`, .NET 10. All **79 native tests passed**.
The six final binaries carry valid, timestamped Certum Authenticode signatures from
SimplySign certificate `C0D5481E2ACBB9DD104A825A81EECF55557BA783`.
The signed copies match the packaged MSI and portable payloads. PE architectures
and byte-identical Vulkan manifests were checked. Earlier hashes below describe
historical builds, not these signed beta files.

| Component | SHA-256 |
| --- | --- |
| Core x64 | `f2e2f6e9be14e7ef72a1f95845ec68befda89074d7a38b147fa6bb720f7146ef` |
| Hook x64 | `d65fc21685971486689514afed4a648ec07f7edfea50d359d899a4c188659aa8` |
| Hook x86 | `f28eed3fd512795ca4ee90f5ff3e03834a34df3c8f2a1fed49c65362d31dbe13` |
| Vulkan x64 | `4688d93bfd19e957f94530cdbb6110e6a8728405fec87d66ee67087ce98f57d2` |
| Vulkan x86 | `59a52fa26b6e270555413c522ed3b6dc1b9827ec38d5df47616abf6fc342aa21` |
| Managed interop | `e0f29cb46497c20db6bf2555ed99420573dbf7229880b797dba4b6d00364f9f9` |

### Previous build (2026-09-26)

All five native DLLs (core, hook x64/x86, Vulkan layer x64/x86) were rebuilt on **2026-09-26**
with VS 2026/v145 (MSVC 19.51.36260.0), Windows SDK 10.0.26100.0 and Vulkan SDK 1.4.335.0 in
`RelWithDebInfo`, from private OSD revision `9f82ac1fd6651a0c8dd0671045c54fa87cb91ca5`, which
contains the frame-cadence, early-attach install-race and chart-motion changes below.

### In-game chart motion (2026-09-26)

The in-game frametime chart now moves like RTSS draws it: the chart geometry is rebuilt at most
every 10 ms, always on a game present, and between two rebuilds the cached chart scrolls by whole
pixels at every present, so it stays sharp and meets the next rebuild pixel for pixel. D3D11
shifts the cached coverage mask in its chart shader. The D3D12 hook and the Vulkan layer, which
previously moved the chart only with a 30 Hz full-panel raster by one or two pixels at a time,
now receive pre-scrolled copies of each chart from the core (`cfx_osd_create_cpu_charts`) and
upload, per present, only the copy the chart clock selects; the panel text keeps its 5 Hz raster.

Two defects surfaced on the way and are fixed: the core's producer loop re-anchored its clock on
every timer wake without a present, so rebuilds drifted off the presents and each was handed
~10 ms too little wall time (the PresentMon replay would have run at ~0.86x on D3D12/Vulkan); and
the Vulkan layer rebuilt its textures for the whole intro fade whenever a panel frame raced the
staging ring's re-registration (27 rebuilds at 90 FPS, now one).

Verified with CapFrameX.Test's new `verify-osd-chart-motion.ps1` (D3D11 x64/x86, D3D12 x64/x86,
Vulkan x64/x86 at 60/90/144 FPS): the chart is placed at every present, keeps real time
(0.99–1.01x), never moves by two or more pixels at once (0 of ~9,500 presents) and is rebuilt at
`fps / ceil(fps / 100)` per second on presents. The frame-cadence, D3D12-native, late-attach,
FSR/XeSS routing, XeSS application-native, Vulkan learning and HDR suites pass unchanged. Cost at
144 FPS (presentStats, median of the per-10-s percentiles, test applications): about +8 µs per
present at p50 and +10 µs at p99 on D3D12 and Vulkan, split roughly evenly between writing the
chart copy and recording its upload; D3D11 about +2 µs.

All five staged DLLs match their build outputs by SHA-256 and PE architecture. The Vulkan
manifests remain byte-identical, and the signed managed bridge was retained (the new C API
functions are additive; the existing exports are unchanged).

| Native DLL | SHA-256 |
| --- | --- |
| Core x64 | `99A3F3D982B3427B763B6D1B7551A94DFBFCCEB2973744BF84CEE3283BD9B04C` |
| Hook x64 | `894035AB562CAC921DEDF1A7628AA6BF4EB49E15C06F0526A4792FD66A2325C5` |
| Hook x86 | `424267C4F809A40B1528C8717A5BE7F3747F4F75E08251300B88204FAC181F67` |
| Vulkan x64 | `1B5C422429A6E3EEA381700DF0C05D6A2A0B5EAC1681374FEB368F6D32D60A48` |
| Vulkan x86 | `09E790BD98995428ED9332954ACF6773FCD47D22D453CB62A4EB953F6FA1F5AC` |

### Early-attach install race (2026-09-25)

A vendor runtime loaded after injection is covered by two routes: the executable's
`GetProcAddress` IAT detour (delay imports included) hands out a detour synchronously, and the
loader-notification worker inline-patches the exports. When the worker was inside the API install
lock at the moment the game resolved an export, the route returned the untouched export; the call
then ran before its inline hook was enabled. For XeSS-FG's one-shot `InitFromSwapChain` the exact
queue was lost and the in-game overlay stood down for the whole session — about one
early-attached run in three. The route now waits for the worker (bounded to 2 s) and hands out
the inline-patched export; XeSS-FG resolves its exports before taking the lock, as FidelityFX
already did, so no loader-lock cycle is possible. Same change for FidelityFX `ffxCreateContext`.

The integration-test switch `CFX_HOOK_TEST_API_INSTALL_RACE_MS` forces that interleaving. With it,
a build with the former immediate fallback missed the initialization in 3 of 3 runs; the staged
hooks passed the new `early-attach-install-race` cases of `verify-osd-xess-routing.ps1` and
`verify-osd-fsr-routing.ps1`, and 10 of 10 unforced XeSS-FG early attachments (3 of them had to
wait for the worker, 15–78 ms).

All five staged DLLs match their build outputs by SHA-256 and PE architecture. The Vulkan
manifests remain byte-identical, and the signed managed bridge was retained.

| Native DLL | SHA-256 |
| --- | --- |
| Core x64 | `DD11B9A47D5E71278C79F2762FA628AEEC6A345FECC86F19D37ECCFB9A658DBC` |
| Hook x64 | `A0B9AD7B144AB1C44BD298F09E5A182304584229C3A06E503DAB645E3C4016A0` |
| Hook x86 | `B23D7203AE69B1B1287A6D7A23C21B04C8453812B60544D39859DA315A798122` |
| Vulkan x64 | `128E800A52E2D179EEC1BCFE6280981D64D24A46BAFBDB28364AC80F349DA8CF` |
| Vulkan x86 | `3A64FC25EA2A55F0744F7D674EBA0A81C6FEE2B321CDA5A263096AD044FD32D1` |

### Frame cadence under frame generation (2026-09-25)

The in-game overlay's local graph source (the hook's present ring, CapFrameX's default) mixed
two cadences whenever it drew on a frame-generation proxy. On Dying Light: The Beast with FSR 3,
the hook drew once per application frame on the FidelityFX proxy Present while the runtime
presented twice natively from its own thread, and every Present of either kind timed the ring:
Current FPS read ~300 for 200 displayed frames per second, and the graph advanced by only the
newest interval per draw, crawling at a fraction of real time (about 1/multiplier with MFG).

The ring is now timed by every native DXGI Present, nested in a proxy call or not (what
PresentMon counts); proxy Presents stand in only when no native Present arrived for 250 ms. Each
draw feeds the graph every frame recorded since the previous draw, and the ring is locked because
the runtime's presenter thread and the application thread tick concurrently. The Vulkan layer
only picked up the locked ring and a renamed accessor; its behaviour is unchanged.

All hook and Vulkan CTests passed in both architectures (29 + 2 each), including the new
`present_cadence` unit test (2x, 3x, 4x, 6x frame generation, passthrough, topology switches,
stalls, concurrency, and a negative control that reproduces the old behaviour). The staged hooks
passed the new `CapFrameX.Test` suite `verify-osd-frame-cadence.ps1` (12 cases: controls,
Streamline 2x/3x/4x/6x on x64 and x86, passthrough, real FSR 3 on the FidelityFX proxy with and
without toggling, real XeSS-FG): graph 1.00x of real time and the native framerate in every case.
The other OSD suites (D3D12 native, late attach, FSR/XeSS routing, XeSS application-native,
Vulkan queue families, Vulkan learning) passed with the same build. The staged hooks carry this
change together with the install-race change above; hashes there.

### Antialiased chart lines (2026-09-24, superseded)

The aliased one-pixel line turned every gentle slope into visible stairs. Charts now draw a
one-physical-pixel line with analytic antialiasing, identical on every path: the points are
rasterized on the CPU into an 8-bit coverage mask (x snapped to the centre of its column, y exact,
round-capped strokes with a one-pixel box filter across them, overlaps unioned with MAX). The
D3D11 paths (hook-free window, in-game D3D11) upload the mask into an R8 texture and composite it
once; D3D12/Vulkan composite the same mask through a Direct2D A8 opacity mask. Gamma-encoded SDR
targets use `coverage^(1/2.2)`, so the faint edge pixels keep their linear-light brightness and
the line stays continuous; linear (HDR) targets use the raw coverage. Snapping y to pixel centres
as before would keep the stairs even with antialiasing, so y is no longer snapped.

`OsdDebug.json` gains two optional knobs, read at every overlay activation (also inside the
game process): `chartLineWidth` (physical pixels, 0.5..4, default 1) and `chartLineGamma`
(1..3, default 2.2; 1 = raw coverage). Out-of-range values fall back to the defaults.

All **18 selected CTests** passed, the same selection as below. The raster tests now check
the coverage profile (one row on a pixel centre, an even split between two centres, one pixel
of coverage per column at every phase), smooth slopes without steps, no accumulation at joins
or retraced segments, and GPU/CPU composites against the shared mask.

Measured per chart against the previous build (same benchmark, three alternating runs,
medians; scrolling 5 s window, 60–500 FPS, 100–200 % zoom): a new chart generation costs the
present thread 1.0–4.0 µs CPU (previously 2.8–5.0 µs) and 5.0–5.9 µs GPU (5.5–6.8 µs); the
cached composite per game present is unchanged at 0.07–0.14 µs CPU and 0.05–0.17 µs GPU. The
producer thread spends 12–64 µs on points and mask (5–17 µs on points and vertices). The
Direct2D producer (D3D12/Vulkan) takes 97–213 µs per chart (146–371 µs).

All five staged DLLs match their build outputs by SHA-256 and PE architecture. The
Vulkan manifests remain byte-identical, and the signed managed bridge was retained.
In-game appearance still needs runtime verification after restarting CapFrameX and the game.

| Native DLL | SHA-256 |
| --- | --- |
| Core x64 | `DD11B9A47D5E71278C79F2762FA628AEEC6A345FECC86F19D37ECCFB9A658DBC` |
| Hook x64 | `53EE6B7750DBE9AB0832A2455A5A8C10C2A1919457D28B0A0D4FC0760F3673ED` |
| Hook x86 | `C6E88A8F51EDE3D636468358B23EBB6E6015A27BC3D586E5C5ACD5FA98EF1310` |
| Vulkan x64 | `A51B0AE5E124C3ED661E4412D7370FF3F35C9DDF3A4702070C40DF2DB2341EC9` |
| Vulkan x86 | `ABCE1172BBE5E8DB37A09C360CC47EBA275C49671D3CBB060DF1F059BB7E28EB` |

### One-pixel chart lines (2026-09-23, superseded)

Charts now use a hard, one-physical-pixel line at every UI scale. The hook-free/D3D11
renderer draws native LINELIST geometry with multisampling and line antialiasing disabled.
It retains the cached R8 MAX coverage mask and composites each chart once, avoiding
opacity accumulation where segments meet. D3D12/Vulkan use one aliased Direct2D path
with flat caps and bevel joins. Text antialiasing is unaffected.

Points snap to pixel centers. Time advances the chart in whole-pixel steps, and min/max
selection uses one physical pixel per column, preserving spikes. The compositor scroll
path explicitly uses nearest-neighbor sampling. Diagonals intentionally have pixel steps;
there is no soft fringe or round extension of the line.

All **18 selected CTests** passed: eight core tests on x64 (GPU and WARP raster,
CPU raster, geometry, row alignment, graph source, framerate graph and HDR composite),
four chart tests on x86, the D3D12 shader test on both architectures, and both Vulkan
tests on both architectures. Raster tests check one-pixel horizontal/vertical lines,
binary coverage, flat endpoints and constant opacity across fractional input positions.

All five staged DLLs match their build outputs by SHA-256 and PE architecture. The
Vulkan manifests remain byte-identical, and the signed managed bridge was retained.
In-game appearance still needs runtime verification after restarting CapFrameX and the game.

| Native DLL | SHA-256 |
| --- | --- |
| Core x64 | `71E0E8AC1B258415912C24D202F888EAAF1FCF41EB2E5704440334F706C02204` |
| Hook x64 | `45A25C58959A88ED8D587C79BEE5A7D918B9E083BF94368C8ACA6049CB921FBD` |
| Hook x86 | `69CE75A382FF18F80EEAF2D2F7ED93F506D1BFCC61A4ECE5A427865B8DE9A94D` |
| Vulkan x64 | `9BA80B9F60715ADC30DE389B37BCA01DA2540381DA037B9EA484B5396E13690B` |
| Vulkan x86 | `CBC2207409AEE0B52F12597C399D6A3454C0979F200F4B2C60509D9B145F33E5` |

### Chart coverage and joins (2026-09-23, superseded)

This earlier correction removed excess blending but retained soft, two-pixel lines.
Its coverage-mask approach remains in the current build; its rounded geometry does not.

The D3D12/Vulkan CPU renderer stroked each chart as one Direct2D path with round
joins. Separate segments previously accumulated opacity at spikes and changed edge
coverage when a straight line was subdivided.

The hook-free/D3D11 GPU renderer used round segment geometry, combined coverage in
one R8 mask per chart with MAX blending, then composited each chart once. This removed
the old triangle strip's overlapping, narrowed spikes. Masks are reused between geometry
uploads. Sample values, time mapping and column-extreme selection are retained.

All **18 selected CTests** passed: eight core tests on x64 (GPU and WARP raster,
CPU raster, geometry, row alignment, graph source, framerate graph and HDR composite),
four chart tests on x86, the D3D12 shader test on both architectures, and both Vulkan
tests on both architectures. All staged DLLs match their build outputs by SHA-256 and
PE architecture. Both Vulkan manifests match the build outputs and remain byte-identical.
The signed managed bridge was retained. No installed binaries or layer registrations
were changed; these payloads are picked up by the next CapFrameX build.

| Native DLL | SHA-256 |
| --- | --- |
| Core x64 | `6F4CCD94B8D5FD1D88923D12149303E9C596D94DCE171A8A72D460269DF6317C` |
| Hook x64 | `DB7DFFD2EEE8122760CB8952E529DD8BC450FDDE5A76DA632A6079A989964797` |
| Hook x86 | `C77695ECB4E3AB7A81A77400C8CDA6FAF65E200EED4EF1EC910C064617D43AE6` |
| Vulkan x64 | `10DF0A828439DABFEE1A4E798D59A258A51A55F026225A99F9B3FCA929235275` |
| Vulkan x86 | `6BE64FBD8BF56E41721AAEC5C5542379D03CC9D2A79B89883CF01BF0F1F9EFB9` |

### Previous combined rebuild (2026-09-22)

All five native DLLs were rebuilt on **2026-09-22** with VS 2026/v145 in `RelWithDebInfo`,
every tree with `--clean-first`, from the source state now committed as private OSD revision
`c8926eb549c66034ac72e62ff678eecd8c64bc20`: the compact value columns with their minimum
column gap, refresh-interval numerics, value-smoothing clock and antialiased chart lines
described in the next section, and in both hooks the render-progress changes described after
it.

These native payloads are **unsigned development builds**. The existing
`net10.0-windows` managed bridge retains its Certum signature from the rebuild below; its
only source change since then (`d801775`) is a documentation comment.

### Compact columns, refresh-interval numerics, value smoothing, chart lines (2026-09-22)

**Compact value columns.** A value column reserves only what its own values print. The
integer places follow the value at scene build and grow, never shrink, when a live value
needs another place; the grown widths survive scene rebuilds. Each column reserves only its
own widest unit instead of `MT/s` in every column. The label gap is 12 px (was 18 px) at
100 % zoom. Value columns stay at least three spaces of the value font apart (about 13 px at
the default 16 px font, formerly a fixed 8 px): where a column's widest unit meets the next
column's widest number (`5600 MT/s  2800 MHz`) the gap has to read clearly wider than the one
space between a number and its unit, and it now follows the value font scale. The test
renders are 17-27 % narrower than before. This replaces the uniform `MT/s` unit reserve and
the five-digit minimum of the 2026-09-20 section.

**Refresh-interval numerics.** The hook and the Vulkan layer no longer refresh
Framerate/Frametime at a fixed 10 Hz (Frametime used to be the newest single frame). Both
rows, and Displaytime in PresentMon mode, are the means over the interval between two
CapFrameX publishes and change together with every other metric
(`hook_poc/src/numeric_frame_means.h`). An interval without samples keeps the previous
means. The present frame ring holds 1024 samples. The hook-free overlay does the same in
`OsdOverlayBridge`.

**Value-smoothing clock.** Value smoothing advances by the time since the values were last
drawn instead of the render-loop tick. The hook-free panel is drawn once per refresh and the
in-game text layer every ~200 ms, so the values lagged their targets before: in-game with a
~1.4 s time constant, hook-free at 20 Hz and above by 40-90 % of each change per refresh.

**Antialiased chart lines.** Charts plot at most two points per column as wide as the line
(2 px x zoom): each column's minimum and maximum in the order they occurred. Columns are fixed
in stream time, so scrolling only moves the line instead of re-sampling several frames per
pixel, which flickered; spikes are kept. The D3D11 chart pipeline (hook-free window and the
in-game DXGI hook) draws one mitered triangle strip per chart (two vertices per point instead
of six per segment) and the pixel shader derives coverage from the distance to the centre
line, which antialiases the edges without MSAA. The CPU producer (D3D12 hooks, Vulkan layer)
keeps Direct2D's antialiased segments with the reduced points. `cfx_osd_chart_perf_bench`
measured, against the previous renderer on the same GPU: geometry CPU time 3.3-27.4 us ->
3.2-6.6 us per chart, GPU time per chart draw 0.31-2.19 us -> 0.35-0.49 us (only 60 FPS at
100 % zoom was not faster: 0.31 -> 0.35 us, a single measurement), CPU raster 282-1561 us ->
~230 us per chart at 60-500 FPS.

All twelve core CTests passed (`row_alignment`: 764 checks; `chart_geometry` rewritten for the
strip and the column reduction), as did the CPU raster test, 28/28 hook CTests per
architecture (including the new `numeric_frame_means`) and 2/2 Vulkan CTests per
architecture. The staged DLLs match their build outputs by SHA-256 and PE architecture; both
Vulkan manifests are byte-identical to the build output and unchanged. Not yet verified in a
game. Restart CapFrameX and the game to load them.

Input SHA-256 at build time:

- `src/core/ChartGeometry.h`: `C38C12478C5F60A8FD0DD40235FDC1029A5D2E683B3FBFA49722D94C7CA07D70`
- `src/core/ChartLayer.cpp`: `75E0E78D7F512C3C1E4DE297803C0D3B92D8FDC9B0FA4F192A1603BBCD1420E5`
- `src/core/ScrollChart.cpp`: `BA38EAC089C5CACE117B3E4B806C9861D2C4190DE41EF417A74A061380A1EA6A`
- `src/core/Widget.cpp`: `397E1473967F627B26DFA5CD7E4B8DA4B2049B144B523DCBC0C732966FF87E77`
- `src/core/Widget.h`: `E5C67EC72FCC2B2629F7C2B711AA11FFD7E50C1ED81F0ACC27C8D16C970042D3`
- `src/core/OsdInstance.cpp`: `1EB78FE41AE8BB7AC1B3F0639686D51A66C4416014D646E1C4AC4CF7A4C49B41`
- `src/core/OsdInstance.h`: `FF04282367D48A9FB6FC9BB262E0897A2DBB9BB7D60EF493E5CCDF9DAF67FC09`
- `hook_poc/src/frame_ring.cpp`: `7E8ABB90A1E990067D8AD54155F65C8F11BA1C1797E5544E7C515D9A9C9E9B07`
- `hook_poc/src/numeric_frame_means.h`: `6610D1337B43C1195BD4AF44A4BAEF240E6C1CDEBF75CD3F6E04C0C64892319B`
- `hook_poc/src/overlay_osd.cpp`: `80ED79A99D4C13D754EE622C41D3F86B0E27D18EC31D731DBED9C24B2A9FE96D`
- `hook_poc/src/hook_status.cpp`: `328FEE23204B7F982E13FFE7F3F7179E816C59DFD88B6A59E4D409DD9CDDD91A`
- `vk_layer/src/osd_feed.cpp`: `5FA9F049C98026B7EC53B36F3B3D961D9A197D2CF1B158A21E45EE8D60F8E0F3`

| Native DLL | SHA-256 |
| --- | --- |
| Core x64 | `55C312C7F283C22CC56B43115DA9C2227871B17A4D0A11A06A4F68EC7FB03305` |
| Hook x64 | `637742252FF157B2985FE7362EBAF2DC29C66E40B0C4780FC51AF87B5053D1B0` |
| Hook x86 | `7FFA74EB1746924F0B00FC07743E4E09165FC35753A991516BC5FBA7757C0425` |
| Vulkan x64 | `C89E597CD37B396E7CE8228FFABC03EE5195C14AF77AEAFF06E31A5537820403` |
| Vulkan x86 | `A554CFE91CD32E9467A58F8CCE254B612AE1BE2F278C166AE483AE12527289B0` |

### Render-progress diagnostics (2026-09-22)

The hooks publish a separate, coherent 64-byte progress channel with cumulative
Present/draw counts and the last successful draw's context. The host uses it to
verify ongoing rendering and to collect compact opt-in diagnostics. Existing
native status mappings remain byte compatible. See
[overlay diagnostics and rollout](https://github.com/CXWorld/CapFrameX.OSD/blob/main/Documentation/overlay-profile-diagnostics.md)
in the OSD repository.

Both hooks were built with VS 2026/v145 in `RelWithDebInfo` from an isolated copy
of `4e362b1aac895ea8681752ea1ea028f3559a08ac` plus the changes to
`hook_poc/src/hook_status.cpp`, `hook_status.h`, `hook_status_test.cpp` and
`overlay.cpp`. The `hook_status`, `renderer_arbiter` and `swapchain_lifetime` CTests
passed on both architectures. PE architecture and staged SHA-256 hashes were checked.

| Native DLL | SHA-256 |
| --- | --- |
| Hook x64 | `5AA5E4F03D65BF1E14A1201C60CE95D9CD2806DAC834775530596F0A8642F068` |
| Hook x86 | `429AC7BEB0F505BF7781CF39FD7B56728A55EE1E5D031F48723FFA2B6C2134D7` |

These two hooks are superseded by the combined rebuild above, which contains the same
render-progress changes.

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

As of the frame-generation telemetry rebuild (2026-10-14):

- managed bridge SHA-256: `8153F08BB906CBB14EFAD8530DBAE78D5913A83851D79D2BC044A437EC1B5A43`
- editor controls SHA-256: `E128D12BFE8B3A3E2FABAE6A007E6B948A60F87F3E16D58CD649CD4C72A761FC`
- core SHA-256: `06C9C932F52C2B53FF62ABD05FD4C68F9D4B398241610F13938294E68137D94A`
- hook x64 SHA-256: `2886D763C2C9C962DD547AA724C32972CE7E1EE241B92EEFACDB169D83EE4350`
- hook x86 SHA-256: `E6D6B1FF244167C85A95FC731AD8D43E10BEC7889331F3EC58AA10E6B34292D3`
- Vulkan x64 SHA-256: `D5FB129C6D232355F8280D7FAE379AE6FAD56C6199AC0108FC3921828CA5A7BE`
- Vulkan x86 SHA-256: `11F4A56E433020ED94610492F970475119972614A5C4F61E1E146486A5A70D27`

## Contents

- `net10.0-windows/` — `CapFrameX.OSD.Interop.dll` (managed P/Invoke bridge, x64) and
  `CapFrameX.OSD.Controls.dll` (reusable WPF design editor and native preview)
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
  telemetry. When the loader unloads an image that holds any telemetry target, the unload
  notification removes those hooks while the image is still mapped and returns that provider to
  N/A, so a runtime loaded again is armed afresh; the FidelityFX creation and swapchain-instance
  hooks still arm once per process. V1 compatibility bits 0 and 4 are reserved and ignored: avoiding the shared Release
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

The current installer defaults to `C:\Program Files\CapFrameX`. It installs
`cfx_osd_vklayer.dll` and `cfx_osd_vklayer_v1.json` in
`C:\Program Files\CapFrameX\vulkan` for x64 and
`C:\Program Files\CapFrameX\vulkan\x86` for x86. Earlier installers used
`C:\Program Files (x86)\CapFrameX`.

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
`RelWithDebInfo` outputs into the matching folders above, and record the matching OSD commit
and payload hashes in this document. The Vulkan manifest is renamed on the way in: the build emits `cfx_osd_vklayer.json`,
this tree keeps the versioned `cfx_osd_vklayer_v1.json`.

Copy those files one by one, never a whole `RelWithDebInfo` folder: the build outputs also
hold the ctest executables and `cfx_inject.exe`. That injector was deliberately dropped when
`HookInjector` learned to resolve the target's 32-bit `LoadLibraryW` itself, and a bulk copy
is how it silently reappeared under `native/x86/` once already.

**When the Vulkan layer code changes, the installed layers have to be updated as well.** The layer
compiles `vk_layer/src` with its shaders, the core sources (`${CFX_OSD_CORE_SRC}`) and the `hook_poc`
feed sources listed in `HOOK_SHARED_SRC` (`vk_layer/CMakeLists.txt`). After a change to any of them,
the `vk` DLLs here carry new code, but Vulkan games keep loading the registered copies, normally
`C:\Program Files\CapFrameX\vulkan` and `vulkan\x86`. Copy both new `cfx_osd_vklayer.dll` there
(elevated; the manifests stay), or register this checkout's payload as described below, and keep a
backup of the replaced DLLs. A change to another `hook_poc` header alone does not change the layer's
behavior, only `CFX_VK_COMPATIBILITY_BUILD`, the build hash CapFrameX binds learned Vulkan profiles to;
updating the installed copies is optional then.

For Vulkan runtime tests, also check the manifest paths registered in both HKLM registry views.
The loader uses the DLL beside each registered manifest, even when CapFrameX runs from its
build output. The default installed paths are `C:\Program Files\CapFrameX\vulkan` (x64) and
`C:\Program Files\CapFrameX\vulkan\x86` (x86); older installations may still have registrations
under `C:\Program Files (x86)\CapFrameX`. Development registrations can point at build outputs.
Always use the registered paths to identify the active DLLs. Updating this prebuilt folder and
rebuilding the app does not update the installed copies. Use an updated installation
or run [`scripts/Register-DevelopmentVulkanLayer.ps1`](../../scripts/Register-DevelopmentVulkanLayer.ps1)
from an elevated PowerShell for this checkout. It validates both architectures, backs up the
CapFrameX registrations, disables older HKLM entries, and selects the matching development
payload in each registry view. `-ValidateOnly` previews the plan without changes; `-Restore`
reverts the recorded HKLM changes. Restart the Vulkan target to load the
updated layer; a process already running retains its loaded DLL.

Configure every native tree with the **same** toolset the core preset pins (`-G "Visual Studio 18
2026"`, toolset v145, matching the `.vcxproj` projects in this repo). `hook_poc` and `vk_layer`
take no preset, so passing `-G` explicitly is what keeps them in step: core, hook and layer all end
up loaded in the same game process, and a silent toolset split between them is hard to spot.

Rebuild **all** native trees, not just the core: `hook_poc` and `vk_layer` compile the core sources
into themselves (`${CFX_OSD_CORE_SRC}`), so a core change that is not followed by a rebuild of
those two leaves them silently behind.

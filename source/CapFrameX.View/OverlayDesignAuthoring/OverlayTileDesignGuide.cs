using System;
using System.Linq;
using CapFrameX.OSD.Controls;

namespace CapFrameX.View.OverlayDesignAuthoring
{
    /// <summary>
    /// The reference an automation client reads before it designs a tile profile. It describes the
    /// tile document of <see cref="OverlayTileDocument"/>; ranges mirror OsdDesignDocument.Validate.
    /// </summary>
    public static class OverlayTileDesignGuide
    {
        private static readonly Lazy<string> Content = new Lazy<string>(() => Body
            .Replace("{KINDS}", string.Join(", ", OsdDesignDocument.WidgetKinds.Append("group")), StringComparison.Ordinal)
            .Replace("{SERIES}", string.Join(", ", OsdDesignDocument.ChartSeries), StringComparison.Ordinal)
            .Replace("{MAXTILES}", OsdDesignDocument.MaximumTiles.ToString(), StringComparison.Ordinal)
            .Replace("{FIELDS}", string.Join(", ", OverlayTileDocument.TileFieldNames), StringComparison.Ordinal));

        public static string Text => Content.Value;

        private const string Body = @"# CapFrameX tile overlay design guide

A tile profile is a saved design for the CapFrameX OSD: a canvas of tiles that show live readings
(FPS, frame times, temperatures, clocks, loads, ...), charts, text, icons and images. The in-game and
the hook-free renderer draw tile profiles; RTSS cannot (it always shows the classic row overlay).
The profile library is shared with the tile editor in CapFrameX (Overlay tab > tile overlay > Open
designer): everything you save here opens there, and the user can fine-tune it by mouse.

## Workflow

1. Orient: cfx_list_tile_profiles (saved profiles), cfx_list_tile_templates (gallery templates),
   cfx_list_tile_sources (readings and sensor families on this machine).
2. Create: cfx_create_tile_profile from a template (the quickest good-looking start), as a copy of a
   profile, or from your own tile document.
3. Look: cfx_render_tile_profile returns a PNG made by the real OSD renderer. Check it after every
   change: clipped values, overlaps, empty tiles (a key without a reading) and contrast.
4. Refine: cfx_edit_tile_profile for targeted changes (one tile, one color), cfx_update_tile_profile to
   replace the whole document. cfx_get_tile_profile returns the current document with tile paths.
5. Show it: cfx_activate_tile_profile puts the profile on the OSD. It needs the in-game or the
   hook-free renderer (cfx_set_osd_options renderer=InGame or HookFree). While a profile is active,
   every saved change appears on screen immediately. cfx_deactivate_tile_profile returns to rows.

Where the overlay sits on screen (corner, margins, zoom, scale with resolution) is a global OSD
option (cfx_set_osd_options anchor/marginX/marginY/zoom), not part of a profile.

If the user has unsaved changes in the open tile editor, every library change is refused until they
save or discard them. Tell the user; never ask them to lose their work.

## Tile document

    {
      ""layout"": ""canvas"",
      ""width"": 320,
      ""accent"": ""#00C8FFFF"", ""tileColor"": ""#101820D9"", ""textColor"": ""#F2F4F8FF"",
      ""tiles"": [
        { ""kind"": ""metric"", ""key"": ""fps"", ""label"": ""FPS"", ""valueSize"": 36 },
        { ""kind"": ""metric"", ""key"": ""frametime"", ""label"": ""Frame time"", ""unit"": ""ms"", ""digits"": 1 },
        { ""kind"": ""chart"", ""key"": ""frametimes"", ""columnSpan"": 2, ""frameHeight"": 120 }
      ]
    }

Document fields (all optional except tiles):
- name: the profile name. Output only; rename with cfx_rename_tile_profile.
- layout: ""canvas"" (default) or ""grid"". Canvas: every tile has a frame (x, y, frameWidth,
  frameHeight) and may overlap; required for containers. Grid: tiles flow left to right in
  `columns` and size themselves; coordinates are ignored.
- width: 220..1600 design pixels. Omitted on a canvas: fitted to the tiles.
- canvasHeight: 64..4096 (canvas). Omitted: fitted to the tiles. A canvas grows when tiles need it.
- columns: 1..4. Grid: number of columns. Canvas: a new tile defaults to width / columns per
  columnSpan, so columns 2 gives half-width tiles.
- gap (0..40, space between auto-placed tiles), padding (0..40, default inner padding of every tile),
  radius (0..32, corner radius of tile backgrounds), scale (0.5..2, scales the whole design).
- gridSize (1..128, default 8), showGrid, snapToGrid: editor canvas helpers; auto placement snaps to
  gridSize.
- accent: default color of text tiles, icons, charts, bars and gauges. tileColor: default tile
  background. textColor: default color of values and labels.
- fontFamily: font of every text in the design (default Segoe UI), an installed font family such as
  Bahnschrift or Consolas. A tile's own family overrides it. Fonts that are not installed fall back
  to a system font.
- pixelShift: { ""intervalSeconds"": 10..3600, ""radius"": 1..32 } moves the overlay by one pixel per
  interval (OLED burn-in protection). Omit or null for none.
- tiles: the tiles, at most {MAXTILES} including nested ones.

Colors are ""#RRGGBB"" or ""#RRGGBBAA"" with alpha last: ""#20242CEB"" is 92 % opaque,
""#00000000"" is fully transparent (a tile without background).

## Tiles

Every tile is a panel (background, rounded corners, padding) with one widget inside. Fields shared
by all kinds:
- kind: {KINDS}. Default metric.
- label: the caption (metric/bar/gauge), the chart title, the group title; the layer name for the rest.
- x, y, frameWidth, frameHeight (canvas): the panel's frame in design pixels, relative to the canvas or,
  for children, to the container's inner area (its frame minus padding). Omit x and y (always both)
  to place a tile automatically in the first free spot; omit frameWidth/frameHeight to size it
  naturally. In cfx_edit_tile_profile, set them to null to recompute. Content is clipped to the
  frame. Minimum frameWidth on the canvas is 64 + 2 x padding (8 + 2 x padding for containers, icons,
  images and container children); minimum frameHeight 24.
- columnSpan (1..columns): grid columns the tile spans; on a canvas it sets the default width.
- padding (0..40), backgroundColor, borderColor, borderWidth (0..32): the panel. Omitted values use
  the document's padding and tileColor.
- color: the widget's main color (metric: label, value and unit; bar, gauge and chart: the accent;
  text and icon: the glyphs). Omitted: theme colors.
- family: the font of everything a metric, text, bar, gauge, chart, group or classicRows tile writes
  (labels, values, units, titles, axis labels). Omitted: the document's fontFamily. On an icon it
  names the icon font instead.
- locked: editor-only, prevents moving the tile with the mouse.

Kinds:
- metric: one reading. key (a source key, see Keys), unit, digits (0..3 decimals), valueSize (10..72,
  value font size), labelPosition (top, left, bottom, none), labelSize and unitSize (6..72),
  valueColor, labelColor, unitColor, align (left, center, right), valueLines (1..16, for
  multi-line text readings), showValue.
  valueScale multiplies the value (0.001 turns MB into GB; then set unit). valueFormat ""duration""
  shows seconds as a clock (h:mm:ss) instead of ""number"".
  A second reading in the same tile: secondaryKey, secondaryUnit, secondaryScale,
  secondaryDigits (0..6), separator (default "" / ""), for example temperature / power.
- bar: horizontal bar. key, unit, minimum, maximum (the bar's range), digits, showValue.
- gauge: ring gauge. key, unit, minimum, maximum, digits, startAngle (degrees, -3600..3600),
  sweepAngle (1..360), thickness, showValue. In a grid, height (24..240) is the diameter.
- chart: live line chart. key is the series: {SERIES} (frametimes in ms, framerates in FPS,
  displaytimes in ms). label is the title, unit the axis unit. showBackground, showBorder,
  showLabels (axis labels), currentValue (badge with the latest value). In a grid, height 24..240.
- text: static text. text (up to 500 characters, ""\n"" for a line break), valueSize (font size),
  color (default accent), align (left, center, right).
- icon: one glyph. glyph (1..16 characters, e.g. """" from Segoe MDL2 Assets or Segoe Fluent
  Icons), family (default Segoe MDL2 Assets), valueSize (glyph size), color.
- image: PNG or JPEG. dataUri (""data:image/png;base64,..."", at most 512 KB and 2048 x 2048 px),
  fit (contain, cover, stretch), opacity (0..1).
- group: many readings of one sensor family, e.g. every CPU core clock or thread load. Set groupKey to
  a family key from cfx_list_tile_sources and the members are filled in. groupDisplay (metric, bar
  or heatmap), groupColumns (1..32), groupBarOrientation (horizontal, vertical), groupShowValues,
  groupShowLabels, groupByCoreType (a row per CPU core type), groupBarWidth, groupBarHeight,
  minimum and maximum (bar range), unit, digits, valueSize. groupMembers lists
  { ""key"", ""label"", ""isEnabled"" }: rename or hide members there.
  groupDisplay ""heatmap"" draws one cell per member, shaded from idle to full load in valueColor
  (default the document's textColor) under the title (in color) and an idle..maximum legend. Each
  cell shows its reading averaged over groupWindowSeconds (1..600, default 60). Members are ordered
  P-cores first with each core's threads together, and every core type forms a block of its own,
  labelled (P-Cores, E-Cores, ...) and side by side. Up to 16 cells share one row; more take the
  fewest rows at which the
  blocks need at most 16 columns (8 P + 16 E cores: 4x2 beside 8x2). The cells fill the tile's
  height; minimum and maximum set the range. Use it with a thread or core load family
  (role:cpu-loads).
- container (canvas only): holds tiles in `children`; build cards from several pieces (icon + text +
  metric). Containers nest up to 4 levels.
- classicRows: embeds the classic row overlay (the entries configured on the Overlay tab, see
  cfx_get_overlay_entries). height (24..2048), classicRowScale (0.5..2).

Omitted fields get the editor's defaults: a metric takes label and unit from its reading, a chart
defaults to frametimes, an icon to Segoe MDL2 Assets.

## Keys

A metric, bar or gauge shows the reading named by `key`. Use:
- a source key from cfx_list_tile_sources (`key`), exact for this machine, or
- a semantic key (`semanticKey` there) such as fps, frametime, fps1pctLow, gpuTemp, gpuLoad, cpuLoad,
  gpuPower, cpuTemp, gpuClock, vramUsed, ramLoad. Saving binds it to this machine's matching reading.
A key that matches nothing is accepted but shows no value; the tools report it as a warning. Text
readings (isText, e.g. the GPU name or the game) belong in metric tiles; bars and gauges need numbers.

## Edit operations (cfx_edit_tile_profile)

An array, applied in order, saved only if all succeed. Paths are tile indices joined by ""/"":
""0"" is the first tile, ""2/1"" the second child of container 2. Each operation sees the result of the
previous one, so removing tile 1 shifts later paths.

    [
      { ""op"": ""setDocument"", ""values"": { ""accent"": ""#FF6A00FF"", ""canvasHeight"": null } },
      { ""op"": ""addTile"", ""tile"": { ""kind"": ""gauge"", ""key"": ""gpuTemp"", ""label"": ""GPU"" } },
      { ""op"": ""addTile"", ""parent"": ""3"", ""index"": 0, ""tile"": { ""kind"": ""text"", ""text"": ""CPU"" } },
      { ""op"": ""updateTile"", ""path"": ""0"", ""values"": { ""valueSize"": 40, ""frameWidth"": null, ""frameHeight"": null } },
      { ""op"": ""moveTile"", ""path"": ""4"", ""index"": 0 },
      { ""op"": ""removeTile"", ""path"": ""2"" }
    ]

- setDocument: values merge into the document fields; null restores the default (or the fit).
- addTile: appends to the canvas, or to the container `parent`, at `index` (default: the end).
- updateTile: values merge into the tile; null removes a field (default value, or automatic frame).
- moveTile: to `index` within `parent` (default: the top level). A change of parent re-places the tile.
- removeTile: removes the tile and its children.
Later tiles draw over earlier ones: index order is the layer order.

## Design advice

- An overlay sits over a game: keep it compact (a corner overlay is often 240..420 px wide), use
  semi-transparent dark tiles (alpha around CC..EB) or transparent ones, one accent color, high
  contrast for values and two or three font sizes.
- Typical sizes at the default padding of 12: a metric tile with valueSize 28 and the label on top is
  about 90 px high; a chart needs 100..200 px.
- Render after every change. A clipped value means the frame is too small: enlarge it or set
  frameWidth/frameHeight to null to fit.
- Templates and existing profiles show good patterns; cfx_get_tile_profile on a shipped profile is a
  good way to learn how cards, groups and charts are built.

## Limits

At most {MAXTILES} tiles, 4 container levels and 512 renderer nodes (a group costs 4 plus one per
member); a saved design is at most 1 MiB.

## All tile fields

{FIELDS}
";
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.OSD.Controls;
using CapFrameX.OSD.Integration;
using CapFrameX.OSD.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CapFrameX.Test.Integration
{
    /// <summary>
    /// Fonts of the CapFrameX renderers: a design carries one font for all of its text, and only an
    /// icon names its glyph font; the classic layout takes the font from the Overlay tab. RTSS is
    /// not involved.
    /// </summary>
    [STATestClass]
    [DoNotParallelize]
    public class OverlayFontFamilyTest
    {
        private const string Monospace = "Consolas";

        [TestMethod]
        public void DesignFont_RoundTripsThroughTheThemeAndDefaultsToSegoeUi()
        {
            var document = Design();
            Assert.AreEqual(OsdDesignDocument.DefaultFontFamily, document.FontFamily);
            Assert.AreEqual("Segoe UI", Theme(document.ToJson())["fontFamily"].GetValue<string>(),
                "Designs without a font must export exactly as before.");

            document.FontFamily = "Bahnschrift";
            string json = document.ToJson();
            Assert.AreEqual("Bahnschrift", Theme(json)["fontFamily"].GetValue<string>());
            var loaded = OsdDesignDocument.FromJson(json);
            Assert.AreEqual("Bahnschrift", loaded.FontFamily);
            Assert.AreEqual(json, loaded.ToJson());

            document.FontFamily = "  ";
            Assert.AreEqual(OsdDesignDocument.DefaultFontFamily, document.FontFamily, "An empty font restores the default.");
            document.FontFamily = new string('F', OsdDesignDocument.MaximumFontFamilyLength + 1);
            Assert.ThrowsExactly<FormatException>(() => document.Validate());
        }

        [TestMethod]
        public void DesignFont_IsTheOnlyTextFont_OnlyAnIconNamesItsOwn()
        {
            var document = Design(designFont: Monospace);
            document.Tiles.Add(new OsdDesignTile
            {
                Kind = "icon", Label = "Icon", Glyph = "", Family = "Segoe MDL2 Assets", ValueSize = 20,
                X = 0, Y = 300, FrameWidth = 40, FrameHeight = 40, Padding = 0
            });
            var scene = JsonNode.Parse(document.ToJson());

            Assert.AreEqual(Monospace, scene["theme"]["fontFamily"].GetValue<string>());
            foreach (var panel in scene["root"]["children"].AsArray())
            {
                var widget = panel["children"][0];
                string type = widget["type"].GetValue<string>();
                Assert.AreEqual(type == "icon" ? "Segoe MDL2 Assets" : null, widget["family"]?.GetValue<string>(),
                    $"The {type} widget draws its text in the design font; only an icon names its glyph font.");
            }

            foreach (var kind in OsdDesignDocument.TextFontKinds)
            {
                var tile = Design().Tiles.First().Copy();
                var single = Design();
                single.Tiles.Clear();
                tile.Kind = kind;
                if (kind == "group")
                {
                    tile.Key = tile.GroupKey = "cpu-clocks"; tile.GroupDisplay = "metric"; tile.GroupColumns = 2;
                    tile.GroupMembers = new List<OsdGroupMember> { new OsdGroupMember { Key = "clock0", Label = "C0", IsEnabled = true } };
                }
                else if (kind == "chart") tile.Key = "frametimes";
                tile.Family = Monospace;
                single.Tiles.Add(tile);
                var error = Assert.ThrowsExactly<FormatException>(() => single.Validate(), $"A {kind} tile cannot take its own font.");
                Assert.AreEqual(OsdLocalization.T("Editor_TileFontRemoved"), error.Message);
            }
        }

        [TestMethod]
        public void LegacyDesign_WithAFontItNeverDrew_StillLoadsWithoutIt()
        {
            // Editors before text fonts kept a family a tile had as text, but exported none for
            // bars, gauges, charts, linked classic rows and groups.
            string legacy = WithTileFonts(Design().ToJson(), Monospace, drawn: false);

            var loaded = OsdDesignDocument.FromJson(legacy);

            Assert.IsTrue(loaded.Tiles.All(tile => tile.Family == null), "A font the design never drew must not appear now.");
            Assert.AreEqual(OsdDesignDocument.DefaultFontFamily, loaded.FontFamily, "An undrawn font never becomes the design font.");
            Assert.IsTrue(JsonNode.DeepEquals(WithoutEditorMetadata(JsonNode.Parse(legacy)), WithoutEditorMetadata(JsonNode.Parse(loaded.ToJson()))),
                "The loaded design must draw exactly what the saved one drew.");

            var node = JsonNode.Parse(legacy);
            node["root"]["children"][0]["children"][0]["label"] = "TAMPERED";
            Assert.ThrowsExactly<FormatException>(() => OsdDesignDocument.FromJson(node.ToJsonString()),
                "Dropping old fonts must not let any other mismatch through.");
        }

        [TestMethod]
        [DataRow(4, "Consolas")]
        [DataRow(3, "Consolas")]
        [DataRow(2, "Segoe UI")]
        [DataRow(1, "Segoe UI")]
        public void LegacyDesign_WithTileFonts_TakesTheFontMostOfItsTilesDrew(int tilesWithFont, string expected)
        {
            // Editors before one font per design let every text tile draw a font of its own.
            string legacy = WithTileFonts(Design().ToJson(), Monospace, drawn: true, count: tilesWithFont);

            var loaded = OsdDesignDocument.FromJson(legacy);

            Assert.AreEqual(expected, loaded.FontFamily, "The font most tiles drew wins; a tie keeps the design font.");
            Assert.IsTrue(loaded.EnumerateTiles().All(tile => tile.Family == null), "No text tile keeps a font of its own.");
            string json = loaded.ToJson();
            Assert.AreEqual(json, OsdDesignDocument.FromJson(json).ToJson(), "The unified design loads as it is.");
            Assert.IsFalse(json.Contains("\"family\"", StringComparison.Ordinal), "The unified design names its font once, in the theme.");
            if (tilesWithFont == 4)
            {
                var (before, after) = RenderBoth(legacy, json);
                CollectionAssert.AreEqual(before, after, "A design whose tiles all drew one font draws exactly as before.");
            }
        }

        [TestMethod]
        public void NativePreview_DrawsTheDesignFontOnEveryWidget()
        {
            byte[] segoe = Render(Design());
            var (tileFonts, designFont) = RenderBoth(WithTileFonts(Design().ToJson(), Monospace, drawn: true), Design(designFont: Monospace).ToJson());

            Assert.IsFalse(segoe.SequenceEqual(designFont), "The design font must change the rendered text.");
            CollectionAssert.AreEqual(tileFonts, designFont,
                "The design font must reach the bar, gauge, chart and linked classic rows exactly like the tile fonts did.");
            foreach (var kind in new[] { "bar", "gauge", "chart", "classicRows" })
            {
                var segoeSingle = Design();
                var monoSingle = Design(designFont: Monospace);
                foreach (var single in new[] { segoeSingle, monoSingle })
                    foreach (var other in single.Tiles.Where(tile => tile.Kind != kind).ToArray()) single.Tiles.Remove(other);
                Assert.IsFalse(Render(segoeSingle).SequenceEqual(Render(monoSingle)), $"A {kind} tile must draw the design font.");
            }
        }

        [TestMethod]
        public void HookFreeBridge_ForwardsTheClassicFontAtStartAndOnChange()
        {
            using var configChanges = new Subject<(string key, object value)>();
            using var active = new Subject<bool>();
            using var entries = new Subject<IOverlayEntry[]>();
            var config = new Mock<IAppConfiguration>();
            config.SetupGet(x => x.OnValueChanged).Returns(configChanges);
            config.SetupGet(x => x.OsdFontFamily).Returns(Monospace);
            var overlay = new Mock<IOverlayService>();
            overlay.SetupGet(x => x.IsOverlayActiveStream).Returns(active);
            overlay.SetupGet(x => x.OnDictionaryUpdated).Returns(entries);

            using var bridge = new OsdOverlayBridge(overlay.Object, config.Object);
            object host = ReadField<object>(bridge, "_osd");
            Assert.AreEqual(Monospace, ReadField<string>(host, "_fontFamily"),
                "A stored font must reach the renderer before it is first created.");

            configChanges.OnNext((nameof(IAppConfiguration.OsdFontFamily), "Bahnschrift"));
            Assert.AreEqual("Bahnschrift", ReadField<string>(host, "_fontFamily"));

            configChanges.OnNext((nameof(IAppConfiguration.OsdFontFamily), ""));
            Assert.IsNull(ReadField<string>(host, "_fontFamily"), "An empty font restores the native default.");
        }

        // Bar, gauge, chart and a linked classic block, each with a caption, on one canvas.
        private static OsdDesignDocument Design(string designFont = null)
        {
            var document = new OsdDesignDocument
            {
                Name = "Fonts", Width = 400, CanvasHeight = 370, IsCanvas = true, GridSize = 1,
                Accent = "#00FF80FF", TileColor = "#181818FF"
            };
            if (designFont != null) document.FontFamily = designFont;
            void Add(OsdDesignTile tile, double x, double y, double width, double height)
            {
                tile.X = x; tile.Y = y; tile.FrameWidth = width; tile.FrameHeight = height;
                document.Tiles.Add(tile);
            }
            Add(new OsdDesignTile { Kind = "bar", Key = "load", Label = "LOAD", Unit = "%" }, 0, 0, 240, 70);
            Add(new OsdDesignTile { Kind = "gauge", Key = "load", Label = "GAUGE", Unit = "%" }, 250, 0, 140, 140);
            Add(new OsdDesignTile { Kind = "chart", Key = "frametimes", Label = "FRAME TIME", Unit = "ms" }, 0, 150, 390, 100);
            Add(new OsdDesignTile { Kind = "classicRows", Key = null, Label = "Classic rows", Unit = "" }, 0, 260, 390, 100);
            return document;
        }

        // A file as an editor before one font per design saved it: the first `count` tiles carry the
        // family in their metadata and, when it was drawn, on their widget.
        private static string WithTileFonts(string json, string family, bool drawn, int count = int.MaxValue)
        {
            var node = JsonNode.Parse(json);
            foreach (var panel in node["root"]["children"].AsArray().Take(count))
            {
                var widget = panel["children"][0].AsObject();
                widget["editor"]["family"] = family;
                if (drawn) widget["family"] = family;
            }
            return node.ToJsonString();
        }

        private static byte[] Render(OsdDesignDocument document) => Render(document.ToJson());

        private static byte[] Render(string json)
        {
            using var preview = CreatePreview();
            return Render(preview, json);
        }

        // Pixel-exact comparisons render both scenes in one renderer: separate instances in a long
        // test run may differ in single anti-aliased pixels.
        private static (byte[] First, byte[] Second) RenderBoth(string first, string second)
        {
            using var preview = CreatePreview();
            return (Render(preview, first), Render(preview, second));
        }

        private const int RenderWidth = 480, RenderHeight = 640;

        private static OsdPreview CreatePreview()
        {
            var preview = new OsdPreview();
            Assert.IsTrue(preview.CreateBitmap(RenderWidth, RenderHeight), "Native bitmap renderer did not initialize.");
            return preview;
        }

        private static byte[] Render(OsdPreview preview, string json)
        {
            const int stride = RenderWidth * 4;
            Assert.IsTrue(preview.LoadTemplateJson(json), "Native renderer rejected the design.");
            Assert.IsTrue(preview.SetMetricsJson("{\"load\":64}"));
            preview.UpdateEntries(new[]
            {
                new OsdEntry { Identifier = "Framerate", Group = "FPS", Label = "Framerate", IsNumeric = true, Value = 144, Unit = "FPS", Color = 0xFFFFFFFF }
            });
            var pixels = new byte[stride * RenderHeight];
            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                Assert.IsTrue(preview.RenderBitmap(handle.AddrOfPinnedObject(), pixels.Length, stride, 0.05));
            }
            finally
            {
                handle.Free();
            }
            return pixels;
        }

        private static JsonNode Theme(string json) => JsonNode.Parse(json)["theme"];

        // The native scene alone: what the renderers draw, without the editor's tile metadata.
        private static JsonNode WithoutEditorMetadata(JsonNode node)
        {
            if (node is JsonObject obj)
            {
                obj.Remove("editor");
                foreach (var property in obj) WithoutEditorMetadata(property.Value);
            }
            else if (node is JsonArray array)
                foreach (var item in array) WithoutEditorMetadata(item);
            return node;
        }

        private static T ReadField<T>(object target, string name) =>
            (T)(target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"Missing field: {name}")).GetValue(target);
    }
}

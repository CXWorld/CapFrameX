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
    /// Fonts of the CapFrameX renderers: a design carries one font for all of its text and every
    /// tile that draws text may override it; the classic layout takes the font from the Overlay
    /// tab. RTSS is not involved.
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
        public void TileFont_ReachesEveryWidgetThatDrawsText()
        {
            var document = Design(tileFamily: Monospace);
            var group = new OsdDesignTile
            {
                Kind = "group", Key = "cpu-clocks", GroupKey = "cpu-clocks", GroupDisplay = "metric", GroupColumns = 2,
                Label = "CLOCKS", Unit = "MHz", Family = Monospace, X = 0, Y = 370, FrameWidth = 400, FrameHeight = 140,
                GroupMembers = new List<OsdGroupMember>
                {
                    new OsdGroupMember { Key = "clock0", Label = "C0", IsEnabled = true },
                    new OsdGroupMember { Key = "clock1", Label = "C1", IsEnabled = true }
                }
            };
            document.CanvasHeight = 520;
            document.Tiles.Add(group);
            var root = JsonNode.Parse(document.ToJson())["root"]["children"].AsArray();

            foreach (var panel in root.Take(4))
            {
                var widget = panel["children"][0];
                Assert.AreEqual(Monospace, widget["family"]?.GetValue<string>(),
                    $"The {widget["type"]} widget must carry the tile font.");
            }
            var groupWidget = root[4]["children"][0];
            Assert.AreEqual(Monospace, groupWidget["children"][0]["family"]?.GetValue<string>(), "group title");
            foreach (var reading in groupWidget["children"][1]["children"].AsArray())
                Assert.AreEqual(Monospace, reading["family"]?.GetValue<string>(), "group reading");
            Assert.AreEqual(document.ToJson(), OsdDesignDocument.FromJson(document.ToJson()).ToJson());
        }

        [TestMethod]
        public void LegacyDesign_WithAFontItNeverDrew_StillLoadsWithoutIt()
        {
            // Editors before this change kept a family a tile had as text, but exported none for
            // bars, gauges, charts, linked classic rows and groups.
            var node = JsonNode.Parse(Design(tileFamily: Monospace).ToJson());
            foreach (var panel in node["root"]["children"].AsArray())
                panel["children"][0].AsObject().Remove("family");
            string legacy = node.ToJsonString();

            var loaded = OsdDesignDocument.FromJson(legacy);

            Assert.IsTrue(loaded.Tiles.All(tile => tile.Family == null), "A font the design never drew must not appear now.");
            Assert.IsTrue(JsonNode.DeepEquals(WithoutEditorMetadata(JsonNode.Parse(legacy)), WithoutEditorMetadata(JsonNode.Parse(loaded.ToJson()))),
                "The loaded design must draw exactly what the saved one drew.");

            node["root"]["children"][0]["children"][0]["label"] = "TAMPERED";
            Assert.ThrowsExactly<FormatException>(() => OsdDesignDocument.FromJson(node.ToJsonString()),
                "Dropping old fonts must not let any other mismatch through.");
        }

        [TestMethod]
        public void NativePreview_DrawsTheDesignFontLikeATileFontOnEveryWidget()
        {
            byte[] segoe = Render(Design());
            byte[] tileFonts = Render(Design(tileFamily: Monospace));
            byte[] designFont = Render(Design(designFont: Monospace));

            Assert.IsFalse(segoe.SequenceEqual(tileFonts), "Tile fonts must change the rendered text.");
            CollectionAssert.AreEqual(tileFonts, designFont,
                "The design font must reach the bar, gauge, chart and linked classic rows exactly like a tile font.");
            foreach (var kind in new[] { "bar", "gauge", "chart", "classicRows" })
            {
                var single = Design();
                single.Tiles.First(tile => tile.Kind == kind).Family = Monospace;
                Assert.IsFalse(segoe.SequenceEqual(Render(single)), $"A {kind} tile must draw its own font.");
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
        private static OsdDesignDocument Design(string tileFamily = null, string designFont = null)
        {
            var document = new OsdDesignDocument
            {
                Name = "Fonts", Width = 400, CanvasHeight = 370, IsCanvas = true, GridSize = 1,
                Accent = "#00FF80FF", TileColor = "#181818FF"
            };
            if (designFont != null) document.FontFamily = designFont;
            void Add(OsdDesignTile tile, double x, double y, double width, double height)
            {
                tile.X = x; tile.Y = y; tile.FrameWidth = width; tile.FrameHeight = height; tile.Family = tileFamily;
                document.Tiles.Add(tile);
            }
            Add(new OsdDesignTile { Kind = "bar", Key = "load", Label = "LOAD", Unit = "%" }, 0, 0, 240, 70);
            Add(new OsdDesignTile { Kind = "gauge", Key = "load", Label = "GAUGE", Unit = "%" }, 250, 0, 140, 140);
            Add(new OsdDesignTile { Kind = "chart", Key = "frametimes", Label = "FRAME TIME", Unit = "ms" }, 0, 150, 390, 100);
            Add(new OsdDesignTile { Kind = "classicRows", Key = null, Label = "Classic rows", Unit = "" }, 0, 260, 390, 100);
            return document;
        }

        private static byte[] Render(OsdDesignDocument document)
        {
            const int width = 480, height = 640, stride = width * 4;
            using var preview = new OsdPreview();
            Assert.IsTrue(preview.CreateBitmap(width, height), "Native bitmap renderer did not initialize.");
            Assert.IsTrue(preview.LoadTemplateJson(document.ToJson()), "Native renderer rejected the design.");
            Assert.IsTrue(preview.SetMetricsJson("{\"load\":64}"));
            preview.UpdateEntries(new[]
            {
                new OsdEntry { Identifier = "Framerate", Group = "FPS", Label = "Framerate", IsNumeric = true, Value = 144, Unit = "FPS", Color = 0xFFFFFFFF }
            });
            var pixels = new byte[stride * height];
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

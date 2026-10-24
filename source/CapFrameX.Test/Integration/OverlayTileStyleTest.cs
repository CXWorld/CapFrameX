using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using CapFrameX.Contracts.Overlay;
using CapFrameX.OSD.Controls;
using CapFrameX.OSD.Integration;
using CapFrameX.OSD.Interop;
using CapFrameX.View.OverlayDesignAuthoring;
using MaterialDesignThemes.Wpf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace CapFrameX.Test.Integration
{
    /// <summary>
    /// Tile styles: every tile owns the colors it draws, by kind, and a design shares only the tile
    /// opacity and the font. Designs saved before keep drawing exactly what they drew.
    /// </summary>
    [STATestClass]
    [DoNotParallelize]
    public class OverlayTileStyleTest
    {
        private const string Accent = "#0271F9FF", TileColor = "#20242CEB", TextColor = "#F2F4F8FF";

        // One distinct color per field, apart from each other and from the dark tile background.
        private static readonly IReadOnlyDictionary<string, string> Distinct = new Dictionary<string, string>
        {
            [OsdTileStyle.Title] = "#FF40FFFF", [OsdTileStyle.Primary] = "#FFE020FF", [OsdTileStyle.Label] = "#FF3030FF",
            [OsdTileStyle.Value] = "#30FF40FF", [OsdTileStyle.Unit] = "#3070FFFF", [OsdTileStyle.Track] = "#30F0F0FF",
            [OsdTileStyle.ChartBackground] = "#503050FF", [OsdTileStyle.ChartBorder] = "#FF9020FF",
            [OsdTileStyle.Background] = "#101010FF", [OsdTileStyle.Border] = "#A0A0FFFF"
        };

        [TestMethod]
        public void EveryKindListsExactlyTheColorsItDraws()
        {
            const string background = OsdTileStyle.Background, border = OsdTileStyle.Border;
            string[] Parts(OsdDesignTile tile) => OsdTileStyle.PartsOf(tile).Select(part => part.Field).ToArray();
            void Expect(OsdDesignTile tile, params string[] fields) => CollectionAssert.AreEqual(
                fields.Concat(new[] { background, border }).ToArray(), Parts(tile), $"{tile.Kind} {tile.GroupDisplay}");

            Expect(Tile("metric"), "labelColor", "valueColor", "unitColor");
            var bare = Tile("metric"); bare.Unit = ""; bare.LabelPosition = "none";
            Expect(bare, "valueColor");
            Expect(Tile("text"), "color");
            Expect(Tile("icon"), "color");
            Expect(Tile("bar"), "color", "labelColor", "valueColor", "trackColor");
            var quiet = Tile("gauge"); quiet.ShowValue = false;
            Expect(quiet, "color", "labelColor", "trackColor");
            Expect(Tile("chart"), "color", "labelColor", "chartBackgroundColor", "chartBorderColor");
            var chart = Tile("chart");
            chart.ShowLabels = false; chart.ShowBackground = false; chart.ShowBorder = false; chart.CurrentValue = true;
            chart.ChartLines = new List<OsdChartLine> { new OsdChartLine { Series = "frametimes", Kind = "value" } };
            Expect(chart, "valueColor");
            Expect(Group("metric"), "titleColor", "labelColor", "valueColor", "unitColor");
            Expect(Group("bar"), "titleColor", "color", "labelColor", "valueColor", "trackColor");
            var bars = Group("bar"); bars.GroupShowLabels = false; bars.GroupShowValues = false;
            Expect(bars, "titleColor", "color", "trackColor");
            Expect(Group("heatmap"), "titleColor", "labelColor", "valueColor");
            foreach (var kind in new[] { "classicRows", "image", "container" }) Expect(Tile(kind));

            Assert.AreEqual(OsdTileStyle.Fields.Count, OsdTileStyle.Fields.Distinct().Count(), "Fields lists every color once.");
            var captions = new Dictionary<string, string>
            {
                ["bar"] = "Editor_StyleFill", ["gauge"] = "Editor_StyleArc", ["chart"] = "Editor_StyleLine",
                ["text"] = "Editor_StyleText", ["icon"] = "Editor_StyleIcon"
            };
            foreach (var pair in captions)
                Assert.AreEqual(pair.Value, OsdTileStyle.PartsOf(Tile(pair.Key)).First(part => part.Field == OsdTileStyle.Primary).CaptionKey);
            var heatmap = OsdTileStyle.PartsOf(Group("heatmap"));
            Assert.AreEqual("Editor_StyleCells", heatmap.Single(part => part.Field == OsdTileStyle.Value).CaptionKey);
            Assert.AreEqual("Editor_StyleLegend", heatmap.Single(part => part.Field == OsdTileStyle.Label).CaptionKey);
            Assert.AreEqual("Editor_StyleCaptions", OsdTileStyle.PartsOf(Tile("chart")).Single(part => part.Field == OsdTileStyle.Label).CaptionKey);

            // Every caption reads in every language.
            var keys = AllKinds().SelectMany(tile => OsdTileStyle.PartsOf(tile)).Append(OsdTileStyle.PartsOf(chart).Single(part => part.Field == OsdTileStyle.Value))
                .Select(part => part.CaptionKey).Distinct().ToArray();
            string language = OsdLocalization.Instance.Language;
            try
            {
                foreach (string code in new[] { "en", "de", "es", "ru", "ko" })
                {
                    OsdLocalization.SetLanguage(code);
                    foreach (string key in keys)
                        Assert.AreNotEqual(key, OsdLocalization.T(key), $"{code}: {key} has no text.");
                }
            }
            finally
            {
                OsdLocalization.SetLanguage(language);
            }
        }

        [TestMethod]
        public void UnsetColorsResolveToWhatTheRendererDraws()
        {
            var document = new OsdDesignDocument { Accent = "#112233FF", TileColor = "#445566EE", TextColor = "#778899FF" };
            string Resolve(OsdDesignTile tile, string field) => OsdTileStyle.Resolve(document, tile, field);

            var metric = Tile("metric");
            Assert.AreEqual("#778899FF", Resolve(metric, OsdTileStyle.Label));
            Assert.AreEqual("#778899FF", Resolve(metric, OsdTileStyle.Value));
            Assert.AreEqual("#778899FF", Resolve(metric, OsdTileStyle.Unit));
            Assert.AreEqual("#445566EE", Resolve(metric, OsdTileStyle.Background));
            Assert.AreEqual(OsdTileStyle.ThemeGrid, Resolve(metric, OsdTileStyle.Border));
            metric.Color = "#ABCDEFFF";
            Assert.AreEqual("#ABCDEFFF", Resolve(metric, OsdTileStyle.Value), "A metric's older shared color draws its value.");
            Assert.AreEqual("#ABCDEFFF", Resolve(metric, OsdTileStyle.Unit), "The unit follows the value.");
            Assert.AreEqual("#778899FF", Resolve(metric, OsdTileStyle.Label), "It never drew the label.");
            metric.ValueColor = "#010203FF";
            Assert.AreEqual("#010203FF", Resolve(metric, OsdTileStyle.Unit));
            metric.UnitColor = "#040506FF";
            Assert.AreEqual("#040506FF", Resolve(metric, OsdTileStyle.Unit));

            Assert.AreEqual("#112233FF", Resolve(Tile("text"), OsdTileStyle.Primary));
            Assert.AreEqual("#112233FF", Resolve(Tile("icon"), OsdTileStyle.Primary));
            foreach (var kind in new[] { "bar", "gauge" })
            {
                var tile = Tile(kind);
                tile.Color = "#ABCDEFFF";
                Assert.AreEqual("#ABCDEFFF", Resolve(tile, OsdTileStyle.Primary));
                Assert.AreEqual("#778899FF", Resolve(tile, OsdTileStyle.Value), $"A {kind}'s fill is no value color.");
                Assert.AreEqual("#778899FF", Resolve(tile, OsdTileStyle.Label));
                Assert.AreEqual(OsdTileStyle.ThemeGrid, Resolve(tile, OsdTileStyle.Track));
            }
            var chart = Tile("chart");
            Assert.AreEqual("#112233FF", Resolve(chart, OsdTileStyle.Primary));
            Assert.AreEqual(OsdTileStyle.ThemeChartBackground, Resolve(chart, OsdTileStyle.ChartBackground));
            Assert.AreEqual(OsdTileStyle.ThemeGrid, Resolve(chart, OsdTileStyle.ChartBorder));

            foreach (var display in new[] { "metric", "bar", "heatmap" })
            {
                var group = Group(display);
                Assert.AreEqual("#112233FF", Resolve(group, OsdTileStyle.Title));
                group.Color = "#ABCDEFFF";
                Assert.AreEqual("#ABCDEFFF", Resolve(group, OsdTileStyle.Title), "A group's title follows its color.");
                Assert.AreEqual(display == "metric" ? "#ABCDEFFF" : "#778899FF", Resolve(group, OsdTileStyle.Value),
                    $"Only a metric group draws its color as the values ({display}).");
                group.TitleColor = "#0A0B0CFF";
                Assert.AreEqual("#0A0B0CFF", Resolve(group, OsdTileStyle.Title));
            }
        }

        [TestMethod]
        public void EveryColorReachesItsWidgetAndSurvivesTheRoundTrip()
        {
            foreach (var tile in AllKinds())
            {
                var document = SingleTile(tile);
                foreach (var part in OsdTileStyle.PartsOf(tile)) OsdTileStyle.SetColor(tile, part.Field, Distinct[part.Field]);
                tile.BorderWidth = 2;
                string json = document.ToJson();
                var panel = JsonNode.Parse(json)["root"]["children"][0];
                var widget = panel["children"][0];
                string Native(JsonNode node, string key) => node?[key]?.GetValue<string>();
                string kind = tile.Kind == "group" ? "group " + tile.GroupDisplay : tile.Kind;

                Assert.AreEqual(Distinct[OsdTileStyle.Background], Native(panel, "color"), kind);
                Assert.AreEqual(Distinct[OsdTileStyle.Border], Native(panel, "borderColor"), kind);
                switch (kind)
                {
                    case "metric":
                        Assert.AreEqual(Distinct[OsdTileStyle.Label], Native(widget, "labelColor"));
                        Assert.AreEqual(Distinct[OsdTileStyle.Value], Native(widget, "valueColor"));
                        Assert.AreEqual(Distinct[OsdTileStyle.Unit], Native(widget, "unitColor"));
                        break;
                    case "text":
                    case "icon":
                        Assert.AreEqual(Distinct[OsdTileStyle.Primary], Native(widget, "color"), kind);
                        break;
                    case "bar":
                    case "gauge":
                        Assert.AreEqual(Distinct[OsdTileStyle.Primary], Native(widget, "color"), kind);
                        Assert.AreEqual(Distinct[OsdTileStyle.Label], Native(widget, "labelColor"), kind);
                        Assert.AreEqual(Distinct[OsdTileStyle.Value], Native(widget, "valueColor"), kind);
                        Assert.AreEqual(Distinct[OsdTileStyle.Track], Native(widget, "trackColor"), kind);
                        break;
                    case "chart":
                        Assert.AreEqual(Distinct[OsdTileStyle.Primary], Native(widget, "color"));
                        Assert.AreEqual(Distinct[OsdTileStyle.Label], Native(widget, "labelColor"));
                        Assert.AreEqual(Distinct[OsdTileStyle.ChartBackground], Native(widget, "backgroundColor"));
                        Assert.AreEqual(Distinct[OsdTileStyle.ChartBorder], Native(widget, "borderColor"));
                        Assert.IsNull(Native(widget, "valueColor"), "The badge color is no part of a chart without its badge.");
                        break;
                    case "group metric":
                    case "group bar":
                        Assert.AreEqual(Distinct[OsdTileStyle.Title], Native(widget["children"][0], "color"), kind);
                        foreach (var reading in widget["children"][1]["children"].AsArray())
                        {
                            Assert.AreEqual(Distinct[OsdTileStyle.Label], Native(reading, "labelColor"), kind);
                            Assert.AreEqual(Distinct[OsdTileStyle.Value], Native(reading, "valueColor"), kind);
                            if (kind == "group bar")
                            {
                                Assert.AreEqual(Distinct[OsdTileStyle.Primary], Native(reading, "color"));
                                Assert.AreEqual(Distinct[OsdTileStyle.Track], Native(reading, "trackColor"));
                            }
                            else Assert.AreEqual(Distinct[OsdTileStyle.Unit], Native(reading, "unitColor"));
                        }
                        break;
                    case "group heatmap":
                        var heatmap = widget["children"][0];
                        Assert.AreEqual(Distinct[OsdTileStyle.Title], Native(heatmap, "titleColor"));
                        Assert.AreEqual(Distinct[OsdTileStyle.Label], Native(heatmap, "labelColor"));
                        Assert.AreEqual(Distinct[OsdTileStyle.Value], Native(heatmap, "color"));
                        break;
                }
                var loaded = OsdDesignDocument.FromJson(json);
                Assert.AreEqual(json, loaded.ToJson(), $"A styled {kind} loads as it is.");
                foreach (var part in OsdTileStyle.PartsOf(tile))
                    Assert.AreEqual(Distinct[part.Field], OsdTileStyle.DrawnColor(loaded.Tiles[0], part.Field), $"{kind} {part.Field}");
            }

            var badge = Tile("chart");
            badge.CurrentValue = true;
            badge.ValueColor = Distinct[OsdTileStyle.Value];
            Assert.AreEqual(Distinct[OsdTileStyle.Value],
                JsonNode.Parse(SingleTile(badge).ToJson())["root"]["children"][0]["children"][0]["valueColor"].GetValue<string>());
        }

        [TestMethod]
        public void UnsetColorsKeepTheExportOfEverySavedDesign()
        {
            // Designs saved before tiles had styles export no style colors; nothing new may appear.
            foreach (var tile in AllKinds())
            {
                var scene = JsonNode.Parse(SingleTile(tile).ToJson());
                Assert.IsNull(scene["editor"]["tileOpacity"], "Opaque tiles store no opacity.");
                string native = WithoutEditorMetadata(scene).ToJsonString();
                foreach (string key in new[] { "labelColor", "valueColor", "unitColor", "trackColor", "backgroundColor", "borderColor" })
                    Assert.IsFalse(native.Contains("\"" + key + "\"", StringComparison.Ordinal), $"{tile.Kind} {tile.GroupDisplay} exports {key} unset.");
            }
        }

        [TestMethod]
        public void NativeRendererDrawsEveryPartInItsColor()
        {
            var background = Rgb(Distinct[OsdTileStyle.Background]);
            foreach (var tile in AllKinds().Where(tile => tile.Kind is "metric" or "bar" or "gauge" or "chart" or "group" or "text"))
            {
                var document = SingleTile(tile);
                document.Scale = 2;
                tile.CurrentValue = tile.Kind == "chart" ? true : tile.CurrentValue;
                foreach (var part in OsdTileStyle.PartsOf(tile)) OsdTileStyle.SetColor(tile, part.Field, Distinct[part.Field]);
                string kind = tile.Kind == "group" ? "group " + tile.GroupDisplay : tile.Kind;
                byte[] pixels = Render(document);

                // The border needs a border width, a chart's line frames, and the tile background is
                // what the other colors are measured against.
                foreach (var part in OsdTileStyle.PartsOf(tile).Where(part => part.Field != OsdTileStyle.Border && part.Field != OsdTileStyle.Background
                    && !(tile.Kind == "chart" && part.Field == OsdTileStyle.Primary)))
                {
                    // Chart texts sit on the chart area; everything else on the tile.
                    var under = tile.Kind == "chart" && part.Field is OsdTileStyle.Label or OsdTileStyle.Value
                        ? Rgb(Distinct[OsdTileStyle.ChartBackground]) : background;
                    int count = Count(pixels, Rgb(Distinct[part.Field]), under);
                    Assert.IsTrue(count >= 6, $"The {kind} draws no {part.Field} ({count} px).");
                }
                Assert.IsTrue(Exact(pixels, background) > 10000, $"The {kind} tile lost its background.");
                Assert.AreEqual(0, Count(pixels, Rgb(TextColor), background), $"The {kind} still draws text in the default color.");
                Assert.AreEqual(0, Count(pixels, Rgb(Accent), background), $"The {kind} still draws the default accent.");

                if (kind == "bar")
                {
                    Assert.IsTrue(Bounds(pixels, Distinct[OsdTileStyle.Label], background).Right < Bounds(pixels, Distinct[OsdTileStyle.Value], background).Left,
                        "The bar's label is on the left, its value on the right.");
                    Assert.IsTrue(Bounds(pixels, Distinct[OsdTileStyle.Track], background).Right > Bounds(pixels, Distinct[OsdTileStyle.Primary], background).Right,
                        "The empty track continues right of the fill.");
                }
                if (kind == "gauge")
                    Assert.IsTrue(Bounds(pixels, Distinct[OsdTileStyle.Label], background).Top > Bounds(pixels, Distinct[OsdTileStyle.Value], background).Bottom,
                        "The gauge's label is below its value.");
                if (kind == "group heatmap")
                    Assert.IsTrue(Bounds(pixels, Distinct[OsdTileStyle.Title], background).Right < Bounds(pixels, Distinct[OsdTileStyle.Label], background).Left,
                        "The heatmap title is left of its legend.");
            }
        }

        [TestMethod]
        public void TileOpacityFadesEveryTileBackgroundButNoTransparentOne()
        {
            var document = new OsdDesignDocument { Name = "Opacity", IsCanvas = true, Width = 400, CanvasHeight = 300, GridSize = 1 };
            var plain = Frame(Tile("metric"), 0, 0, 180, 90);
            var red = Frame(Tile("metric"), 200, 0, 180, 90); red.BackgroundColor = "#FF0000FF";
            var clear = Frame(Tile("metric"), 0, 100, 180, 90); clear.BackgroundColor = "#00000000";
            var card = Frame(Tile("container"), 200, 100, 190, 190); card.BackgroundColor = "#0000FFFF";
            var inner = Frame(Tile("metric"), 0, 0, 120, 90); inner.BackgroundColor = "#00FF0080";
            card.Children.Add(inner);
            foreach (var tile in new[] { plain, red, clear, card }) document.Tiles.Add(tile);
            // The tile panels of the scene: the four tiles, then the one inside the container's canvas.
            string[] Backgrounds(string json)
            {
                var panels = JsonNode.Parse(json)["root"]["children"].AsArray();
                return panels.Select(panel => panel["color"].GetValue<string>())
                    .Append(panels[3]["children"][0]["children"][0]["color"].GetValue<string>()).ToArray();
            }

            string opaque = document.ToJson();
            Assert.IsNull(JsonNode.Parse(opaque)["editor"]["tileOpacity"]);
            CollectionAssert.AreEqual(new[] { TileColor, "#FF0000FF", "#00000000", "#0000FFFF", "#00FF0080" }, Backgrounds(opaque),
                "At 100 % every tile draws its background exactly.");

            document.TileOpacity = 0.5;
            string faded = document.ToJson();
            Assert.AreEqual(0.5, JsonNode.Parse(faded)["editor"]["tileOpacity"].GetValue<double>());
            CollectionAssert.AreEqual(new[] { "#20242C76", "#FF000080", "#00000000", "#0000FF80", "#00FF0040" }, Backgrounds(faded),
                "Half opacity halves every background's alpha; a transparent tile stays transparent.");
            var loaded = OsdDesignDocument.FromJson(faded);
            Assert.AreEqual(0.5, loaded.TileOpacity);
            Assert.AreEqual(faded, loaded.ToJson());
            Assert.AreEqual("#FF0000FF", loaded.Tiles[1].BackgroundColor, "The opacity never rewrites a tile's own color.");

            document.TileOpacity = 0;
            Assert.AreEqual("#FF000000", Backgrounds(document.ToJson())[1]);
            foreach (double invalid in new[] { -0.01, 1.01, double.NaN })
            {
                document.TileOpacity = invalid;
                Assert.ThrowsExactly<FormatException>(() => document.Validate(), $"Tile opacity {invalid}");
            }

            // The renderer blends the faded tile with what lies behind it: the preview's checkerboard
            // (#2A2A30 and #21212A), so half a red is about 146 red over a dark gray, never pure red.
            var single = SingleTile(Tile("text"));
            single.Tiles[0].BackgroundColor = "#FF0000FF";
            int Pure(byte[] pixels) => Enumerable.Range(0, pixels.Length / 4)
                .Count(i => pixels[i * 4 + 2] > 245 && pixels[i * 4 + 1] < 8 && pixels[i * 4] < 8);
            int Half(byte[] pixels) => Enumerable.Range(0, pixels.Length / 4)
                .Count(i => pixels[i * 4 + 2] is >= 138 and <= 154 && pixels[i * 4 + 1] is >= 12 and <= 26 && pixels[i * 4] is >= 16 and <= 30);
            byte[] solid = Render(single);
            single.TileOpacity = 0.5;
            byte[] halved = Render(single);
            Assert.IsTrue(Pure(solid) > 50000, "At 100 % the tile covers what lies behind it.");
            Assert.AreEqual(0, Half(solid));
            Assert.AreEqual(0, Pure(halved), "At 50 % no pixel of the tile is opaque.");
            Assert.IsTrue(Half(halved) > 50000, $"At 50 % the tile shows half of the backdrop ({Half(halved)} px).");
        }

        [TestMethod]
        public void ChangingTheBarsOfAGroupKeepsItsTitle()
        {
            var document = SingleTile(Group("bar"));
            var group = document.Tiles[0];
            OsdTileStyle.Set(document, group, OsdTileStyle.Primary, "#FF0000FF");
            Assert.AreEqual(Accent, group.TitleColor, "The title keeps the color it showed.");
            Assert.AreEqual("#FF0000FF", group.Color);
            var title = JsonNode.Parse(document.ToJson())["root"]["children"][0]["children"][0]["children"][0];
            Assert.AreEqual(Accent, title["color"].GetValue<string>());

            OsdTileStyle.Set(document, group, OsdTileStyle.Primary, "#00FF00FF");
            Assert.AreEqual(Accent, group.TitleColor, "A title with its own color keeps it.");
            var metric = SingleTile(Group("metric"));
            OsdTileStyle.Set(metric, metric.Tiles[0], OsdTileStyle.Value, "#00FF00FF");
            Assert.IsNull(metric.Tiles[0].TitleColor, "Only the bar color is shared with the title.");
        }

        [TestMethod]
        public void ResetAndCopyWorkOnOneTileAndItsKindOnTheSameLevel()
        {
            var document = new OsdDesignDocument { Name = "Copy", IsCanvas = true, Width = 600, CanvasHeight = 400, GridSize = 1 };
            var source = Frame(Tile("metric"), 0, 0, 180, 90);
            var sibling = Frame(Tile("metric"), 200, 0, 180, 90);
            var same = Frame(Tile("metric"), 400, 0, 180, 90);
            var bar = Frame(Tile("bar"), 0, 100, 180, 90);
            var card = Frame(Tile("container"), 200, 100, 380, 200);
            var nested = Frame(Tile("metric"), 0, 0, 180, 90);
            card.Children.Add(nested);
            foreach (var tile in new[] { source, sibling, same, bar, card }) document.Tiles.Add(tile);
            foreach (var part in OsdTileStyle.PartsOf(source)) OsdTileStyle.SetColor(source, part.Field, Distinct[part.Field]);
            source.BorderWidth = 3;
            same.LabelColor = Distinct[OsdTileStyle.Label];

            Assert.AreEqual(2, OsdTileStyle.CopyToSameKind(source, document.Tiles));
            foreach (var tile in new[] { sibling, same })
            {
                foreach (string field in OsdTileStyle.Fields)
                    Assert.AreEqual(OsdTileStyle.GetColor(source, field), OsdTileStyle.GetColor(tile, field), field);
                Assert.AreEqual(3, tile.BorderWidth);
            }
            Assert.IsNull(bar.LabelColor, "Another kind keeps its colors.");
            Assert.IsNull(nested.LabelColor, "A tile on another level keeps its colors.");
            Assert.AreEqual(0, OsdTileStyle.CopyToSameKind(source, document.Tiles), "Nothing left to change.");

            OsdTileStyle.Reset(sibling);
            Assert.IsTrue(OsdTileStyle.Fields.All(field => OsdTileStyle.GetColor(sibling, field) == null));
            Assert.AreEqual(3, sibling.BorderWidth, "Resetting the colors keeps the border width.");

            var heatmap = Group("heatmap");
            var metricGroup = Group("metric");
            var barGroup = Group("bar");
            barGroup.TitleColor = Distinct[OsdTileStyle.Title];
            Assert.AreEqual(0, OsdTileStyle.CopyToSameKind(barGroup, new[] { heatmap, metricGroup }), "Groups only take styles of the same display.");
        }

        [TestMethod]
        public void KindChangesKeepOnlyTheColorsTheNewKindDraws()
        {
            var tile = Tile("metric");
            tile.Color = "#010101FF"; tile.LabelColor = "#020202FF"; tile.ValueColor = "#030303FF"; tile.UnitColor = "#040404FF";
            tile.BackgroundColor = "#050505FF"; tile.BorderColor = "#060606FF";
            tile.Kind = "bar";
            OsdTileStyle.ClearUnused(tile);
            Assert.AreEqual("#020202FF", tile.LabelColor);
            Assert.AreEqual("#030303FF", tile.ValueColor);
            Assert.IsNull(tile.UnitColor, "A bar draws no unit color.");
            tile.TrackColor = "#070707FF";
            tile.Kind = "text";
            OsdTileStyle.ClearUnused(tile);
            Assert.IsNull(tile.LabelColor);
            Assert.IsNull(tile.ValueColor);
            Assert.IsNull(tile.TrackColor);
            Assert.AreEqual("#010101FF", tile.Color, "The primary color carries over, as it always did.");
            Assert.AreEqual("#050505FF", tile.BackgroundColor);
            Assert.AreEqual("#060606FF", tile.BorderColor);
        }

        [TestMethod]
        public void ColorsASavedDesignNeverDrewStayUndrawnUntilSet()
        {
            // Generated designs kept label colors on charts and groups that no editor drew.
            var document = new OsdDesignDocument { Name = "Legacy", IsCanvas = true, Width = 500, CanvasHeight = 640, GridSize = 1 };
            document.Tiles.Add(Frame(Tile("chart"), 0, 0, 480, 120));
            document.Tiles.Add(Frame(Tile("chart"), 0, 130, 480, 120));
            document.Tiles.Add(Frame(Group("bar"), 0, 260, 480, 180));
            document.Tiles.Add(Frame(Group("heatmap"), 0, 450, 480, 180));
            document.Tiles[1].LabelColor = "#123456FF";
            var node = JsonNode.Parse(document.ToJson());
            foreach (int index in new[] { 0, 2, 3 })
                node["root"]["children"][index]["children"][0]["editor"]["labelColor"] = "#654321FF";
            string legacy = node.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

            var loaded = OsdDesignDocument.FromJson(legacy);

            Assert.IsTrue(JsonNode.DeepEquals(JsonNode.Parse(legacy), JsonNode.Parse(loaded.ToJson())), "The file stays exactly as it was.");
            foreach (int index in new[] { 0, 2, 3 })
            {
                var tile = loaded.Tiles[index];
                Assert.AreEqual("#654321FF", tile.LabelColor, "The saved metadata stays.");
                Assert.IsNull(OsdTileStyle.DrawnColor(tile, OsdTileStyle.Label), "It never drew the color.");
                Assert.AreEqual(loaded.TextColor, OsdTileStyle.Resolve(loaded, tile, OsdTileStyle.Label));
                var exchanged = OverlayTileDocument.ToJson(loaded)["tiles"][index];
                Assert.IsNull(exchanged["labelColor"], "The tile document shows what the tile draws.");
            }
            Assert.AreEqual("#123456FF", OsdTileStyle.DrawnColor(loaded.Tiles[1], OsdTileStyle.Label), "A drawn color in the same file is drawn.");

            var chart = loaded.Tiles[0];
            int changes = 0;
            chart.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(OsdDesignTile.LabelColor)) changes++; };
            OsdTileStyle.Set(loaded, chart, OsdTileStyle.Label, "#654321FF");
            Assert.AreEqual(1, changes, "Drawing the same color is a change.");
            Assert.AreEqual("#654321FF", JsonNode.Parse(loaded.ToJson())["root"]["children"][0]["children"][0]["labelColor"].GetValue<string>());
            var copy = loaded.Tiles[2].Copy();
            Assert.IsNull(OsdTileStyle.DrawnColor(copy, OsdTileStyle.Label), "A copy keeps the color undrawn.");

            node["root"]["children"][0]["children"][0]["title"] = "TAMPERED";
            Assert.ThrowsExactly<FormatException>(() => OsdDesignDocument.FromJson(node.ToJsonString()),
                "Reading what a file drew must not let any other difference through.");
        }

        [TestMethod]
        public void EveryShippedDesignLoadsExactlyAsSaved()
        {
            foreach (var starter in OverlayDesignStarterCatalog.Profiles)
            {
                var loaded = OsdDesignDocument.FromJson(starter.DesignJson);
                Assert.IsTrue(JToken.DeepEquals(JToken.Parse(starter.DesignJson), JToken.Parse(loaded.ToJson())),
                    $"Starter '{starter.PresetId}' changes when the editor opens it.");
                Assert.IsTrue(loaded.EnumerateTiles().All(tile => tile.Family == null || tile.Kind == "icon"),
                    $"Starter '{starter.PresetId}' still sets a font per tile.");
            }
            foreach (var preset in OsdDesignPresets.All)
            {
                string json = preset.CreateDocument().ToJson();
                Assert.AreEqual(json, OsdDesignDocument.FromJson(json).ToJson(), $"Template '{preset.Id}' changes when it loads.");
            }
        }

        [TestMethod]
        public void TileDocumentCarriesTheOpacityAndEveryTileColor()
        {
            var json = JObject.Parse(@"{ ""tileOpacity"": 0.75, ""tiles"": [
                { ""kind"": ""bar"", ""key"": ""load"", ""labelColor"": ""#FF0000FF"", ""valueColor"": ""#00FF00FF"", ""trackColor"": ""#0000FFFF"" },
                { ""kind"": ""chart"", ""key"": ""frametimes"", ""chartBackgroundColor"": ""#101010FF"", ""chartBorderColor"": ""#202020FF"" },
                { ""kind"": ""text"", ""text"": ""CPU"", ""color"": ""#303030FF"" } ] }");
            var document = OverlayTileDocument.FromJson(json, "Styles", Array.Empty<OsdTelemetrySource>());

            Assert.AreEqual(0.75, document.TileOpacity);
            Assert.AreEqual("#0000FFFF", document.Tiles[0].TrackColor);
            Assert.AreEqual("#101010FF", document.Tiles[1].ChartBackgroundColor);
            Assert.AreEqual("#303030FF", document.Tiles[2].Color);
            var exchanged = OverlayTileDocument.ToJson(document);
            Assert.AreEqual(0.75, (double)exchanged["tileOpacity"]);
            Assert.AreEqual("#202020FF", (string)exchanged["tiles"][1]["chartBorderColor"]);
            Assert.AreEqual(document.ToJson(), OverlayTileDocument.FromJson(exchanged, "Styles", Array.Empty<OsdTelemetrySource>()).ToJson());

            var opacity = Assert.ThrowsExactly<OverlayDesignAuthoringException>(() => OverlayTileDocument.FromJson(
                JObject.Parse(@"{ ""tileOpacity"": 2, ""tiles"": [ { ""kind"": ""metric"" } ] }"), "Bad", Array.Empty<OsdTelemetrySource>()));
            StringAssert.Contains(opacity.Message, "tileOpacity");
            var font = Assert.ThrowsExactly<OverlayDesignAuthoringException>(() => OverlayTileDocument.FromJson(
                JObject.Parse(@"{ ""tiles"": [ { ""kind"": ""metric"" }, { ""kind"": ""text"", ""family"": ""Consolas"" } ] }"), "Bad", Array.Empty<OsdTelemetrySource>()));
            StringAssert.Contains(font.Message, "Tile 1");

            string guide = OverlayTileDesignGuide.Text;
            foreach (string field in OsdTileStyle.Fields.Append("tileOpacity"))
                StringAssert.Contains(guide, field, $"The design guide must explain {field}.");
        }

        [TestMethod]
        public void StyleTabHoldsTheTileOpacityAndTheFontAndNoColors()
        {
            using var host = new EditorHost(SingleTile(Tile("metric")));
            var paths = LogicalDescendants<FrameworkElement>(host.Editor).SelectMany(element => BindingPaths(element)).ToArray();
            foreach (string removed in new[] { "Document.Accent", "Document.TileColor", "Document.TextColor", "SelectedTile.Family" })
                CollectionAssert.DoesNotContain(paths, removed, $"The editor still edits {removed}.");

            var styleTab = (TabItem)host.Editor.FindName("StyleTab");
            var slider = LogicalDescendants<Slider>(styleTab).Single();
            Assert.AreEqual("Document.TileOpacity", BindingOperations.GetBindingExpression(slider, RangeBase.ValueProperty)?.ParentBinding.Path.Path);
            Assert.AreEqual(0, slider.Minimum);
            Assert.AreEqual(1, slider.Maximum);
            Assert.IsTrue(LogicalDescendants<ComboBox>(styleTab).Any(combo =>
                BindingOperations.GetBindingExpression(combo, ComboBox.TextProperty)?.ParentBinding.Path.Path == "Document.FontFamily"));

            slider.Value = 0.6;
            Settle();
            Assert.IsTrue(host.Editor.TryCommitEdits());
            var scene = JsonNode.Parse(host.Editor.GetTemplateJson());
            Assert.AreEqual(0.6, scene["editor"]["tileOpacity"].GetValue<double>(), 1e-9);
            Assert.AreEqual("#20242C8D", scene["root"]["children"][0]["color"].GetValue<string>(), "235 x 0.6 = 141.");
        }

        [TestMethod]
        public void TileStyleShowsTheRowsOfTheSelectedKindWithTheirDefaults()
        {
            var document = new OsdDesignDocument { Name = "Kinds", IsCanvas = true, Width = 1200, CanvasHeight = 1200, GridSize = 1 };
            int index = 0;
            foreach (var tile in AllKinds())
            {
                Frame(tile, index % 4 * 300, index / 4 * 300, 280, 280);
                document.Tiles.Add(tile);
                index++;
            }
            using var host = new EditorHost(document);
            foreach (var tile in host.Editor.Document.Tiles.ToArray())
            {
                Assert.IsTrue(host.Editor.SelectElement(tile));
                Settle();
                var parts = OsdTileStyle.PartsOf(tile);
                foreach (string field in OsdTileStyle.Fields)
                {
                    var input = StyleField(host.Editor, field);
                    var row = (StackPanel)((Grid)input.Parent).Parent;
                    var part = parts.FirstOrDefault(candidate => candidate.Field == field);
                    Assert.AreEqual(part != null ? Visibility.Visible : Visibility.Collapsed, row.Visibility, $"{tile.Kind} {tile.GroupDisplay}: {field}");
                    if (part == null) continue;
                    Assert.AreEqual(part.Caption, ((TextBlock)row.Children[0]).Text);
                    Assert.AreEqual(string.Empty, input.Text, "An unset color shows no value of its own.");
                    var hint = ((Grid)input.Parent).Children.OfType<TextBlock>().Single();
                    Assert.AreEqual(OsdTileStyle.Resolve(host.Editor.Document, tile, field), hint.Text, $"{tile.Kind}: {field} default");
                    Assert.AreEqual(Visibility.Visible, hint.Visibility, "The default shows while the field is empty.");
                }
            }
        }

        [TestMethod]
        public void EditingAStyleRowColorsItsPartAndUndoRestoresIt()
        {
            var document = new OsdDesignDocument { Name = "Rows", IsCanvas = true, Width = 600, CanvasHeight = 400, GridSize = 1 };
            document.Tiles.Add(Frame(Tile("bar"), 0, 0, 280, 120));
            document.Tiles.Add(Frame(Group("bar"), 0, 130, 580, 260));
            using var host = new EditorHost(document);
            var bar = host.Editor.Document.Tiles[0];
            Assert.IsTrue(host.Editor.SelectElement(bar));
            Settle();
            string before = host.Editor.GetTemplateJson();
            var label = StyleField(host.Editor, OsdTileStyle.Label);

            SetText(label, "#FF0000FF");
            Assert.IsTrue(host.Editor.TryCommitEdits());
            Assert.AreEqual("#FF0000FF", bar.LabelColor);
            Assert.AreEqual("#FF0000FF", JsonNode.Parse(host.Editor.GetTemplateJson())["root"]["children"][0]["children"][0]["labelColor"].GetValue<string>());
            var hint = ((Grid)label.Parent).Children.OfType<TextBlock>().Single();
            Settle();
            Assert.AreEqual(Visibility.Collapsed, hint.Visibility, "A color of its own hides the default.");

            SetText(label, "red");
            Assert.IsTrue(Validation.GetHasError(label), "Only hex colors are colors.");
            Assert.AreEqual("#FF0000FF", bar.LabelColor, "A rejected color changes nothing.");
            SetText(label, "#FF0000FF");
            Assert.IsFalse(Validation.GetHasError(label));

            ((Button)host.Editor.FindName("UndoButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Settle();
            Assert.AreEqual(before, host.Editor.GetTemplateJson(), "Undo restores the design.");
            Assert.IsNull(host.Editor.Document.Tiles[0].LabelColor);

            var group = host.Editor.Document.Tiles[1];
            Assert.IsTrue(host.Editor.SelectElement(group));
            Settle();
            SetText(StyleField(host.Editor, OsdTileStyle.Primary), "#00FF00FF");
            Assert.IsTrue(host.Editor.TryCommitEdits());
            Assert.AreEqual("#00FF00FF", group.Color);
            Assert.AreEqual(Accent, group.TitleColor, "The title keeps its color when the bars change.");
        }

        [TestMethod]
        public void ResetAndApplyButtonsChangeOnlyTheirTiles()
        {
            var document = new OsdDesignDocument { Name = "Buttons", IsCanvas = true, Width = 600, CanvasHeight = 300, GridSize = 1 };
            document.Tiles.Add(Frame(Tile("metric"), 0, 0, 180, 90));
            document.Tiles.Add(Frame(Tile("metric"), 200, 0, 180, 90));
            document.Tiles.Add(Frame(Tile("bar"), 400, 0, 180, 90));
            using var host = new EditorHost(document);
            var tiles = host.Editor.Document.Tiles;
            Assert.IsTrue(host.Editor.SelectElement(tiles[0]));
            Settle();
            SetText(StyleField(host.Editor, OsdTileStyle.Value), "#00FF00FF");
            SetText(StyleField(host.Editor, OsdTileStyle.Background), "#000000CC");
            Assert.IsTrue(host.Editor.TryCommitEdits());

            ((Button)host.Editor.FindName("CopyTileStyleButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Settle();
            Assert.AreEqual("#00FF00FF", tiles[1].ValueColor);
            Assert.AreEqual("#000000CC", tiles[1].BackgroundColor);
            Assert.IsNull(tiles[2].ValueColor, "A bar is another kind.");
            var status = LogicalDescendants<TextBlock>(host.Editor).Single(block =>
                BindingOperations.GetBindingExpression(block, TextBlock.TextProperty)?.ParentBinding.Path.Path == "Status");
            Assert.AreEqual(OsdLocalization.Format("Editor_CopiedTileStyle", 1), status.Text);

            ((Button)host.Editor.FindName("ResetTileColorsButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Settle();
            Assert.IsNull(tiles[0].ValueColor);
            Assert.IsNull(tiles[0].BackgroundColor);
            Assert.AreEqual("#00FF00FF", tiles[1].ValueColor, "Reset changes only the selected tile.");
        }

        [TestMethod]
        public void KindChangesInTheEditorKeepTheColorsTheNewKindDraws()
        {
            using var host = new EditorHost(SingleTile(Tile("metric")));
            var tile = host.Editor.SelectedElement;
            tile.LabelColor = "#FF0000FF"; tile.UnitColor = "#0000FFFF";
            tile.Kind = "gauge";
            Settle();
            Assert.AreEqual("#FF0000FF", tile.LabelColor, "A gauge draws its label color.");
            Assert.IsNull(tile.UnitColor, "A gauge draws no unit color.");
            Assert.AreEqual(Visibility.Visible, ((FrameworkElement)((Grid)StyleField(host.Editor, OsdTileStyle.Track).Parent).Parent).Visibility,
                "The gauge offers its track.");
        }

        // ---------------------------------------------------------------- fixtures

        private static OsdDesignTile Tile(string kind)
        {
            var tile = new OsdDesignTile { Kind = kind, Label = kind.ToUpperInvariant(), Key = "load", Unit = "%" };
            switch (kind)
            {
                case "chart": tile.Key = "frametimes"; tile.Unit = "ms"; tile.Label = "FRAME TIME"; break;
                case "text": tile.Text = "TEXT"; tile.Key = null; tile.Unit = ""; tile.ValueSize = 28; break;
                case "icon": tile.Glyph = ""; tile.Family = "Segoe MDL2 Assets"; tile.Key = null; tile.Unit = ""; tile.Padding = 0; break;
                case "image":
                    tile.DataUri = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";
                    tile.Fit = "contain"; tile.Opacity = 1; tile.Key = null; tile.Unit = ""; tile.Padding = 0;
                    break;
                case "container": tile.Key = null; tile.Unit = ""; tile.Padding = 8; break;
                case "classicRows": tile.Key = null; tile.Unit = ""; tile.Height = 120; break;
            }
            return tile;
        }

        private static OsdDesignTile Group(string display)
        {
            var group = new OsdDesignTile
            {
                Kind = "group", Label = "LOADS", Key = "role:none", GroupKey = "role:none", GroupDisplay = display, GroupColumns = 2,
                Unit = display == "heatmap" ? "%" : "MHz", Minimum = 0, Maximum = 100,
                GroupMembers = new List<OsdGroupMember>
                {
                    new OsdGroupMember { Key = "m0", Label = "C0", IsEnabled = true },
                    new OsdGroupMember { Key = "m1", Label = "C1", IsEnabled = true }
                }
            };
            return group;
        }

        private static IEnumerable<OsdDesignTile> AllKinds() => new[]
        {
            Tile("metric"), Tile("text"), Tile("icon"), Tile("bar"), Tile("gauge"), Tile("chart"),
            Group("metric"), Group("bar"), Group("heatmap"), Tile("classicRows"), Tile("image"), Tile("container")
        };

        private static OsdDesignTile Frame(OsdDesignTile tile, double x, double y, double width, double height)
        {
            tile.X = x; tile.Y = y; tile.FrameWidth = width; tile.FrameHeight = height;
            return tile;
        }

        private static OsdDesignDocument SingleTile(OsdDesignTile tile)
        {
            var document = new OsdDesignDocument { Name = "Style", IsCanvas = true, Width = 420, CanvasHeight = 320, GridSize = 1 };
            document.Tiles.Add(Frame(tile, 10, 10, 400, 300));
            return document;
        }

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

        // ---------------------------------------------------------------- native rendering

        private const int RenderWidth = 1000, RenderHeight = 800;

        private static byte[] Render(OsdDesignDocument document)
        {
            string json = document.ToJson();
            var metrics = new JsonObject { ["load"] = 64, ["m0"] = 40, ["m1"] = 80 };
            // A heatmap reads averaged keys; at full load its cells draw their full color.
            foreach (string key in HeatmapKeys(JsonNode.Parse(json))) metrics[key] = 100;
            const int stride = RenderWidth * 4;
            using var preview = new OsdPreview();
            Assert.IsTrue(preview.CreateBitmap(RenderWidth, RenderHeight), "Native bitmap renderer did not initialize.");
            Assert.IsTrue(preview.LoadTemplateJson(json), "Native renderer rejected the design.");
            Assert.IsTrue(preview.SetMetricsJson(metrics.ToJsonString()));
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

        private static IEnumerable<string> HeatmapKeys(JsonNode node)
        {
            if (node is JsonObject obj)
            {
                if (obj["type"]?.GetValue<string>() == "heatmap")
                    foreach (var key in obj["keys"].AsArray()) yield return key.GetValue<string>();
                foreach (var property in obj.Where(property => property.Key != "editor"))
                    foreach (string key in HeatmapKeys(property.Value)) yield return key;
            }
            else if (node is JsonArray array)
                foreach (var item in array)
                    foreach (string key in HeatmapKeys(item)) yield return key;
        }

        private static (int R, int G, int B) Rgb(string color) => (
            int.Parse(color.Substring(1, 2), NumberStyles.HexNumber),
            int.Parse(color.Substring(3, 2), NumberStyles.HexNumber),
            int.Parse(color.Substring(5, 2), NumberStyles.HexNumber));

        // A pixel shows `color` when it mixes it over `under` with at least 75 % coverage: full
        // glyph stems, fills and the inner pixels of thin lines, never another color's edges.
        private static bool Shows(byte[] pixels, int offset, (int R, int G, int B) color, (int R, int G, int B) under)
        {
            if (pixels[offset + 3] < 200) return false;
            double? coverage = null;
            foreach (var (pixel, target, back) in new[] { (pixels[offset + 2], color.R, under.R), (pixels[offset + 1], color.G, under.G), (pixels[offset], color.B, under.B) })
            {
                int span = target - back;
                if (Math.Abs(span) < 48)
                {
                    if (Math.Abs(pixel - back) > 24 && Math.Abs(pixel - target) > 24) return false;
                    continue;
                }
                double channel = (pixel - back) / (double)span;
                if (coverage == null) coverage = channel;
                else if (Math.Abs(coverage.Value - channel) > 0.15) return false;
            }
            return coverage >= 0.75 && coverage <= 1.1;
        }

        private static int Exact(byte[] pixels, (int R, int G, int B) color)
        {
            int count = 0;
            for (int offset = 0; offset < pixels.Length; offset += 4)
                if (pixels[offset + 3] > 250 && Math.Abs(pixels[offset + 2] - color.R) <= 2 && Math.Abs(pixels[offset + 1] - color.G) <= 2
                    && Math.Abs(pixels[offset] - color.B) <= 2) count++;
            return count;
        }

        private static int Count(byte[] pixels, (int R, int G, int B) color, (int R, int G, int B) under)
        {
            int count = 0;
            for (int offset = 0; offset < pixels.Length; offset += 4)
                if (Shows(pixels, offset, color, under)) count++;
            return count;
        }

        private static Rect Bounds(byte[] pixels, string color, (int R, int G, int B) under)
        {
            var target = Rgb(color);
            double left = double.MaxValue, top = double.MaxValue, right = double.MinValue, bottom = double.MinValue;
            for (int offset = 0; offset < pixels.Length; offset += 4)
            {
                if (!Shows(pixels, offset, target, under)) continue;
                int pixel = offset / 4, x = pixel % RenderWidth, y = pixel / RenderWidth;
                left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x + 1); bottom = Math.Max(bottom, y + 1);
            }
            Assert.IsTrue(right > left, $"Nothing shows {color}.");
            return new Rect(left, top, right - left, bottom - top);
        }

        // ---------------------------------------------------------------- editor

        private static TextBox StyleField(OsdEditorControl editor, string field)
            => LogicalDescendants<TextBox>(editor).Single(box =>
                BindingOperations.GetBindingExpression(box, TextBox.TextProperty)?.ParentBinding.Path.Path == $"TileStyle[{field}].Value");

        private static IEnumerable<string> BindingPaths(FrameworkElement element)
        {
            foreach (var property in new DependencyProperty[] { TextBox.TextProperty, ComboBox.TextProperty, RangeBase.ValueProperty, Selector.SelectedItemProperty })
            {
                string path = BindingOperations.GetBindingExpression(element, property)?.ParentBinding.Path?.Path;
                if (path != null) yield return path;
            }
        }

        private static void SetText(TextBox field, string text)
        {
            field.SetCurrentValue(TextBox.TextProperty, text);
            field.GetBindingExpression(TextBox.TextProperty).UpdateSource();
        }

        private static IEnumerable<T> LogicalDescendants<T>(DependencyObject parent) where T : DependencyObject
        {
            foreach (object child in LogicalTreeHelper.GetChildren(parent))
            {
                if (child is not DependencyObject dependency) continue;
                if (dependency is T match) yield return match;
                foreach (T descendant in LogicalDescendants<T>(dependency)) yield return descendant;
            }
        }

        private static void Settle(int milliseconds = 1)
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromMilliseconds(milliseconds) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start();
            Dispatcher.PushFrame(frame);
        }

        private sealed class EditorHost : IDisposable
        {
            public OsdEditorControl Editor { get; }
            private readonly Window _window;

            public EditorHost(OsdDesignDocument document)
            {
                var host = new Border();
                TransitionAssist.SetDisableTransitions(host, true);
                host.Resources.MergedDictionaries.Add(new CustomColorTheme
                {
                    BaseTheme = BaseTheme.Dark,
                    PrimaryColor = Color.FromRgb(2, 113, 249),
                    SecondaryColor = Colors.Lime
                });
                foreach (string resource in new[]
                {
                    "MaterialDesignThemes.Wpf;component/Themes/MaterialDesign3.Defaults.xaml",
                    "CapFrameX.View;component/Styles/CxTextFields.xaml"
                })
                {
                    host.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/" + resource) });
                }
                Editor = new OsdEditorControl();
                Editor.SetTemplateJson(document.ToJson());
                host.Child = Editor;
                _window = new Window
                {
                    Content = host, Width = 1280, Height = 960, WindowStyle = WindowStyle.None,
                    ShowActivated = false, ShowInTaskbar = false, Left = -10000, Top = -10000
                };
                _window.Show();
                Settle();
                _window.UpdateLayout();
                Editor.ResetHistory();
            }

            public void Dispose()
            {
                _window.Close();
                Editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                _window.Content = null;
            }
        }
    }
}

using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CapFrameX.Contracts.Overlay;
using CapFrameX.OSD.Controls;
using CapFrameX.OSD.Integration;
using CapFrameX.OSD.Interop;
using CapFrameX.View.OverlayDesignAuthoring;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace CapFrameX.Test.Integration
{
    // The run history tile (feature request 487): a table that reserves a row for every run, so a
    // session never changes its height, and moves the tiles below it when the reserved runs change.
    [STATestClass]
    [DoNotParallelize]
    public class OverlayRunHistoryTileTest
    {
        // The renderer's shares of the font size (RunHistory.h): title 1.6, captions 1.3, a run 1.4, and
        // the result 0.4 + 1.4.
        private static double Content(int runs, double size = 14, bool result = true)
            => size * (1.6 + 1.3 + 1.4 * runs + (result ? 1.8 : 0));

        [TestMethod]
        public void ATableReservesTheRoomOfEveryRunAndTheRendererGetsIt()
        {
            var document = Canvas();
            var table = AddTable(document, 5);

            Assert.AreEqual(Math.Ceiling((Content(5) + 24) / 8) * 8, table.FrameHeight.Value, 1e-9,
                "The frame holds every reserved run, the padding and the result, rounded up to the grid.");
            var widget = Widget(document);
            Assert.AreEqual("runHistory", (string)widget["type"]);
            Assert.AreEqual(OsdDesignDocument.RunHistorySourceKey, (string)widget["key"]);
            Assert.AreEqual(5, (int)widget["runs"]);
            Assert.AreEqual(14 * 1.5, (double)widget["size"], 1e-9, "The text size follows the design's scale.");
            Assert.AreEqual((table.FrameHeight.Value - 24) * 1.5, (double)widget["height"], 1e-9);
            Assert.IsNull(widget["result"]);
            Assert.IsNull(widget["deviation"]);

            table.ShowResult = false;
            table.ShowDeviation = false;
            widget = Widget(document);
            Assert.AreEqual(false, (bool)widget["result"]);
            Assert.AreEqual(false, (bool)widget["deviation"]);
            string json = document.ToJson();
            Assert.AreEqual(json, OsdDesignDocument.FromJson(json).ToJson(), "A run history opens in the editor unchanged.");
        }

        [TestMethod]
        public void MoreRunsPushTheTilesBelowDownAndFewerPullThemBack()
        {
            var document = Canvas();
            var table = AddTable(document, 5);
            double bottom = table.FrameHeight.Value;
            var below = Metric(0, bottom + 8, 296, 80);
            var further = Metric(0, bottom + 96, 296, 40);   // below the one below: the chain moves along
            var beside = Metric(304, 0, 296, 400);           // beside the table: stays
            var underBeside = Metric(304, 420, 296, 40);     // below the tile beside it: stays
            foreach (var tile in new[] { below, further, beside, underBeside }) document.Tiles.Add(tile);
            document.Validate();

            table.Runs = 10;
            document.FitRunHistory(table);

            double grown = table.FrameHeight.Value - bottom;
            Assert.AreEqual(Math.Ceiling((Content(10) + 24) / 8) * 8, table.FrameHeight.Value, 1e-9);
            Assert.AreEqual(bottom + 8 + grown, below.Y.Value, 1e-9, "The tile below moves with the table's lower edge.");
            Assert.AreEqual(bottom + 96 + grown, further.Y.Value, 1e-9, "So does the tile below that one.");
            Assert.AreEqual(0, beside.Y.Value, "A tile beside the table stays.");
            Assert.AreEqual(420, underBeside.Y.Value, "A tile below a tile beside the table stays.");
            document.Validate();

            table.Runs = 5;
            document.FitRunHistory(table);
            Assert.AreEqual(bottom, table.FrameHeight.Value, 1e-9);
            Assert.AreEqual(bottom + 8, below.Y.Value, 1e-9, "Fewer runs pull the tiles back.");
            Assert.AreEqual(bottom + 96, further.Y.Value, 1e-9);
            document.Validate();
        }

        [TestMethod]
        public void ARunCountTheCanvasCannotHoldGrowsTheCanvas()
        {
            var document = Canvas();
            document.CanvasHeight = 320;
            var table = AddTable(document, 3);
            var below = Metric(0, table.FrameHeight.Value + 8, 296, 80);
            document.Tiles.Add(below);

            table.Runs = 20;
            document.FitRunHistory(table);

            Assert.IsTrue(document.CanvasHeight >= below.Y.Value + below.FrameHeight.Value, "The canvas grows to hold the moved tiles.");
            document.Validate();
        }

        [TestMethod]
        public void AShrinkingTableBringsNoTileCloserThanTheGapToATileAboveItThatStays()
        {
            var document = Canvas();
            var table = AddTable(document, 5);                        // 192 high
            var beside = Metric(304, 0, 296, 170);                    // shorter than the table
            var wide = Metric(0, table.FrameHeight.Value + 8, 600, 40); // below the table and the tile beside it
            document.Tiles.Add(beside);
            document.Tiles.Add(wide);
            document.Validate();

            table.Runs = 3;
            document.FitRunHistory(table);

            Assert.AreEqual(170 + document.Gap, wide.Y.Value, 1e-9,
                "The wide tile moves up only until the gap to the tile beside the table.");
            document.Validate();
        }

        [TestMethod]
        public void ATableWithoutTheRoomOfItsRunsAndRunsOnOtherTilesAreRefused()
        {
            var document = Canvas();
            var table = AddTable(document, 5);
            table.FrameHeight = Content(5) + 24 - 2;
            Assert.ThrowsExactly<FormatException>(document.Validate, "A table shorter than its rows would clip runs.");

            table.FrameHeight = document.RunHistoryFrameHeight(table);
            table.Runs = 21;
            Assert.ThrowsExactly<FormatException>(document.Validate, "At most 20 runs.");

            table.Runs = 3;
            table.FrameHeight = document.RunHistoryFrameHeight(table);
            document.Tiles.Add(new OsdDesignTile { Kind = "metric", Key = "fps", Label = "FPS", X = 0, Y = 400, FrameWidth = 120, FrameHeight = 48, Runs = 3 });
            Assert.ThrowsExactly<FormatException>(document.Validate, "Only a run history reserves runs.");
        }

        [TestMethod]
        public void InAFlowDesignTheHeightFollowsTheRuns()
        {
            var document = new OsdDesignDocument { Width = 520, Columns = 2, Padding = 12, Scale = 1 };
            var table = new OsdDesignTile { Kind = "runHistory", ColumnSpan = 2 };
            OsdDesignDocument.InitializeRunHistory(table, null, 4);
            document.Tiles.Add(table);

            var widget = Widget(document);
            Assert.AreEqual(Content(4), (double)widget["height"], 1e-9);
            Assert.AreEqual("Run History", table.Label);
            Assert.AreEqual(14, table.ValueSize);
        }

        [TestMethod]
        public void TheTitleFollowsTheResultColorUntilItHasItsOwn()
        {
            var document = Canvas();
            var table = AddTable(document, 3);
            CollectionAssert.AreEqual(new[] { OsdTileStyle.Title, OsdTileStyle.Primary, OsdTileStyle.Label, OsdTileStyle.Value,
                OsdTileStyle.Background, OsdTileStyle.Border }, OsdTileStyle.PartsOf(table).Select(part => part.Field).ToArray());
            Assert.IsNull(Widget(document)["titleColor"], "Unset colors stay off the scene.");

            OsdTileStyle.Set(document, table, OsdTileStyle.Primary, "#00FF00FF");
            Assert.AreEqual(document.Accent, (string)Widget(document)["titleColor"], "Changing the result color keeps the title's color.");
            Assert.AreEqual("#00FF00FF", (string)Widget(document)["color"]);
        }

        [TestMethod]
        public void TheTileDocumentGivesATableTheRoomOfItsRuns()
        {
            var document = OverlayTileDocument.FromJson(JObject.Parse(@"{
                ""width"": 320,
                ""tiles"": [
                    { ""kind"": ""runHistory"", ""runs"": 6, ""frameHeight"": 40 },
                    { ""kind"": ""metric"", ""key"": ""fps"", ""label"": ""FPS"", ""columnSpan"": 2 }
                ]}"), "Runs", Array.Empty<OsdTelemetrySource>());

            var table = document.Tiles[0];
            Assert.AreEqual(OsdDesignDocument.RunHistorySourceKey, table.Key);
            Assert.AreEqual(14, table.ValueSize);
            Assert.AreEqual(document.RunHistoryFrameHeight(table), table.FrameHeight.Value, 1e-9,
                "The frame follows the runs whatever height the client sent.");
            Assert.IsTrue(document.Tiles[1].Y >= table.Y + table.FrameHeight - 1e-9, "The tile below it moved along.");
            var rebuilt = OverlayTileDocument.FromJson(OverlayTileDocument.ToJson(document), document.Name, Array.Empty<OsdTelemetrySource>());
            Assert.AreEqual(document.ToJson(), rebuilt.ToJson());
        }

        [TestMethod]
        public void TheEditorBindsTheTableToTheHostsSourceAndOffersItToNoOtherTile()
        {
            var host = new OsdTelemetrySource { Key = "metric/RunHistoryTable", SemanticKey = RunHistoryTablePayload.SemanticKey, Name = "Run history (table)", IsText = true };
            var document = Canvas();
            var table = AddTable(document, 3);

            document.BindPresetSources(new[] { host });

            Assert.AreEqual("metric/RunHistoryTable", table.Key);
            Assert.IsTrue(OsdDesignDocument.IsRunHistorySource(host));
            Assert.AreEqual(RunHistoryTablePayload.SemanticKey, OsdDesignDocument.RunHistorySourceKey,
                "The editor and CapFrameX name the table alike.");
        }

        [TestMethod]
        public void TheRendererDrawsTheSessionThePayloadDescribes()
        {
            const int width = 400, height = 300, stride = width * 4;
            var document = Canvas();
            document.Scale = 1;
            document.Width = 320;
            document.CanvasHeight = 200;
            document.GridSize = 1;
            document.TileColor = "#000000FF";
            document.TextColor = "#808080FF";
            var table = AddTable(document, 4);
            var snapshot = new RunHistorySnapshot(4, "P1", "P0dot2", "Second", new[]
            {
                new RunHistoryRun(144.2, 118.3, 92.1, "P1", "P0dot2", 0, false),
                new RunHistoryRun(131.9, 102.6, 71.4, "P1", "P0dot2", -13.3, true)
            }, null);
            var metrics = new JsonObject { [OsdDesignDocument.RunHistorySourceKey] = RunHistoryTablePayload.Build(snapshot, true, 1, text => text) };

            using var preview = new OsdPreview();
            Assert.IsTrue(preview.CreateBitmap(width, height), "Native bitmap renderer did not initialize.");
            Assert.IsTrue(preview.LoadTemplateJson(document.ToJson()), "The renderer rejected the run history.");
            Assert.IsTrue(preview.SetMetricsJson(metrics.ToJsonString()));
            var pixels = Render(preview, width, height, stride);
            var tiles = new OsdPreviewTileRect[1];
            Assert.AreEqual(1, preview.GetLayout(out var layout, tiles));
            double scale = layout.Scale;
            Assert.AreEqual(table.FrameHeight.Value * scale, tiles[0].Height, 1.5, "The renderer keeps the room the editor reserved.");

            // Rows start below the title and the captions, inside the padding.
            double rowsTop = tiles[0].Y + (12 + 14 * (1.6 + 1.3)) * scale, row = 14 * 1.4 * scale;
            var outlier = Band(pixels, stride, tiles[0], rowsTop + row, row);
            var plain = Band(pixels, stride, tiles[0], rowsTop, row);
            Assert.IsTrue(outlier.Red > 2 * outlier.Green && outlier.Red > 1000, "The outlier draws in red.");
            Assert.IsTrue(plain.Red > 1000 && Math.Abs(plain.Red - plain.Green) < plain.Red * .2, "Another run draws the neutral value color.");
            var pending = Band(pixels, stride, tiles[0], rowsTop + row * 2, row);
            Assert.IsTrue(pending.Red > 0 && pending.Red < plain.Red, "Pending runs draw subdued placeholders.");
        }

        [TestMethod]
        public void TheRunsPickerOfTheInspectorMovesTheTilesBelowAlong()
        {
            var document = Canvas();
            var table = AddTable(document, 5);
            document.Tiles.Add(Metric(0, table.FrameHeight.Value + 8, 296, 80));
            using var host = new EditorHost(document);
            var editing = host.Editor.Document;
            var (runs, below) = (editing.Tiles[0], editing.Tiles[1]);
            Assert.IsTrue(host.Editor.SelectElement(runs));
            Settle();
            double before = runs.FrameHeight.Value, top = below.Y.Value;

            var picker = (ComboBox)host.Editor.FindName("RunHistoryRunsPicker");
            Assert.IsNotNull(picker, "The inspector offers the reserved runs.");
            Assert.AreEqual(5, picker.SelectedItem, "The picker shows the tile's runs.");
            picker.SelectedItem = 10;
            Assert.IsTrue(host.Editor.TryCommitEdits());

            Assert.AreEqual(10, runs.Runs);
            Assert.AreEqual(editing.RunHistoryFrameHeight(runs), runs.FrameHeight.Value, 1e-9);
            Assert.AreEqual(top + runs.FrameHeight.Value - before, below.Y.Value, 1e-9, "The tile below moved with the table.");

            picker.SelectedItem = 5;
            Assert.IsTrue(host.Editor.TryCommitEdits());
            Assert.AreEqual(before, runs.FrameHeight.Value, 1e-9);
            Assert.AreEqual(top, below.Y.Value, 1e-9, "Fewer runs pull it back.");
        }

        [TestMethod]
        public void ANewTableReservesTheRunsOfTheHostsSessions()
        {
            var document = Canvas();
            document.Tiles.Add(Metric(0, 0, 296, 48));
            using var host = new EditorHost(document);
            host.Editor.DefaultRunHistoryRuns = 7;

            typeof(OsdEditorControl).GetMethod("AddWidget", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(host.Editor, new object[] { "runHistory", 1, null });
            Assert.IsTrue(host.Editor.TryCommitEdits());

            var table = host.Editor.Document.Tiles.Single(tile => tile.IsRunHistory);
            Assert.AreEqual(7, table.Runs);
            Assert.AreEqual("Run History", table.Label);
            Assert.AreEqual(OsdDesignDocument.RunHistorySourceKey, table.Key, "Without a host the editor's sample table.");
            Assert.AreEqual(host.Editor.Document.RunHistoryFrameHeight(table), table.FrameHeight.Value, 1e-9);
            Assert.AreEqual(OsdDesignDocument.DefaultRunHistoryWidth, table.FrameWidth.Value, 1e-9);
        }

        private static void Settle(int milliseconds = 1)
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(DispatcherPriority.ApplicationIdle) { Interval = TimeSpan.FromMilliseconds(milliseconds) };
            timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
            timer.Start();
            Dispatcher.PushFrame(frame);
        }

        // The real editor in an off-screen window, as a user edits a design.
        private sealed class EditorHost : IDisposable
        {
            public OsdEditorControl Editor { get; }
            public Window Window { get; }

            public EditorHost(OsdDesignDocument document)
            {
                Editor = new OsdEditorControl();
                Editor.SetTemplateJson(document.ToJson());
                Window = new Window
                {
                    Content = Editor, Width = 1280, Height = 960,
                    WindowStyle = WindowStyle.None, ShowActivated = false,
                    ShowInTaskbar = false, Left = -10000, Top = -10000
                };
                Window.Show();
                Settle();
                Window.UpdateLayout();
                Editor.ResetHistory();
            }

            public void Dispose()
            {
                Window.Close();
                Editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                Window.Content = null;
            }
        }

        private static OsdDesignDocument Canvas() =>
            new OsdDesignDocument { Width = 640, CanvasHeight = 720, IsCanvas = true, Padding = 12, GridSize = 8, Scale = 1.5 };

        private static OsdDesignTile AddTable(OsdDesignDocument document, int runs)
        {
            var table = new OsdDesignTile { Kind = "runHistory", X = 0, Y = 0, FrameWidth = 296 };
            OsdDesignDocument.InitializeRunHistory(table, null, runs);
            table.FrameHeight = document.RunHistoryFrameHeight(table);
            document.Tiles.Add(table);
            document.Validate();
            return table;
        }

        private static OsdDesignTile Metric(double x, double y, double width, double height) =>
            new OsdDesignTile { Kind = "metric", Key = "fps", Label = "FPS", X = x, Y = y, FrameWidth = width, FrameHeight = height };

        private static JsonObject Widget(OsdDesignDocument document)
        {
            var scene = JsonNode.Parse(document.ToJson());
            return scene["root"]["children"].AsArray().Select(panel => panel["children"][0].AsObject())
                .First(widget => (string)widget["type"] == "runHistory");
        }

        private static byte[] Render(OsdPreview preview, int width, int height, int stride)
        {
            var pixels = new byte[stride * height];
            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try { Assert.IsTrue(preview.RenderBitmap(handle.AddrOfPinnedObject(), pixels.Length, stride, 0.05)); }
            finally { handle.Free(); }
            return pixels;
        }

        private readonly struct Ink
        {
            public Ink(double red, double green) { Red = red; Green = green; }
            public double Red { get; }
            public double Green { get; }
        }

        // The red and green sums of the tile's rows from `top` (premultiplied BGRA); the preview draws a
        // checkerboard around the design.
        private static Ink Band(byte[] pixels, int stride, OsdPreviewTileRect tile, double top, double height)
        {
            double red = 0, green = 0;
            int height0 = pixels.Length / stride;
            for (int y = Math.Max(0, (int)Math.Ceiling(top)); y < Math.Min(height0, (int)Math.Floor(top + height)); y++)
                for (int x = Math.Max(0, (int)Math.Ceiling(tile.X)); x < Math.Min(stride / 4, (int)Math.Floor(tile.X + tile.Width)); x++)
                {
                    red += pixels[y * stride + x * 4 + 2];
                    green += pixels[y * stride + x * 4 + 1];
                }
            return new Ink(red, green);
        }
    }
}

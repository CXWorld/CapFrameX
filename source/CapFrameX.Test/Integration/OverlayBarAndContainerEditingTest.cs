using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using CapFrameX.OSD.Controls;
using CapFrameX.OSD.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    // Two requests from a user of the tile editor: trimming the empty space out of a container without
    // scaling its contents, and single bar tiles that stand upright or carry a thicker track.
    [STATestClass]
    [DoNotParallelize]
    public class OverlayBarAndContainerEditingTest
    {
        // The renderer's vertical bar gives its value and its label one caption line each (12 px × 1.3 + 4).
        private const double Caption = 12 * 1.3 + 4;

        [TestMethod]
        public void TrimmingAContainerCutsTheEmptySpaceOnEverySideAndKeepsItsChildrenInPlace()
        {
            var document = Canvas();
            var card = AddCard(document);
            var onScreen = card.Children.Select(document.AbsoluteTileFrame).ToArray();

            document.TrimContainer(card);

            CollectionAssert.AreEqual(onScreen, card.Children.Select(document.AbsoluteTileFrame).ToArray(), "The contents moved on the screen.");
            Assert.AreEqual(new Rect(56, 56, 120 + 24, 88 + 24), document.TileFrame(card));
            Assert.AreEqual(new Rect(0, 0, 120, 88), document.ContainerContentBounds(card));
            Assert.AreEqual(80.0, card.Children[0].FrameWidth.Value, "Trimming scales nothing.");
            AssertRoundTrip(document);
        }

        [TestMethod]
        public void TrimmingAnEmptyContainerIsRefused()
        {
            var document = Canvas();
            var card = new OsdDesignTile { Kind = "container", Label = "Card", X = 0, Y = 0, FrameWidth = 200, FrameHeight = 160 };
            document.Tiles.Add(card);

            Assert.Throws<FormatException>(() => document.TrimContainer(card));
            Assert.AreEqual(new Rect(0, 0, 200, 160), document.TileFrame(card));
        }

        [TestMethod]
        public void AContainerResizedWithoutScalingKeepsItsChildrenAndRefusesToCutThemOff()
        {
            var document = Canvas();
            var card = AddCard(document);
            var children = card.Children.Select(document.TileFrame).ToArray();

            document.ResizeContainer(card, 136 + 24, 112 + 24, scaleChildren: false);

            CollectionAssert.AreEqual(children, card.Children.Select(document.TileFrame).ToArray());
            Assert.AreEqual(new Size(160, 136), document.TileFrame(card).Size);
            Assert.Throws<FormatException>(() => document.ResizeContainer(card, 135 + 24, 136, scaleChildren: false));
            Assert.AreEqual(new Size(160, 136), document.TileFrame(card).Size, "A refused resize changes nothing.");
            CollectionAssert.AreEqual(children, card.Children.Select(document.TileFrame).ToArray());
            AssertRoundTrip(document);
        }

        [TestMethod]
        public void WithScalingSwitchedOffADragStopsWhereTheFrameReachesTheContents()
        {
            var document = Canvas();
            AddCard(document);
            using var host = new EditorHost(document);
            var card = host.Editor.Document.Tiles[0];
            Assert.IsTrue(host.Editor.SelectElement(card));
            Settle();
            var children = card.Children.Select(host.Editor.Document.TileFrame).ToArray();
            var scaleContents = (CheckBox)host.Editor.FindName("ScaleContainerContentsBox");
            Assert.IsTrue(scaleContents.IsChecked == true, "Resizing scales the contents unless the user switches it off.");
            scaleContents.SetCurrentValue(ToggleButton.IsCheckedProperty, false);

            var handle = (Thumb)host.Editor.FindName("WidthHandle");
            InvokeEditor(host.Editor, "BeginResize", handle, new Point(0, 0));
            InvokeEditor(host.Editor, "UpdateResize", new Point(-2000, 0));
            handle.RaiseEvent(new DragCompletedEventArgs(0, 0, false) { RoutedEvent = Thumb.DragCompletedEvent });
            Settle();

            Assert.IsTrue(host.Editor.TryCommitEdits());
            Assert.AreEqual(136 + 24, card.FrameWidth.Value, .001, "The frame stops at the right edge of the contents.");
            CollectionAssert.AreEqual(children, card.Children.Select(host.Editor.Document.TileFrame).ToArray(), "The contents were scaled.");
        }

        [TestMethod]
        [DataRow("WidthHandle", 120.0)]
        [DataRow("HeightHandle", 80.0)]
        public void AContainerWhoseContentsScaleKeepsItsProportionsAndScalesThemEvenlyBothWays(string handleName, double delta)
        {
            // Before, a handle scaled one axis and the text by the smaller factor: a narrower card shrank
            // its metric's text, a wider one did not grow it, and narrower-then-wider left it small.
            using var host = new EditorHost(ScalingCard());
            var card = host.Editor.Document.Tiles[0];
            var metric = card.Children[0];
            Assert.IsTrue(host.Editor.SelectElement(card));
            Settle();

            Drag(host, handleName, delta);
            double factor = handleName == "WidthHandle" ? (296 + delta) / 296 : (216 + delta) / 216;
            // The side that follows is rounded to hundredths (0.005 at most).
            Assert.AreEqual(296 * factor + 24, card.FrameWidth.Value, .006);
            Assert.AreEqual(216 * factor + 24, card.FrameHeight.Value, .006, "The other side follows the handle.");
            Assert.AreEqual(Math.Round(handleName == "WidthHandle" ? card.FrameHeight.Value : card.FrameWidth.Value, 2),
                handleName == "WidthHandle" ? card.FrameHeight.Value : card.FrameWidth.Value, "The inspector shows hundredths at most.");
            Assert.AreEqual(160 * factor, metric.FrameWidth.Value, .006);
            Assert.AreEqual(96 * factor, metric.FrameHeight.Value, .006);
            Assert.AreEqual(28 * factor, metric.ValueSize, 1e-3, "The value grows with the container.");
            Assert.AreEqual(13 * factor, metric.LabelSize.Value, 1e-3);

            Drag(host, handleName, -delta);
            Assert.AreEqual(new Rect(40, 32, 320, 240), host.Editor.Document.TileFrame(card), "Back at its size, the frame reads exactly as before.");
            Assert.AreEqual(new Rect(16, 24, 160, 96), Round(host.Editor.Document.TileFrame(metric)));
            Assert.AreEqual(28, metric.ValueSize, 1e-3, "Back at its size, the container shows its contents as before.");
            Assert.AreEqual(13, metric.LabelSize.Value, 1e-3);
        }

        [TestMethod]
        public void ATypedWidthTakesTheHeightAlongOnlyWhileTheContentsScale()
        {
            using var host = new EditorHost(ScalingCard());
            var card = host.Editor.Document.Tiles[0];
            var metric = card.Children[0];
            Assert.IsTrue(host.Editor.SelectElement(card));
            Settle();
            var state = typeof(OsdEditorControl).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(host.Editor);

            state.GetType().GetProperty("SelectedFrameWidth").SetValue(state, 444.0);
            Assert.IsTrue(host.Editor.TryCommitEdits());
            Assert.AreEqual(Math.Round(216 * 420.0 / 296 + 24, 2), card.FrameHeight.Value, 1e-9, "The height follows, in hundredths.");
            Assert.AreEqual(28 * 420.0 / 296, metric.ValueSize, 1e-3);

            state.GetType().GetProperty("ScaleContainerContents").SetValue(state, false);
            double height = card.FrameHeight.Value, valueSize = metric.ValueSize;
            state.GetType().GetProperty("SelectedFrameWidth").SetValue(state, 480.0);
            Assert.IsTrue(host.Editor.TryCommitEdits());
            Assert.AreEqual(480.0, card.FrameWidth.Value);
            Assert.AreEqual(height, card.FrameHeight.Value, "Without scaling only the typed side changes.");
            Assert.AreEqual(valueSize, metric.ValueSize);
        }

        [TestMethod]
        public void TheTrimButtonTrimsTheSelectedContainerAsOneUndoableEdit()
        {
            var document = Canvas();
            AddCard(document);
            using var host = new EditorHost(document);
            Assert.IsTrue(host.Editor.SelectElement(host.Editor.Document.Tiles[0]));
            Settle();
            string before = host.Editor.GetTemplateJson();
            var trim = (Button)host.Editor.FindName("TrimContainerButton");
            Assert.IsTrue(trim.IsEnabled);

            trim.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Settle();

            Assert.IsTrue(host.Editor.TryCommitEdits());
            Assert.AreEqual(new Rect(56, 56, 144, 112), host.Editor.Document.TileFrame(host.Editor.Document.Tiles[0]));
            ((Button)host.Editor.FindName("UndoButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Settle();
            Assert.AreEqual(before, host.Editor.GetTemplateJson(), "One undo restores the container.");
        }

        [TestMethod]
        [DataRow(1.0, true, "GPU")]
        [DataRow(1.0, false, "GPU")]
        [DataRow(1.5, true, "")]
        [DataRow(1.5, false, "")]
        public void AVerticalBarFillsItsFrameBetweenItsValueAndItsLabel(double scale, bool showValue, string label)
        {
            var document = Canvas(scale);
            document.Tiles.Add(Bar(vertical: true, label, showValue));

            var widget = Widget(document, 0);

            int captions = (showValue ? 1 : 0) + (label.Length > 0 ? 1 : 0);
            Assert.AreEqual("vertical", (string)widget["orientation"]);
            Assert.AreEqual((200 - 24 - captions * Caption) * scale, (double)widget["height"], 1e-9);
            Assert.AreEqual((64 - 24) * scale, (double)widget["width"], 1e-9);
            AssertRoundTrip(document);
        }

        [TestMethod]
        public void AVerticalBarInAGridIsAsTallAsItsHeight()
        {
            var document = new OsdDesignDocument { Width = 320, Columns = 2, Padding = 12 };
            var bar = Bar(vertical: true, "GPU", true);
            bar.X = bar.Y = bar.FrameWidth = bar.FrameHeight = null;
            bar.Height = 120;
            document.Tiles.Add(bar);

            Assert.AreEqual(120.0, (double)Widget(document, 0)["height"], 1e-9);
            AssertRoundTrip(document);
        }

        [TestMethod]
        public void AHorizontalBarDrawsItsThicknessAndAnUnchangedBarExportsAsBefore()
        {
            var document = Canvas(1.5);
            var bar = Bar(vertical: false, "GPU", true);
            document.Tiles.Add(bar);

            var widget = Widget(document, 0);
            Assert.AreEqual(8 * 1.5, (double)widget["height"], 1e-9);
            Assert.IsNull(widget["orientation"]);
            Assert.IsNull(widget["editor"]["orientation"], "An unchanged bar keeps its former editor metadata.");
            Assert.IsNull(widget["editor"]["thickness"]);

            bar.Thickness = 20;
            Assert.AreEqual(20 * 1.5, (double)Widget(document, 0)["height"], 1e-9);
            AssertRoundTrip(document);
        }

        [TestMethod]
        public void AVerticalBarMayBeNarrowerThanOtherTilesAtTheTopLevel()
        {
            var document = Canvas();
            var bar = Bar(vertical: true, "", false);
            bar.FrameWidth = 8 + 24;
            document.Tiles.Add(bar);
            document.Validate();

            bar.Orientation = null;
            Assert.Throws<FormatException>(() => document.Validate());
        }

        [TestMethod]
        public void OnlyHorizontalAndVerticalAreOrientations()
        {
            var bar = Bar(vertical: false, "GPU", true);
            bar.Orientation = "horizontal";
            Assert.IsNull(bar.Orientation, "Horizontal is the default and is stored as such.");

            var document = Canvas();
            document.Tiles.Add(bar);
            bar.Orientation = "diagonal";
            Assert.Throws<FormatException>(() => document.Validate());
        }

        [TestMethod]
        [DataRow(@"{ ""kind"": ""bar"", ""key"": ""load"", ""label"": ""GPU"", ""orientation"": ""diagonal"", ""x"": 0, ""y"": 0, ""frameWidth"": 64, ""frameHeight"": 200 }", "orientation")]
        [DataRow(@"{ ""kind"": ""bar"", ""key"": ""load"", ""label"": ""GPU"", ""x"": 0, ""y"": 0, ""frameWidth"": 32, ""frameHeight"": 56 }", "Tile width")]
        public void TheTileDocumentRejectsAnUnknownOrientationAndANarrowHorizontalBar(string tile, string reason)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var error = Assert.ThrowsExactly<CapFrameX.Contracts.Overlay.OverlayDesignAuthoringException>(() =>
                CapFrameX.View.OverlayDesignAuthoring.OverlayTileDocument.FromJson(
                    Newtonsoft.Json.Linq.JObject.Parse(@"{ ""layout"": ""canvas"", ""width"": 240, ""tiles"": [" + tile + "] }"),
                    "Invalid bar", Array.Empty<OsdTelemetrySource>()));
            StringAssert.Contains(error.Message, "Tile 0");
            StringAssert.Contains(error.Message, reason);
            Assert.IsTrue(watch.Elapsed < TimeSpan.FromSeconds(2), "Rejecting the design took " + watch.Elapsed);
        }

        [TestMethod]
        public void TheBarDirectionTurnsTheSelectedBarAndOnlyAHorizontalBarHasAThickness()
        {
            var document = Canvas();
            document.Tiles.Add(Bar(vertical: false, "GPU", true));
            using var host = new EditorHost(document);
            var bar = host.Editor.Document.Tiles[0];
            Assert.IsTrue(host.Editor.SelectElement(bar));
            Settle();
            var direction = (ComboBox)host.Editor.FindName("BarDirection");
            var thickness = (Slider)host.Editor.FindName("BarThicknessSlider");
            Assert.AreEqual("Horizontal", direction.SelectedItem);
            Assert.AreEqual(Visibility.Visible, ((FrameworkElement)thickness.Parent).Visibility);

            direction.SetCurrentValue(Selector.SelectedItemProperty, "Vertical");
            Settle();
            Assert.IsTrue(host.Editor.TryCommitEdits());
            Assert.AreEqual("vertical", bar.Orientation);
            Assert.AreEqual(new Rect(16, 8, 56, 240), host.Editor.Document.TileFrame(bar), "The frame turns with the bar.");
            Assert.AreEqual(Visibility.Collapsed, ((FrameworkElement)thickness.Parent).Visibility, "A vertical bar spans its tile's width.");

            direction.SetCurrentValue(Selector.SelectedItemProperty, "Horizontal");
            Settle();
            Assert.IsTrue(host.Editor.TryCommitEdits());
            Assert.IsNull(bar.Orientation);
            Assert.AreEqual(new Rect(16, 8, 240, 56), host.Editor.Document.TileFrame(bar), "A horizontal bar takes its natural height.");
        }

        [TestMethod]
        public void AThickerTrackMakesTheBarTileGrowAndAnotherKindForgetsIt()
        {
            var document = Canvas();
            document.Tiles.Add(Bar(vertical: false, "GPU", true));
            using var host = new EditorHost(document);
            var bar = host.Editor.Document.Tiles[0];
            Assert.IsTrue(host.Editor.SelectElement(bar));
            Settle();

            ((Slider)host.Editor.FindName("BarThicknessSlider")).SetCurrentValue(RangeBase.ValueProperty, 30.0);
            Settle();
            Assert.IsTrue(host.Editor.TryCommitEdits());

            Assert.AreEqual(30.0, bar.Thickness);
            Assert.AreEqual(30.0, (double)Widget(host.Editor.Document, 0)["height"], 1e-9);
            Assert.AreEqual(80.0, bar.FrameHeight.Value, "The caption line and the track need 54 px plus the padding.");

            bar.Orientation = "vertical";
            bar.Kind = "gauge";
            Settle();
            Assert.IsNull(bar.Thickness, "A bar's track is no gauge's arc.");
            Assert.IsNull(bar.Orientation);
        }

        [TestMethod]
        [DataRow(true, 1.0)]
        [DataRow(true, 1.5)]
        [DataRow(false, 1.0)]
        [DataRow(false, 1.5)]
        public void TheRendererDrawsABarAsTheEditorLaysItOut(bool vertical, double scale)
        {
            const int width = 640, height = 480, stride = width * 4;
            var document = Canvas(scale);
            document.GridSize = 1;
            document.Accent = "#00FF80FF";
            document.TileColor = "#181818FF";
            var bar = Bar(vertical, "GPU", true);
            // The caption line (14 px × 1.3 + 4) and a 20 px track need 42.2 of the 48 px inside the frame.
            if (!vertical) { bar.FrameHeight = 72; bar.Thickness = 20; }
            document.Tiles.Add(bar);

            using var preview = new OsdPreview();
            Assert.IsTrue(preview.CreateBitmap(width, height), "Native bitmap renderer did not initialize.");
            Assert.IsTrue(preview.LoadTemplateJson(document.ToJson()), "The renderer rejected the bar.");
            Assert.IsTrue(preview.SetMetricsJson(JsonSerializer.Serialize(new { load = 100.0 })));
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
            var tiles = new OsdPreviewTileRect[1];
            Assert.AreEqual(1, preview.GetLayout(out var layout, tiles));
            double pixelScale = scale * layout.Scale;
            Rect fill = GreenBounds(pixels, width, height);
            Assert.IsFalse(fill.IsEmpty, "The full bar was not drawn.");

            if (vertical)
            {
                // The track spans the tile's width less the renderer's 2 px inset and fills its height
                // between the value above and the label below.
                Assert.AreEqual((64 - 24 - 4) * pixelScale, fill.Width, 2.1);
                Assert.AreEqual((200 - 24 - 2 * Caption) * pixelScale, fill.Height, 2.1);
                Assert.AreEqual(tiles[0].Y + (12 + Caption) * pixelScale, fill.Top, 2.1);
                Assert.AreEqual(tiles[0].Y + tiles[0].Height - (12 + Caption) * pixelScale, fill.Bottom, 2.1, "The track does not reach the label.");
            }
            else
            {
                Assert.AreEqual((240 - 24) * pixelScale, fill.Width, 2.1);
                Assert.AreEqual(20 * pixelScale, fill.Height, 2.1, "The renderer ignored the thickness.");
            }
        }

        private static OsdDesignDocument Canvas(double scale = 1) =>
            new OsdDesignDocument { Width = 400, CanvasHeight = 480, IsCanvas = true, Padding = 12, GridSize = 8, Scale = scale };

        // A card at (40, 32) whose children cover (16, 24)..(136, 112) of its 276 x 216 canvas.
        private static OsdDesignTile AddCard(OsdDesignDocument document)
        {
            var card = new OsdDesignTile { Kind = "container", Label = "Card", X = 40, Y = 32, FrameWidth = 300, FrameHeight = 240 };
            card.Children.Add(new OsdDesignTile { Kind = "metric", Label = "FPS", Key = "fps", X = 16, Y = 24, FrameWidth = 80, FrameHeight = 40 });
            card.Children.Add(Bar(false, "GPU", true, 56, 72, 80, 40));
            document.Tiles.Add(card);
            document.Validate();
            return card;
        }

        // A 320 x 240 card at (40, 32), padding 12 (inside: 296 x 216), with a metric at (16, 24), 160 x 96.
        private static OsdDesignDocument ScalingCard()
        {
            var document = new OsdDesignDocument { Width = 640, CanvasHeight = 600, IsCanvas = true, Padding = 12, GridSize = 8 };
            var card = new OsdDesignTile { Kind = "container", Label = "Card", X = 40, Y = 32, FrameWidth = 320, FrameHeight = 240 };
            card.Children.Add(new OsdDesignTile { Kind = "metric", Label = "FPS", Key = "fps", Unit = "FPS", X = 16, Y = 24, FrameWidth = 160, FrameHeight = 96 });
            document.Tiles.Add(card);
            return document;
        }

        private static void Drag(EditorHost host, string handleName, double delta)
        {
            var handle = (Thumb)host.Editor.FindName(handleName);
            InvokeEditor(host.Editor, "BeginResize", handle, new Point(0, 0));
            var gesture = typeof(OsdEditorControl).GetField("_resize", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(host.Editor);
            double scale = (double)gesture.GetType().GetField("Scale").GetValue(gesture);
            InvokeEditor(host.Editor, "UpdateResize", handleName == "WidthHandle" ? new Point(delta * scale, 0) : new Point(0, delta * scale));
            handle.RaiseEvent(new DragCompletedEventArgs(0, 0, false) { RoutedEvent = Thumb.DragCompletedEvent });
            Settle();
            Assert.IsTrue(host.Editor.TryCommitEdits());
        }

        private static Rect Round(Rect rect) =>
            new Rect(Math.Round(rect.X, 6), Math.Round(rect.Y, 6), Math.Round(rect.Width, 6), Math.Round(rect.Height, 6));

        private static OsdDesignTile Bar(bool vertical, string label, bool showValue,
            double x = 16, double y = 8, double? frameWidth = null, double? frameHeight = null) => new OsdDesignTile
        {
            Kind = "bar", Key = "load", Label = label, Unit = "%", Minimum = 0, Maximum = 100,
            Orientation = vertical ? "vertical" : null, ShowValue = showValue ? null : false,
            X = x, Y = y, FrameWidth = frameWidth ?? (vertical ? 64 : 240), FrameHeight = frameHeight ?? (vertical ? 200 : 56)
        };

        // The renderer widget inside the tile panel at the given index of the root.
        private static JsonNode Widget(OsdDesignDocument document, int index) =>
            JsonNode.Parse(document.ToJson())["root"]["children"][index]["children"][0];

        private static void AssertRoundTrip(OsdDesignDocument document)
        {
            string json = document.ToJson();
            Assert.AreEqual(json, OsdDesignDocument.FromJson(json).ToJson());
        }

        // The bounding box of the accent's pixels: the bar's fill at full load.
        private static Rect GreenBounds(byte[] pixels, int width, int height)
        {
            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    int offset = (y * width + x) * 4;
                    if (pixels[offset + 1] <= 210 || pixels[offset + 2] >= 30 || pixels[offset] <= 80 || pixels[offset] >= 160) continue;
                    minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
                }
            return maxX < 0 ? Rect.Empty : new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private static object InvokeEditor(OsdEditorControl editor, string method, params object[] arguments) =>
            typeof(OsdEditorControl).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(editor, arguments);

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
    }
}

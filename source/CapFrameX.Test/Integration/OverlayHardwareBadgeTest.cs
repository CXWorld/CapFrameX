using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CapFrameX.OSD.Controls;
using CapFrameX.OSD.Interop;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;

namespace CapFrameX.Test.Integration
{
    [STATestClass]
    [DoNotParallelize]
    public class OverlayHardwareBadgeTest
    {
        [TestMethod]
        [DataRow("palette")]
        [DataRow("AMD-Ryzen-Radeon-REDLINE")]
        [DataRow("AMD-Ryzen-5-9600X-Badge")]
        [DataRow("AMD-Ryzen-7-9800X3D-Badge")]
        public void RyzenBadgeRetainsItsDarkHousingAboveTheWordmark(string presetId)
        {
            string image;
            if (presetId == "palette")
            {
                using var host = new EditorHost();
                image = host.AddBadge().Children[0].DataUri;
            }
            else
            {
                var starter = OverlayDesignStarterCatalog.Profiles.Single(profile => profile.PresetId == presetId);
                var document = OsdDesignDocument.FromJson(starter.DesignJson);
                image = document.EnumerateTiles().First(tile => tile.Kind == "image").DataUri;
            }
            using var input = new MemoryStream(Convert.FromBase64String(image.Substring(image.IndexOf(',') + 1)));
            var decoder = BitmapDecoder.Create(input, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var bitmap = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);
            int stride = bitmap.PixelWidth * 4;
            var pixels = new byte[stride * bitmap.PixelHeight];
            bitmap.CopyPixels(pixels, stride, 0);
            // The old highlight filled the strip at y=20 above RYZEN with near-white pixels.
            // Sample the housing away from the orange track and the wordmark itself.
            int y = bitmap.PixelHeight * 20 / 320;
            for (int x = bitmap.PixelWidth * 100 / 320; x < bitmap.PixelWidth * 260 / 320; x++)
            {
                int offset = y * stride + x * 4;
                Assert.IsTrue(pixels[offset + 3] > 250, "The badge housing must remain opaque.");
                Assert.IsTrue(pixels[offset] < 120 && pixels[offset + 1] < 120 && pixels[offset + 2] < 120,
                    "The bright horizontal strip above RYZEN must not return.");
            }
        }

        [TestMethod]
        public void PaletteBadgeAddsACompleteContainerWithoutChangingExistingTiles()
        {
            using var host = new EditorHost();
            var palette = (ItemsControl)host.Editor.FindName("WidgetPalette");
            Assert.IsNotNull(palette);
            Assert.IsTrue(palette.Items.Cast<object>().Contains("ryzenBadge"),
                "The composed hardware badge must be available through the editor's add palette.");
            var existing = host.Editor.Document.Tiles.ToArray();
            var previousPanels = (JArray)JObject.Parse(host.Editor.GetTemplateJson())["root"]["children"];

            var badge = host.AddBadge();

            Assert.AreEqual(existing.Length + 1, host.Editor.Document.Tiles.Count);
            Assert.AreSame(badge, host.Editor.SelectedElement);
            Assert.AreEqual("container", badge.Kind, "A badge must remain editable with the ordinary container tools.");
            Assert.AreEqual(320.0, badge.FrameWidth.Value);
            Assert.AreEqual(320.0, badge.FrameHeight.Value);
            Assert.AreEqual(3, badge.Children.Count, "The frame/branding, vertical model and tier must remain separate image layers.");
            Assert.IsTrue(badge.Children.All(child => child.Kind == "image"));
            var panels = (JArray)JObject.Parse(host.Editor.GetTemplateJson())["root"]["children"];
            for (int index = 0; index < existing.Length; index++)
            {
                Assert.AreSame(existing[index], host.Editor.Document.Tiles[index]);
                Assert.IsTrue(JToken.DeepEquals(previousPanels[index], panels[index]),
                    "Adding a hardware badge must preserve every pre-existing tile's content and geometry.");
            }
            AssertChildrenFit(host.Editor.Document, badge);
        }

        [TestMethod]
        public void BadgeImagesRemainPortableThroughProfileRoundtripAndNativeLoading()
        {
            using var host = new EditorHost();
            var badge = host.AddBadge();
            string[] images = badge.Children.Select(child => child.DataUri).ToArray();
            Assert.AreEqual(3, images.Distinct(StringComparer.Ordinal).Count(),
                "The three badge components must contain distinct images rather than repeated placeholders.");
            foreach (string image in images) AssertVisiblePng(image);
            string json = host.Editor.GetTemplateJson();

            var reopened = OsdDesignDocument.FromJson(json);

            Assert.AreEqual(json, reopened.ToJson());
            var restoredBadge = reopened.Tiles.Last();
            Assert.AreEqual("container", restoredBadge.Kind);
            CollectionAssert.AreEqual(images, restoredBadge.Children.Select(child => child.DataUri).ToArray(),
                "Every layer must be embedded in the profile without needing the source SVG files.");
            AssertChildrenFit(reopened, restoredBadge);
            using var preview = new OsdPreview();
            Assert.IsTrue(preview.CreateBitmap(1280, 960));
            Assert.IsTrue(preview.LoadTemplateJson(json), "The existing native image/container renderer must accept the badge profile.");
        }

        [TestMethod]
        public void OneUndoAndRedoRestoreTheCompleteBadgeAsASingleEdit()
        {
            using var host = new EditorHost();
            string before = host.Editor.GetTemplateJson();
            int previousNodeCount = host.Editor.Document.EnumerateTiles().Count();
            host.AddBadge();
            string withBadge = host.Editor.GetTemplateJson();
            Assert.AreEqual(previousNodeCount + 4, host.Editor.Document.EnumerateTiles().Count());

            ((Button)host.Editor.FindName("UndoButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Settle();

            Assert.AreEqual(before, host.Editor.GetTemplateJson());
            Assert.AreEqual(previousNodeCount, host.Editor.Document.EnumerateTiles().Count(),
                "Undo must not leave image children detached from their container.");
            Assert.IsFalse(((Button)host.Editor.FindName("UndoButton")).IsEnabled,
                "Adding a composed badge must produce one undo entry.");
            ((Button)host.Editor.FindName("RedoButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Settle();
            Assert.AreEqual(withBadge, host.Editor.GetTemplateJson());
            Assert.AreEqual(previousNodeCount + 4, host.Editor.Document.EnumerateTiles().Count());
        }

        [TestMethod]
        [DataRow(60, true)]
        [DataRow(61, false)]
        public void AddingABadgeReservesItsContainerAndAllThreeImageLayers(int existingTileCount, bool canAdd)
        {
            using var host = new EditorHost(existingTileCount);
            string before = host.Editor.GetTemplateJson();

            host.Editor.AddElement("ryzenBadge");

            Assert.IsTrue(host.Editor.TryCommitEdits());
            Assert.AreEqual(existingTileCount + (canAdd ? 4 : 0), host.Editor.Document.EnumerateTiles().Count(),
                "The tile budget must account for the complete four-node composition before inserting any part of it.");
            if (canAdd)
            {
                Assert.AreEqual("container", host.Editor.SelectedElement.Kind);
                Assert.AreEqual(3, host.Editor.SelectedElement.Children.Count);
            }
            else
            {
                Assert.AreEqual(before, host.Editor.GetTemplateJson(), "A rejected badge must not leave a partial composition or change the existing design.");
                Assert.IsFalse(((Button)host.Editor.FindName("UndoButton")).IsEnabled);
            }
        }

        [TestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        public void BadgesFitSmallEditingScopesAndEachAdditionHasIndependentLayers(bool nested, bool shortCanvas)
        {
            using var host = new EditorHost();
            var document = new OsdDesignDocument
            {
                Name = "Small badge canvas", Width = nested || shortCanvas ? 640 : 240,
                CanvasHeight = nested ? 480 : shortCanvas ? 160 : 300, IsCanvas = true, Padding = 0, GridSize = 4
            };
            document.Tiles.Add(new OsdDesignTile
            {
                Kind = "text", Label = "Existing heading", Text = "Keep", ValueSize = 12, Padding = 0,
                X = nested ? 300 : 0, Y = nested ? 200 : 0, FrameWidth = 120, FrameHeight = 24
            });
            if (nested)
            {
                document.Tiles.Add(new OsdDesignTile
                {
                    Kind = "container", Label = "Small parent", Padding = 12,
                    X = 20, Y = 20, FrameWidth = 240, FrameHeight = 260
                });
            }
            host.Editor.SetTemplateJson(document.ToJson());
            host.Editor.ResetHistory();
            if (nested)
                Assert.IsTrue(host.Editor.EnterContainer(host.Editor.Document.Tiles.Single(tile => tile.Kind == "container")));

            var first = host.AddBadge();
            var second = host.AddBadge();

            Assert.IsTrue(host.Editor.TryCommitEdits());
            host.Editor.Document.Validate();
            Assert.AreEqual(nested || shortCanvas ? 640.0 : 240.0, host.Editor.Document.Width);
            if (nested || shortCanvas)
                Assert.AreEqual(nested ? 480.0 : 160.0, host.Editor.Document.CanvasHeight);
            else
                Assert.IsTrue(host.Editor.Document.CanvasHeight >= 300.0,
                    "The root canvas may grow vertically to place both badges without overlap.");
            if (nested)
            {
                Assert.AreEqual(240.0, host.Editor.CurrentContainer.FrameWidth.Value);
                Assert.AreEqual(260.0, host.Editor.CurrentContainer.FrameHeight.Value);
            }
            Size available = host.Editor.Document.CanvasSize(host.Editor.CurrentContainer);
            foreach (var badge in new[] { first, second })
            {
                Assert.AreEqual("container", badge.Kind);
                Assert.AreEqual(3, badge.Children.Count);
                Assert.IsTrue(badge.Children.All(child => child.Kind == "image"));
                Rect frame = host.Editor.Document.TileFrame(badge);
                Assert.IsTrue(frame.Left >= 0 && frame.Top >= 0 &&
                    frame.Right <= available.Width + .001 && frame.Bottom <= available.Height + .001,
                    "Adding the badge must fit its selected editing scope before committing the design.");
                AssertChildrenFit(host.Editor.Document, badge);
            }
            Assert.AreNotSame(first, second);
            for (int index = 0; index < first.Children.Count; index++)
                Assert.AreNotSame(first.Children[index], second.Children[index], "Added badges must own independent editable image layers.");
            string secondLabel = second.Children[0].Label;
            first.Children[0].Label = "Changed only in the first badge";
            Assert.AreEqual(secondLabel, second.Children[0].Label);
            Assert.IsTrue(host.Editor.TryCommitEdits());
            Assert.AreEqual(host.Editor.GetTemplateJson(), OsdDesignDocument.FromJson(host.Editor.GetTemplateJson()).ToJson());
        }

        [TestMethod]
        public void MovingTheBadgeMovesAllLayersTogetherWithoutChangingTheirLocalGeometry()
        {
            using var host = new EditorHost();
            var badge = host.AddBadge();
            var original = badge.Children.Select(child => new
            {
                Tile = child,
                Local = host.Editor.Document.TileFrame(child),
                Absolute = host.Editor.Document.AbsoluteTileFrame(child),
                child.DataUri
            }).ToArray();

            badge.X += 48;
            badge.Y += 32;
            Assert.IsTrue(host.Editor.TryCommitEdits());

            foreach (var layer in original)
            {
                Assert.AreEqual(layer.Local, host.Editor.Document.TileFrame(layer.Tile));
                Rect expected = layer.Absolute;
                expected.Offset(48, 32);
                Assert.AreEqual(expected, host.Editor.Document.AbsoluteTileFrame(layer.Tile));
                Assert.AreEqual(layer.DataUri, layer.Tile.DataUri);
            }
            AssertChildrenFit(host.Editor.Document, badge);
        }

        [TestMethod]
        public void ResizingTheContainerScalesAllBadgeLayersWithoutReplacingTheirImages()
        {
            using var host = new EditorHost();
            var badge = host.AddBadge();
            var document = host.Editor.Document;
            var original = badge.Children.Select(child => new
            {
                Tile = child, Frame = document.TileFrame(child), child.DataUri
            }).ToArray();
            Size originalContent = document.CanvasSize(badge);
            double padding = document.EffectivePadding(badge);

            document.ResizeContainer(badge, originalContent.Width * 1.5 + 2 * padding,
                originalContent.Height * 1.5 + 2 * padding);
            Assert.IsTrue(host.Editor.TryCommitEdits());

            foreach (var layer in original)
            {
                Rect resized = document.TileFrame(layer.Tile);
                Assert.AreEqual(layer.Frame.X * 1.5, resized.X, .001);
                Assert.AreEqual(layer.Frame.Y * 1.5, resized.Y, .001);
                Assert.AreEqual(layer.Frame.Width * 1.5, resized.Width, .001);
                Assert.AreEqual(layer.Frame.Height * 1.5, resized.Height, .001);
                Assert.AreEqual(layer.DataUri, layer.Tile.DataUri, "Resizing must retain the original embedded image resolution.");
            }
            AssertChildrenFit(document, badge);
            Assert.AreEqual(document.ToJson(), OsdDesignDocument.FromJson(document.ToJson()).ToJson());
        }

        private static void AssertChildrenFit(OsdDesignDocument document, OsdDesignTile badge)
        {
            Size available = document.CanvasSize(badge);
            foreach (var child in badge.Children)
            {
                Rect frame = document.TileFrame(child);
                Assert.IsTrue(frame.Width > 0 && frame.Height > 0);
                Assert.IsTrue(frame.Left >= 0 && frame.Top >= 0 &&
                    frame.Right <= available.Width + .001 && frame.Bottom <= available.Height + .001,
                    "Each badge layer must remain inside its container after composition or resizing.");
            }
        }

        private static void AssertVisiblePng(string dataUri)
        {
            const string prefix = "data:image/png;base64,";
            StringAssert.StartsWith(dataUri, prefix);
            byte[] bytes = Convert.FromBase64String(dataUri.Substring(prefix.Length));
            using var stream = new MemoryStream(bytes);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var bitmap = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);
            Assert.IsTrue(bitmap.PixelWidth > 1 && bitmap.PixelHeight > 1);
            var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
            Assert.IsTrue(Enumerable.Range(0, bitmap.PixelWidth * bitmap.PixelHeight).Any(index => pixels[index * 4 + 3] > 0),
                "Every component must contain visible rasterized artwork.");
        }

        private static void Settle()
        {
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
        }

        private sealed class EditorHost : IDisposable
        {
            public OsdEditorControl Editor { get; } = new OsdEditorControl();
            private readonly Window _window;

            public EditorHost(int existingTileCount = 2)
            {
                var document = new OsdDesignDocument
                {
                    Name = "Existing overlay", Width = 960, CanvasHeight = 720, IsCanvas = true
                };
                document.Tiles.Add(new OsdDesignTile
                {
                    Kind = "metric", Label = "Existing FPS", Key = "fps", Unit = "FPS",
                    X = 600, Y = 500, FrameWidth = 280, FrameHeight = 100
                });
                document.Tiles.Add(new OsdDesignTile
                {
                    Kind = "text", Label = "Existing heading", Text = "Preserve this heading",
                    X = 600, Y = 620, FrameWidth = 280, FrameHeight = 60
                });
                for (int index = 2; index < existingTileCount; index++)
                {
                    document.Tiles.Add(new OsdDesignTile
                    {
                        Kind = "text", Label = "Existing tile " + index, Text = "T", ValueSize = 12, Padding = 0,
                        X = index % 10 * 80, Y = index / 10 * 40, FrameWidth = 64, FrameHeight = 32
                    });
                }
                Editor.SetTemplateJson(document.ToJson());
                _window = new Window
                {
                    Content = Editor, Width = 1280, Height = 960,
                    WindowStyle = WindowStyle.None, ShowActivated = false,
                    ShowInTaskbar = false, Left = -10000, Top = -10000
                };
                _window.Show();
                Settle();
                _window.UpdateLayout();
                Editor.ResetHistory();
            }

            public OsdDesignTile AddBadge()
            {
                var badge = Editor.AddElement("ryzenBadge");
                Assert.IsNotNull(badge);
                Assert.AreEqual("container", badge.Kind, "Adding the hardware palette entry must create its composed badge.");
                Assert.IsTrue(Editor.TryCommitEdits());
                Settle();
                _window.UpdateLayout();
                return badge;
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

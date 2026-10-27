using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CapFrameX.OSD.Controls;
using CapFrameX.OSD.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    // The palette's CapFrameX banner and logo (OsdLogoPresets): image tiles in the brand blue that keep
    // the logo's ratio on grid, canvas and container designs.
    [STATestClass]
    [DoNotParallelize]
    public class OverlayLogoTileTest
    {
        [TestMethod]
        public void ThePaletteOffersTheBannerAndTheLogo()
        {
            using var host = new EditorHost(Grid());
            var palette = (ItemsControl)host.Editor.FindName("WidgetPalette");
            CollectionAssert.IsSubsetOf(new[] { OsdLogoPresets.BannerKind, OsdLogoPresets.LogoKind }, palette.Items.Cast<object>().ToArray());
        }

        [TestMethod]
        public void TheEmbeddedImagesHaveTheRatioTheTilesAreSizedFor()
        {
            var banner = Decode(OsdLogoPresets.CreateGridTile(OsdLogoPresets.BannerKind, Grid()).DataUri);
            var logo = Decode(OsdLogoPresets.CreateGridTile(OsdLogoPresets.LogoKind, Grid()).DataUri);

            Assert.AreEqual(OsdLogoPresets.BannerAspectRatio, (double)banner.PixelWidth / banner.PixelHeight);
            Assert.AreEqual(logo.PixelWidth, logo.PixelHeight);
            foreach (var image in new[] { banner, logo })
            {
                var pixels = Pixels(image);
                Assert.AreEqual(0, pixels[3], "The tile's blue shows around the logo, so the image itself stays transparent.");
                Assert.IsTrue(Enumerable.Range(0, pixels.Length / 4).Any(i => pixels[i * 4 + 3] == 255 && IsLime(pixels, i * 4)),
                    "The mark's X keeps its lime.");
                Assert.IsTrue(Enumerable.Range(0, pixels.Length / 4).Any(i => pixels[i * 4 + 3] == 255 && IsWhite(pixels, i * 4)),
                    "The mark's C stays white.");
            }
        }

        [TestMethod]
        [DataRow(372, 4, false)]
        [DataRow(372, 4, true)]
        [DataRow(520, 2, true)]
        [DataRow(300, 1, false)]
        public void AGridBannerSpansTheRenderedOverlayAtItsRatioAndTheLogoIsSquare(double width, int columns, bool wideContent)
        {
            using var host = new EditorHost(Grid(width, columns, wideContent));

            var banner = host.Add(OsdLogoPresets.BannerKind);
            var logo = host.Add(OsdLogoPresets.LogoKind);

            Assert.AreEqual(columns, banner.ColumnSpan);
            Assert.AreEqual(1, logo.ColumnSpan);
            foreach (var tile in new[] { banner, logo }) AssertBrandTile(tile);
            var document = OsdDesignDocument.FromJson(host.Editor.GetTemplateJson());
            var render = new Render(document);
            // A 28 px metric needs about 125 px: at 4 x 87 the renderer widens every column for it.
            if (wideContent && columns == 4)
                Assert.IsTrue(render.Layout.Width > width + 100, "The metric must widen the grid beyond the document's width.");
            Rect bannerBounds = render.Bounds(document.Tiles.Count - 2), logoBounds = render.Bounds(document.Tiles.Count - 1);
            Assert.AreEqual(render.Layout.Width, bannerBounds.Width, .5, "The banner must take the overlay's whole rendered width.");
            Assert.AreEqual(OsdLogoPresets.BannerAspectRatio, bannerBounds.Width / bannerBounds.Height, .02);
            // Square up to the grid's widget height limit of 240; a wider column adds blue beside the mark.
            double column = (render.Layout.Width - (columns - 1) * document.Gap) / columns;
            Assert.AreEqual(column / Math.Min(column, 240), logoBounds.Width / logoBounds.Height, .02, "The logo tile must be square.");
            render.AssertBrandTile(bannerBounds, ContentAspect(banner));
            render.AssertBrandTile(logoBounds, ContentAspect(logo));
        }

        [TestMethod]
        public void AWiderOverlayKeepsTheBannerUndistortedBetweenMoreBlue()
        {
            using var host = new EditorHost(Grid(372, 4));
            var banner = host.Add(OsdLogoPresets.BannerKind);
            var document = OsdDesignDocument.FromJson(host.Editor.GetTemplateJson());

            document.Width = 560;

            var render = new Render(document);
            Rect bounds = render.Bounds(document.Tiles.Count - 1);
            Assert.AreEqual(560, bounds.Width, .5);
            Assert.AreEqual(banner.Height, bounds.Height, .5, "The banner keeps its height.");
            render.AssertBrandTile(bounds, ContentAspect(banner));
        }

        [TestMethod]
        public void ACanvasBannerTakesTheWholeWidthAndTheLogoASquare()
        {
            using var host = new EditorHost(Canvas());

            var banner = host.Add(OsdLogoPresets.BannerKind);
            var logo = host.Add(OsdLogoPresets.LogoKind);

            Assert.AreEqual(0.0, banner.X.Value);
            Assert.AreEqual(640.0, banner.FrameWidth.Value);
            Assert.AreEqual(640.0 / OsdLogoPresets.BannerAspectRatio, banner.FrameHeight.Value);
            Assert.AreEqual(96.0, logo.FrameWidth.Value);
            Assert.AreEqual(96.0, logo.FrameHeight.Value);
            Rect bannerFrame = host.Editor.Document.TileFrame(banner), logoFrame = host.Editor.Document.TileFrame(logo);
            Assert.IsFalse(bannerFrame.IntersectsWith(logoFrame), "The logo must be placed beside or below the banner.");
            foreach (var tile in new[] { banner, logo }) AssertBrandTile(tile);
            host.Editor.Document.Validate();
            var document = OsdDesignDocument.FromJson(host.Editor.GetTemplateJson());
            var render = new Render(document);
            render.AssertBrandTile(render.Bounds(document.Tiles.Count - 2), ContentAspect(banner));
            render.AssertBrandTile(render.Bounds(document.Tiles.Count - 1), ContentAspect(logo));
        }

        [TestMethod]
        public void InAContainerBothKeepTheirRatioWithinTheContainer()
        {
            var document = Canvas();
            document.Tiles.Add(new OsdDesignTile { Kind = "container", Label = "Footer", Padding = 0, X = 0, Y = 400, FrameWidth = 400, FrameHeight = 60 });
            using var host = new EditorHost(document);
            Assert.IsTrue(host.Editor.EnterContainer(host.Editor.Document.Tiles.Single(tile => tile.Kind == "container")));

            var banner = host.Add(OsdLogoPresets.BannerKind);
            var logo = host.Add(OsdLogoPresets.LogoKind);

            // The container is lower than 400 / 5, so the banner shrinks to its height.
            Assert.AreEqual(300.0, banner.FrameWidth.Value);
            Assert.AreEqual(60.0, banner.FrameHeight.Value);
            Assert.AreEqual(60.0, logo.FrameWidth.Value);
            Assert.AreEqual(60.0, logo.FrameHeight.Value);
            foreach (var tile in new[] { banner, logo }) AssertBrandTile(tile);
            host.Editor.Document.Validate();
        }

        [TestMethod]
        public void BothRoundTripThroughTheEditor()
        {
            foreach (var document in new[] { Grid(), Canvas() })
            {
                using var host = new EditorHost(document);
                host.Add(OsdLogoPresets.BannerKind);
                host.Add(OsdLogoPresets.LogoKind);
                string json = host.Editor.GetTemplateJson();
                Assert.AreEqual(json, OsdDesignDocument.FromJson(json).ToJson());
            }
        }

        private static void AssertBrandTile(OsdDesignTile tile)
        {
            Assert.AreEqual("image", tile.Kind);
            Assert.AreEqual("contain", tile.Fit, "Only contain keeps the logo's ratio in a frame of another ratio.");
            Assert.AreEqual(OsdLogoPresets.BrandBlue, tile.BackgroundColor);
            Assert.AreEqual(0.0, tile.Padding);
        }

        // wideContent: a 28 px FPS metric, wider than a column of 87; otherwise a short heading.
        private static OsdDesignDocument Grid(double width = 372, int columns = 4, bool wideContent = false)
        {
            var document = new OsdDesignDocument { Name = "Grid overlay", Width = width, Columns = columns, Padding = 8 };
            document.Tiles.Add(wideContent
                ? new OsdDesignTile { Kind = "metric", Label = "FPS", Key = "fps", Unit = "FPS" }
                : new OsdDesignTile { Kind = "text", Label = "Heading", Text = "CX", ValueSize = 12 });
            return document;
        }

        private static OsdDesignDocument Canvas()
        {
            var document = new OsdDesignDocument { Name = "Canvas overlay", Width = 640, CanvasHeight = 480, IsCanvas = true };
            document.Tiles.Add(new OsdDesignTile { Kind = "metric", Label = "FPS", Key = "fps", Unit = "FPS", X = 0, Y = 0, FrameWidth = 200, FrameHeight = 80 });
            return document;
        }

        // Width / height of the drawn logo inside the image, from its opaque pixels.
        private static double ContentAspect(OsdDesignTile tile)
        {
            var image = Decode(tile.DataUri);
            var pixels = Pixels(image);
            var box = InkBox(image.PixelWidth, image.PixelHeight, (x, y) => pixels[(y * image.PixelWidth + x) * 4 + 3] > 128);
            return box.Width / box.Height;
        }

        private static Rect InkBox(int width, int height, Func<int, int, bool> ink, int left = 0, int top = 0)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            for (int y = top; y < top + height; y++)
            for (int x = left; x < left + width; x++)
            {
                if (!ink(x, y)) continue;
                minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
            }
            Assert.IsTrue(maxX >= 0, "The logo must be drawn.");
            return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private static BitmapSource Decode(string dataUri)
        {
            const string prefix = "data:image/png;base64,";
            StringAssert.StartsWith(dataUri, prefix);
            using var stream = new MemoryStream(Convert.FromBase64String(dataUri.Substring(prefix.Length)));
            return BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        }

        private static byte[] Pixels(BitmapSource image)
        {
            var bitmap = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
            var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
            return pixels;
        }

        // BGRA; the brand colors are #0271F9, #97EF04 and #FDFDFD.
        private static bool IsBrandBlue(byte[] pixels, int offset)
            => Math.Abs(pixels[offset] - 0xF9) <= 3 && Math.Abs(pixels[offset + 1] - 0x71) <= 3 && Math.Abs(pixels[offset + 2] - 0x02) <= 3;

        private static bool IsLime(byte[] pixels, int offset)
            => pixels[offset] < 40 && pixels[offset + 1] > 220 && pixels[offset + 2] > 130 && pixels[offset + 2] < 170;

        private static bool IsWhite(byte[] pixels, int offset) => pixels[offset] > 240 && pixels[offset + 1] > 240 && pixels[offset + 2] > 240;

        // The design rendered by the native core at 100 %.
        private sealed class Render
        {
            private const int Width = 1200, Height = 1000, Stride = Width * 4;
            private readonly byte[] _pixels = new byte[Stride * Height];
            private readonly OsdPreviewTileRect[] _bounds = new OsdPreviewTileRect[OsdDesignDocument.MaximumTiles];
            public OsdPreviewLayout Layout { get; }

            public Render(OsdDesignDocument document)
            {
                using var preview = new OsdPreview();
                Assert.IsTrue(preview.CreateBitmap(Width, Height));
                Assert.IsTrue(preview.LoadTemplateJson(document.ToJson()));
                Assert.AreEqual(document.Tiles.Count, preview.GetLayout(out var layout, _bounds));
                Assert.AreEqual(1, layout.Scale, .001, "The scene must fit the bitmap unscaled.");
                Layout = layout;
                var handle = GCHandle.Alloc(_pixels, GCHandleType.Pinned);
                try { Assert.IsTrue(preview.RenderBitmap(handle.AddrOfPinnedObject(), _pixels.Length, Stride, .05)); }
                finally { handle.Free(); }
            }

            public Rect Bounds(int index)
            {
                var tile = _bounds[index];
                return new Rect(tile.X, tile.Y, tile.Width, tile.Height);
            }

            // Brand blue at the middle of every edge, the white and lime logo inside, drawn at the ratio of
            // the image's logo.
            public void AssertBrandTile(Rect bounds, double contentAspect)
            {
                int left = (int)Math.Ceiling(bounds.Left) + 2, right = (int)Math.Floor(bounds.Right) - 3;
                int top = (int)Math.Ceiling(bounds.Top) + 2, bottom = (int)Math.Floor(bounds.Bottom) - 3;
                int midX = (left + right) / 2, midY = (top + bottom) / 2;
                foreach (var (x, y) in new[] { (left, midY), (right, midY), (midX, top), (midX, bottom) })
                    Assert.IsTrue(IsBrandBlue(_pixels, y * Stride + x * 4), $"The tile must be brand blue at its edge ({x}, {y}).");
                int lime = 0, white = 0;
                var ink = InkBox(right - left + 1, bottom - top + 1, (x, y) =>
                {
                    int offset = y * Stride + x * 4;
                    if (IsLime(_pixels, offset)) { lime++; return true; }
                    if (IsWhite(_pixels, offset)) { white++; return true; }
                    return false;
                }, left, top);
                Assert.IsTrue(lime > 20 && white > lime, "The white mark with its lime X must be drawn on the blue.");
                Assert.AreEqual(contentAspect, ink.Width / ink.Height, contentAspect * .05, "The logo must not be distorted.");
            }
        }

        private static void Settle() => Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));

        private sealed class EditorHost : IDisposable
        {
            public OsdEditorControl Editor { get; } = new OsdEditorControl();
            private readonly Window _window;

            public EditorHost(OsdDesignDocument document)
            {
                Editor.SetTemplateJson(document.ToJson());
                _window = new Window
                {
                    Content = Editor, Width = 1280, Height = 960, WindowStyle = WindowStyle.None,
                    ShowActivated = false, ShowInTaskbar = false, Left = -10000, Top = -10000
                };
                _window.Show();
                Settle();
                _window.UpdateLayout();
                Editor.ResetHistory();
            }

            public OsdDesignTile Add(string kind)
            {
                var tile = Editor.AddElement(kind);
                Assert.IsNotNull(tile, $"The palette must add '{kind}'.");
                Assert.IsTrue(Editor.TryCommitEdits());
                Settle();
                _window.UpdateLayout();
                return tile;
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

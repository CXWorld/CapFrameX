using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CapFrameX.OSD.Controls;
using CapFrameX.OSD.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [STATestClass]
    [DoNotParallelize]
    public class OverlayIconNativeLayoutTest
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        [DataRow("\uE968", 28.0, 40.0, 40.0, 0.0, 1.0, false)]
        [DataRow("\uE7F4", 28.0, 40.0, 40.0, 0.0, 1.0, false)]
        [DataRow("\uE968", 48.0, 96.0, 72.0, 8.0, 0.5, false)]
        [DataRow("\uE7F4", 48.0, 96.0, 72.0, 8.0, 0.5, false)]
        [DataRow("\uE968", 20.0, 120.0, 84.0, 10.0, 2.0, false)]
        [DataRow("\uE7F4", 20.0, 120.0, 84.0, 10.0, 2.0, false)]
        [DataRow("\uE968", 72.0, 180.0, 160.0, 8.0, 2.0, false)]
        [DataRow("\uE7F4", 72.0, 180.0, 160.0, 8.0, 2.0, false)]
        [DataRow("\uE968", 36.0, 100.0, 80.0, 6.0, 1.0, true)]
        [DataRow("\uE7F4", 36.0, 100.0, 80.0, 6.0, 1.0, true)]
        public void NativeIconInkIsCenteredInsideItsTile(string glyph, double size, double frameWidth,
            double frameHeight, double padding, double scale, bool nested)
        {
            const int width = 1024, height = 768, stride = width * 4;
            var document = new OsdDesignDocument
            {
                Name = "Icon centering", Width = 320, CanvasHeight = 240, IsCanvas = true,
                Padding = 0, Radius = 0, GridSize = 1, Scale = scale,
                TileColor = "#121212FF", Accent = "#00FF80FF"
            };
            var icon = new OsdDesignTile
            {
                Kind = "icon", Label = "Centering fixture", Glyph = glyph, Family = "Segoe MDL2 Assets",
                ValueSize = size, X = 20, Y = 24, FrameWidth = frameWidth, FrameHeight = frameHeight,
                Padding = padding, BackgroundColor = "#304060FF", Color = "#00FF80FF"
            };
            if (nested)
            {
                var parent = new OsdDesignTile
                {
                    Kind = "container", Label = "Parent", X = 18, Y = 20,
                    FrameWidth = 220, FrameHeight = 180, Padding = 12, BackgroundColor = "#121212FF"
                };
                parent.Children.Add(icon);
                document.Tiles.Add(parent);
            }
            else document.Tiles.Add(icon);
            string json = document.ToJson();
            Assert.AreEqual(json, OsdDesignDocument.FromJson(json).ToJson(), "The authored icon must survive a profile roundtrip.");

            using var preview = new OsdPreview();
            Assert.IsTrue(preview.CreateBitmap(width, height), "Native bitmap renderer did not initialize.");
            Assert.IsTrue(preview.LoadTemplateJson(json), "Native renderer rejected the icon scene.");
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
            SaveBitmap(pixels, width, height, stride, glyph, size, padding, scale, nested);

            // Derive both centers from actual pixels, not from exported alignment or
            // native layout metadata: a correct selection frame can hide shifted ink.
            Rect tile = FindPixelBounds(pixels, width, height, (blue, green, red, alpha) =>
                alpha > 250 && Math.Abs(blue - 0x60) <= 1 && Math.Abs(green - 0x40) <= 1 && Math.Abs(red - 0x30) <= 1);
            Rect ink = FindPixelBounds(pixels, width, height, (blue, green, red, alpha) =>
                alpha > 200 && green > 160 && red < 40 && blue > 60 && blue < 170);
            Assert.IsFalse(tile.IsEmpty, "The distinguishable solid tile background was not rendered.");
            Assert.IsFalse(ink.IsEmpty, "The selected icon glyph did not produce visible pixels.");
            Assert.IsTrue(ink.Width >= 3 && ink.Height >= 3, "A clipped fragment must not count as a rendered icon.");
            Assert.IsTrue(tile.Contains(ink), "Icon pixels escaped their tile's frame.");
            double tileCenterX = tile.Left + tile.Width / 2;
            double tileCenterY = tile.Top + tile.Height / 2;
            double inkCenterX = ink.Left + ink.Width / 2;
            double inkCenterY = ink.Top + ink.Height / 2;
            string detail = $"U+{(int)glyph[0]:X4}; size={size}; scale={scale}; padding={padding}; nested={nested}; tile={tile}; ink={ink}";
            TestContext?.WriteLine(detail);
            Assert.AreEqual(tileCenterX, inkCenterX, 1.5, "Icon ink is not horizontally centered. " + detail);
            Assert.AreEqual(tileCenterY, inkCenterY, 1.5, "Icon ink is not vertically centered. " + detail);
        }

        private static Rect FindPixelBounds(byte[] pixels, int width, int height, Func<byte, byte, byte, byte, bool> matches)
        {
            int minX = width, minY = height, maxX = -1, maxY = -1;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int offset = (y * width + x) * 4;
                if (!matches(pixels[offset], pixels[offset + 1], pixels[offset + 2], pixels[offset + 3])) continue;
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
            }
            return maxX < minX ? Rect.Empty : new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private void SaveBitmap(byte[] pixels, int width, int height, int stride, string glyph,
            double size, double padding, double scale, bool nested)
        {
            if (TestContext?.ResultsDirectory == null) return;
            string name = FormattableString.Invariant($"icon-{(int)glyph[0]:X4}-{size}-{padding}-{scale}-nested-{nested}.png");
            string path = Path.Combine(TestContext.ResultsDirectory, name);
            var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var output = File.Create(path);
            encoder.Save(output);
            TestContext.AddResultFile(path);
        }
    }
}

using System;
using System.Linq;
using System.Runtime.InteropServices;
using CapFrameX.OSD.Controls;
using CapFrameX.OSD.Integration;
using CapFrameX.OSD.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [STATestClass]
    [DoNotParallelize]
    public class OverlayHardwareProfileLayoutTest
    {
        [TestMethod]
        public void RedlineHeaderCaptionsShareTheSameRenderedRightEdge()
        {
            var starter = OverlayDesignStarterCatalog.Profiles.Single(profile => profile.PresetId == "AMD-Ryzen-Radeon-REDLINE");
            // The preview centres the scene, so a 24 px border places it at a known origin. The
            // design's scale maps its design-space coordinates onto rendered pixels.
            var design = OsdDesignDocument.FromJson(starter.DesignJson);
            int width = (int)Math.Ceiling(design.Width * design.Scale) + 48;
            int height = (int)Math.Ceiling(design.CanvasHeight * design.Scale) + 48, stride = width * 4;
            int At(double designPixel) => (int)Math.Round(24 + designPixel * design.Scale);
            using var preview = new OsdPreview();
            Assert.IsTrue(preview.CreateBitmap(width, height));
            Assert.IsTrue(preview.LoadTemplateJson(starter.DesignJson));
            var pixels = new byte[stride * height];
            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                Assert.IsTrue(preview.RenderBitmap(handle.AddrOfPinnedObject(), pixels.Length, stride, .05));
            }
            finally { handle.Free(); }

            int RightEdge(int top, int bottom, Func<byte, byte, byte, bool> matches)
            {
                int right = -1;
                for (int y = top; y < bottom; y++)
                for (int x = At(356); x < At(520); x++)
                {
                    int offset = y * stride + x * 4;
                    if (pixels[offset + 3] > 200 && matches(pixels[offset + 2], pixels[offset + 1], pixels[offset]))
                        right = Math.Max(right, x);
                }
                return right;
            }
            int amdRight = RightEdge(At(12), At(44), (red, green, blue) => red > 150 && green < 70 && blue < 80);
            int performanceRight = RightEdge(At(47), At(66), (red, green, blue) => red > 100 && green > 100 && blue > 100);
            Assert.IsTrue(amdRight > At(476) && performanceRight > At(476), "Both captions must remain visible near the header's right inset.");
            Assert.AreEqual((double)amdRight, performanceRight, 1.5,
                "AMD and PERFORMANCE must be aligned by their actual rendered text, not merely by their tile frames.");
        }

        [TestMethod]
        public void GreenlineGpuNameIsVerticallyCentredInItsHeader()
        {
            var header = RenderStarterHeader("NVIDIA-GeForce-RTX-GREENLINE", out byte[] pixels, out int stride);
            // A dark name on the green header.
            var (top, bottom) = InkRows(pixels, stride, header, header.Top, (red, green, blue) => green < 100);
            Assert.AreEqual((double)(top - header.Top), header.Bottom - 1 - bottom, 2,
                "The GPU name must keep the same distance to the top and bottom of the green header.");
        }

        [TestMethod]
        public void IcelineGpuNameIsVerticallyCentredBelowItsAccentLine()
        {
            var header = RenderStarterHeader("ICELINE-Vertical", out byte[] pixels, out int stride);
            // The accent line along the top edge has the colour of the name, which is centred in the
            // band below it. Without its one pixel of padding the two gaps differ by two pixels,
            // hence the tight tolerance.
            static bool Cyan(byte red, byte green, byte blue) => green > 120 && blue > 130;
            int lineBottom = header.Top, middle = (header.Left + header.Right) / 2;
            while (lineBottom < header.Bottom && IsInk(pixels, stride, middle, lineBottom, Cyan)) lineBottom++;
            Assert.IsTrue(lineBottom > header.Top, "The accent line must be rendered along the top of the header.");
            var (top, bottom) = InkRows(pixels, stride, header, lineBottom, Cyan);
            Assert.AreEqual((double)(top - lineBottom), header.Bottom - 1 - bottom, 1,
                "The GPU name must keep the same distance to the accent line and to the bottom of the header.");
        }

        // Renders a starter with a GPU name and returns the pixel bounds of its first tile, the header.
        private static (int Left, int Top, int Right, int Bottom) RenderStarterHeader(string presetId, out byte[] pixels, out int stride)
        {
            var starter = OverlayDesignStarterCatalog.Profiles.Single(profile => profile.PresetId == presetId);
            var design = OsdDesignDocument.FromJson(starter.DesignJson);
            int width = (int)Math.Ceiling(design.Width * design.Scale) + 48;
            int height = (int)Math.Ceiling(design.CanvasHeight * design.Scale) + 48;
            stride = width * 4;
            using var preview = new OsdPreview();
            Assert.IsTrue(preview.CreateBitmap(width, height));
            Assert.IsTrue(preview.LoadTemplateJson(starter.DesignJson));
            Assert.IsTrue(preview.SetMetricsJson("{\"gpuName\":\"NVIDIA GeForce RTX 5090\"}"));
            pixels = new byte[stride * height];
            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                Assert.IsTrue(preview.RenderBitmap(handle.AddrOfPinnedObject(), pixels.Length, stride, .05));
            }
            finally { handle.Free(); }
            var tiles = new OsdPreviewTileRect[OsdDesignDocument.MaximumTiles];
            Assert.IsTrue(preview.GetLayout(out _, tiles) > 0);
            var header = tiles[0];
            return ((int)Math.Round(header.X), (int)Math.Round(header.Y),
                (int)Math.Round(header.X + header.Width), (int)Math.Round(header.Y + header.Height));
        }

        // The native metric stacks its text from the top of the padded tile, so only the tile
        // padding centres a name. Capitals and digits span cap height to baseline, and the sample
        // name has no descenders, so the rows it covers are exactly that span.
        private static (int Top, int Bottom) InkRows(byte[] pixels, int stride, (int Left, int Top, int Right, int Bottom) area,
            int from, Func<byte, byte, byte, bool> matches)
        {
            int top = -1, bottom = -1;
            for (int y = from; y < area.Bottom; y++)
            for (int x = area.Left + 2; x < area.Right - 2; x++)
            {
                if (!IsInk(pixels, stride, x, y, matches)) continue;
                if (top < 0) top = y;
                bottom = y;
            }
            Assert.IsTrue(top >= 0, "The GPU name must be rendered inside the header.");
            return (top, bottom);
        }

        private static bool IsInk(byte[] pixels, int stride, int x, int y, Func<byte, byte, byte, bool> matches)
        {
            int offset = y * stride + x * 4;
            return pixels[offset + 3] > 200 && matches(pixels[offset + 2], pixels[offset + 1], pixels[offset]);
        }
    }
}

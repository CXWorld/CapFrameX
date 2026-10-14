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
            var starter = OverlayDesignStarterCatalog.Profiles.Single(profile => profile.PresetId == "NVIDIA-GeForce-RTX-GREENLINE");
            var design = OsdDesignDocument.FromJson(starter.DesignJson);
            int width = (int)Math.Ceiling(design.Width * design.Scale) + 48;
            int height = (int)Math.Ceiling(design.CanvasHeight * design.Scale) + 48, stride = width * 4;
            using var preview = new OsdPreview();
            Assert.IsTrue(preview.CreateBitmap(width, height));
            Assert.IsTrue(preview.LoadTemplateJson(starter.DesignJson));
            Assert.IsTrue(preview.SetMetricsJson("{\"gpuName\":\"NVIDIA GeForce RTX 5090\"}"));
            var pixels = new byte[stride * height];
            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                Assert.IsTrue(preview.RenderBitmap(handle.AddrOfPinnedObject(), pixels.Length, stride, .05));
            }
            finally { handle.Free(); }
            var tiles = new OsdPreviewTileRect[OsdDesignDocument.MaximumTiles];
            Assert.IsTrue(preview.GetLayout(out _, tiles) > 0);

            // The native metric stacks its text from the top of the padded tile, so only the tile
            // padding centres the name. Its capitals and digits span cap height to baseline; the
            // name has no descenders, so the dark rows on the green header are exactly that span.
            var header = tiles[0];
            int headerTop = (int)Math.Round(header.Y), headerBottom = (int)Math.Round(header.Y + header.Height);
            int top = -1, bottom = -1;
            for (int y = headerTop; y < headerBottom; y++)
            for (int x = (int)Math.Round(header.X) + 2; x < (int)Math.Round(header.X + header.Width) - 2; x++)
            {
                int offset = y * stride + x * 4;
                if (pixels[offset + 3] > 200 && pixels[offset + 1] < 100)
                {
                    if (top < 0) top = y;
                    bottom = y;
                }
            }
            Assert.IsTrue(top >= 0, "The GPU name must be rendered inside the header.");
            Assert.AreEqual((double)(top - headerTop), headerBottom - 1 - bottom, 2,
                "The GPU name must keep the same distance to the top and bottom of the green header.");
        }
    }
}

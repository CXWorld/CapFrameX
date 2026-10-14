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
    }
}

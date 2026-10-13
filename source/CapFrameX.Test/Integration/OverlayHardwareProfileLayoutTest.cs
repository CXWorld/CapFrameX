using System;
using System.Linq;
using System.Runtime.InteropServices;
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
            const int width = 568, height = 788, stride = width * 4;
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
                for (int x = 380; x < 544; x++)
                {
                    int offset = y * stride + x * 4;
                    if (pixels[offset + 3] > 200 && matches(pixels[offset + 2], pixels[offset + 1], pixels[offset]))
                        right = Math.Max(right, x);
                }
                return right;
            }
            int amdRight = RightEdge(36, 68, (red, green, blue) => red > 150 && green < 70 && blue < 80);
            int performanceRight = RightEdge(71, 90, (red, green, blue) => red > 100 && green > 100 && blue > 100);
            Assert.IsTrue(amdRight > 500 && performanceRight > 500, "Both captions must remain visible near the header's right inset.");
            Assert.AreEqual((double)amdRight, performanceRight, 1.5,
                "AMD and PERFORMANCE must be aligned by their actual rendered text, not merely by their tile frames.");
        }
    }
}

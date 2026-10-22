using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using CapFrameX.OSD.Controls;
using CapFrameX.OSD.Integration;
using CapFrameX.OSD.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

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

        [TestMethod]
        [DataRow("Slick-Overlay-Single-Line", "FPS - Current", "FPS - Label")]
        [DataRow("Slick-Overlay-Single-Line", "GPU - Value - Name", "FPS - Label")]
        [DataRow("Slick-Overlay-Single-Line", "CPU - Value - Name", "FPS - Label")]
        [DataRow("Slick-Overlay-Compact-2x2", "FPS - Current", "FPS - 0.1% Low - Value")]
        [DataRow("Slick-Overlay-Compact-2x2", "GPU - Value - Name", "GPU - Value - Usage")]
        [DataRow("Slick-Overlay-Compact-2x2", "CPU - Value - Name", "CPU - Value - Usage")]
        [DataRow("Slick-Overlay-Compact-2x2", "Footer - Driver Label", "Footer - Driver")]
        [DataRow("Slick-Overlay-Wide-3-Rows", "FPS - Current", "FPS - 0.1% Low - Value")]
        [DataRow("Slick-Overlay-Wide-3-Rows", "GPU - Value - Name", "GPU - Value - Usage")]
        [DataRow("Slick-Overlay-Wide-3-Rows", "CPU - Value - Name", "CPU - Value - Usage")]
        [DataRow("Slick-Overlay-Wide-3-Rows", "FT - Label", "GPU - Value - Usage")]
        [DataRow("Slick-Overlay-Wide-3-Rows", "Footer - Driver Label", "Footer - Driver")]
        [DataRow("Slick-Overlay-Vertical-Panel", "Footer - Driver Label", "Footer - Driver")]
        public void SlickReadoutSitsOnTheBaselineOfItsRow(string presetId, string label, string rowLabel)
        {
            // A canvas tile draws its text at the top, so a larger font in the same frame sits lower:
            // only the rendered baselines show whether a readout lines up with its row.
            var render = new SlickRender(presetId, "Adrenalin 26.10.1");
            Assert.AreEqual(render.Ink(rowLabel).Bottom, render.Ink(label).Bottom, .5,
                $"'{label}' must share the baseline of '{rowLabel}'.");
        }

        [TestMethod]
        [DataRow("Slick-Overlay-Single-Line")]
        [DataRow("Slick-Overlay-Compact-2x2")]
        [DataRow("Slick-Overlay-Wide-3-Rows")]
        [DataRow("Slick-Overlay-Vertical-Panel")]
        public void SlickDriverVersionIsNotCutOff(string presetId)
        {
            // The longest versions the GPU classes report: Adrenalin with a two-digit month and
            // revision, and the Windows driver version AMD falls back to.
            foreach (string driver in new[] { "Adrenalin 26.12.12", "32.0.21025.10016" })
            {
                var render = new SlickRender(presetId, driver);
                Assert.IsTrue(render.Ink("Footer - Driver").Right < render.Frame("Footer - Driver").Right - 1,
                    $"'{driver}' must end before the driver tile clips it.");
            }
        }

        // A Slick design rendered at the largest scale, where a misplaced readout is off by more than
        // a pixel, with readouts that have no descenders, so the lowest ink row of a readout is its baseline.
        private sealed class SlickRender
        {
            private readonly OsdDesignDocument _design;
            private readonly byte[] _pixels;
            private readonly int _stride;

            public SlickRender(string presetId, string driver)
            {
                var starter = OverlayDesignStarterCatalog.Profiles.Single(profile => profile.PresetId == presetId);
                _design = OsdDesignDocument.FromJson(starter.DesignJson);
                _design.Scale = 2;
                int width = (int)Math.Ceiling(_design.Width * _design.Scale) + 48;
                int height = (int)Math.Ceiling(_design.CanvasHeight * _design.Scale) + 48;
                _stride = width * 4;
                using var preview = new OsdPreview();
                Assert.IsTrue(preview.CreateBitmap(width, height));
                Assert.IsTrue(preview.LoadTemplateJson(_design.ToJson()));
                Assert.IsTrue(preview.SetMetricsJson(JsonConvert.SerializeObject(new Dictionary<string, object>
                {
                    ["fpsCurrent"] = 146.0, ["fps0dot1pctLow"] = 2.0, ["gpuLoad"] = 8.0, ["cpuLoad"] = 6.0,
                    ["gpuNameShort"] = "RX 9070 XT", ["cpuNameShort"] = "R7 9800X3D", ["gpuDriver"] = driver
                })));
                _pixels = new byte[_stride * height];
                var handle = GCHandle.Alloc(_pixels, GCHandleType.Pinned);
                try
                {
                    Assert.IsTrue(preview.RenderBitmap(handle.AddrOfPinnedObject(), _pixels.Length, _stride, .05));
                }
                finally { handle.Free(); }
            }

            // The tile's frame in pixels; the preview centres the scene in a 24 px border.
            public (int Left, int Top, int Right, int Bottom) Frame(string label)
            {
                var tile = _design.EnumerateTiles().Single(item => item.Label == label);
                double x = tile.X.Value, y = tile.Y.Value;
                for (var parent = _design.GetParent(tile); parent != null; parent = _design.GetParent(parent))
                {
                    x += parent.X.Value + _design.EffectivePadding(parent);
                    y += parent.Y.Value + _design.EffectivePadding(parent);
                }
                int At(double designPixel) => (int)Math.Round(24 + designPixel * _design.Scale);
                return (At(x), At(y), At(x + tile.FrameWidth.Value), At(y + tile.FrameHeight.Value));
            }

            // The bounds of the bright text inside the tile's frame.
            public (int Left, int Top, int Right, int Bottom) Ink(string label)
            {
                var frame = Frame(label);
                int left = int.MaxValue, top = -1, right = -1, bottom = -1;
                for (int y = frame.Top; y < frame.Bottom; y++)
                for (int x = frame.Left; x < frame.Right; x++)
                {
                    int offset = y * _stride + x * 4;
                    if (_pixels[offset + 3] <= 200 || _pixels[offset] + _pixels[offset + 1] + _pixels[offset + 2] <= 330) continue;
                    if (top < 0) top = y;
                    bottom = y;
                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                }
                Assert.IsTrue(top >= 0, $"'{label}' must be rendered inside its tile.");
                return (left, top, right, bottom);
            }
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

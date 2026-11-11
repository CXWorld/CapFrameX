using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
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
    public class OverlayGroupBarNativeLayoutTest
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        [DataRow(false, 32, 0.5, 0.0)]
        [DataRow(false, 32, 1.0, 0.0)]
        [DataRow(false, 128, 2.0, 0.0)]
        [DataRow(true, 32, 0.5, 0.0)]
        [DataRow(true, 32, 1.0, 0.0)]
        [DataRow(true, 128, 2.0, 0.0)]
        [DataRow(false, 32, 0.5, 480.0)]
        [DataRow(false, 128, 2.0, 200.0)]
        [DataRow(true, 32, 0.5, 480.0)]
        [DataRow(true, 128, 2.0, 200.0)]
        public void NativePixelsKeepRequestedBarDimensionsAndWrapEveryMember(bool vertical, int count, double scale, double resizedWidth)
        {
            VerifyNativePixels(vertical, count, scale, resizedWidth, false);
        }

        [TestMethod]
        [DataRow(false, 0.5)]
        [DataRow(false, 2.0)]
        [DataRow(true, 0.5)]
        [DataRow(true, 2.0)]
        public void NativePixelsKeepHybridCoreClassesOnSeparatePartialRows(bool vertical, double scale)
        {
            VerifyNativePixels(vertical, 16, scale, 0, true);
        }

        private void VerifyNativePixels(bool vertical, int count, double scale, double resizedWidth, bool hybrid)
        {
            const int width = 1024, height = 2048, stride = width * 4;
            var document = new OsdDesignDocument
            {
                Name = "Bar wrapping", Width = 304, CanvasHeight = 120,
                IsCanvas = true, Padding = 8, GridSize = 1, Scale = scale,
                Accent = "#00FF80FF", TileColor = "#181818FF"
            };
            var tile = new OsdDesignTile
            {
                Kind = "group", Key = "cpu-loads", GroupKey = "cpu-loads", GroupDisplay = "bar",
                Label = "", Unit = "%", GroupColumns = 2,
                GroupBarOrientation = vertical ? "vertical" : null,
                GroupShowLabels = false, GroupShowValues = false,
                GroupByCoreType = hybrid ? true : null,
                GroupBarWidth = hybrid ? (304 - 16 - (vertical ? 4 : 8) * 7) / 8.0 : vertical ? 40 : 72,
                GroupBarHeight = 12,
                X = 0, Y = 0, FrameWidth = 304, FrameHeight = 120,
                GroupMembers = Enumerable.Range(0, count).Select(index => new OsdGroupMember
                {
                    Key = "load" + index, Label = "T" + index, IsEnabled = true,
                    CoreType = hybrid ? index < 6 ? "P" : index < 14 ? "E" : "LPE" : null,
                    CoreIndex = hybrid ? index + 1 : null
                }).ToList()
            };
            document.Tiles.Add(tile);
            document.EnsureGroupBarFits(tile);
            if (resizedWidth > 0)
            {
                var previous = document.GetGroupBarLayout(tile);
                document.Width = Math.Max(document.Width, resizedWidth);
                document.ResizeGroupBars(tile, resizedWidth, tile.FrameHeight.Value);
                var resized = document.GetGroupBarLayout(tile);
                Assert.AreEqual(previous.Columns, resized.Columns);
                Assert.AreEqual(previous.Rows, resized.Rows);
                Assert.AreEqual(resizedWidth - 2 * document.EffectivePadding(tile),
                    resized.EffectiveWidth * resized.Columns + resized.Gap * (resized.Columns - 1), .001);
            }
            var expected = document.GetGroupBarLayout(tile);
            Assert.IsTrue(expected.Rows > 1);
            if (hybrid)
            {
                Assert.AreEqual(8, expected.Columns, "Every core class must use the same eight-column capacity.");
                CollectionAssert.AreEqual(new[] { 6, 8, 2 }, expected.RowCounts.ToArray());
            }
            Assert.AreEqual(document.ToJson(), OsdDesignDocument.FromJson(document.ToJson()).ToJson());

            using var preview = new OsdPreview();
            Assert.IsTrue(preview.CreateBitmap(width, height), "Native bitmap renderer did not initialize.");
            Assert.IsTrue(preview.LoadTemplateJson(document.ToJson()), "Native renderer rejected explicit group frames.");
            Assert.IsTrue(preview.SetMetricsJson(JsonSerializer.Serialize(tile.GroupMembers.ToDictionary(member => member.Key, _ => 100d))));
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
            Assert.AreEqual(1, preview.GetLayout(out var layout, tiles), "Group members became separate selectable tiles.");
            double pixelScale = scale * layout.Scale;
            var bars = FindGreenBars(pixels, width, height).OrderBy(rect => rect.Top).ThenBy(rect => rect.Left).ToArray();
            SaveBitmap(pixels, width, height, stride, vertical, count, scale, resizedWidth, hybrid);
            Assert.AreEqual(count, bars.Length, "A bar was clipped, merged, or lost on a wrapped row.");
            double expectedWidth = (expected.EffectiveWidth - (vertical ? 4 : 0)) * pixelScale;
            foreach (var bar in bars)
            {
                Assert.AreEqual(expectedWidth, bar.Width, 2.1, "The native panel stretched the requested bar width.");
                Assert.AreEqual(expected.TrackHeight * pixelScale, bar.Height, 2.1, "The native bar height disagrees with the editor.");
                Assert.IsTrue(bar.Left >= tiles[0].X - 1 && bar.Top >= tiles[0].Y - 1 &&
                    bar.Right <= tiles[0].X + tiles[0].Width + 1 &&
                    bar.Bottom <= tiles[0].Y + tiles[0].Height + 1, "A wrapped bar escaped its tile.");
            }
            var renderedRows = bars.GroupBy(rect => rect.Top).ToArray();
            Assert.AreEqual(expected.Rows, renderedRows.Length, "Rendered rows differ from the inspector.");
            CollectionAssert.AreEqual(expected.RowCounts.ToArray(), renderedRows.Select(row => row.Count()).ToArray(),
                "Native rows do not preserve each core class's member count.");
            Assert.AreEqual(expected.RowCounts[0], renderedRows[0].Count(), "The first row contains a different number of bars.");
            Assert.IsTrue(bars.Skip(expected.RowCounts[0]).First().Left <= bars[0].Left + 1, "The next row did not restart at the left edge.");
            foreach (var row in renderedRows)
                Assert.AreEqual(bars[0].Left, row.First().Left, 1, "A core class did not start at the left edge.");
            if (resizedWidth > 0)
            {
                var lastBar = bars[expected.Columns - 1];
                double expectedRight = tiles[0].X + tiles[0].Width -
                    (document.EffectivePadding(tile) + (vertical ? 2 : 0)) * pixelScale;
                Assert.AreEqual(expectedRight, lastBar.Right, 2.1, "Resized bars left unused space at the tile's right edge.");
            }
        }

        private void SaveBitmap(byte[] pixels, int width, int height, int stride, bool vertical, int count, double scale, double resizedWidth, bool hybrid)
        {
            if (TestContext?.ResultsDirectory == null) return;
            string suffix = hybrid ? "-hybrid-6P-8E-2LPE" : "";
            string path = Path.Combine(TestContext.ResultsDirectory, $"group-bars-{vertical}-{count}-{scale.ToString(System.Globalization.CultureInfo.InvariantCulture)}-{resizedWidth}{suffix}.png");
            var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var output = File.Create(path);
            encoder.Save(output);
            TestContext.AddResultFile(path);
        }

        private static IEnumerable<Rect> FindGreenBars(byte[] pixels, int width, int height)
        {
            var visited = new bool[width * height];
            var pending = new Queue<int>();
            bool IsGreen(int position)
            {
                int offset = position * 4;
                return pixels[offset + 1] > 210 && pixels[offset + 2] < 30 &&
                    pixels[offset] > 80 && pixels[offset] < 160;
            }
            for (int start = 0; start < visited.Length; start++)
            {
                if (visited[start] || !IsGreen(start)) continue;
                visited[start] = true;
                pending.Enqueue(start);
                int minX = width, minY = height, maxX = 0, maxY = 0;
                while (pending.Count != 0)
                {
                    int position = pending.Dequeue(), x = position % width, y = position / width;
                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                    void Visit(int neighbor)
                    {
                        if (visited[neighbor] || !IsGreen(neighbor)) return;
                        visited[neighbor] = true;
                        pending.Enqueue(neighbor);
                    }
                    if (x > 0) Visit(position - 1);
                    if (x + 1 < width) Visit(position + 1);
                    if (y > 0) Visit(position - width);
                    if (y + 1 < height) Visit(position + width);
                }
                yield return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CapFrameX.Contracts.Overlay;
using CapFrameX.OSD.Controls;
using CapFrameX.OSD.Interop;

namespace CapFrameX.View.OverlayDesignAuthoring
{
    /// <summary>
    /// Renders a scene with the windowless native preview, the renderer the editor shows, and
    /// encodes the scene's bounds as PNG. Create, render and dispose happen on the calling thread,
    /// which must be the UI thread.
    /// </summary>
    public static class OverlayDesignPreviewRenderer
    {
        private const int Margin = 8;
        // The native bitmap renderer accepts at most 4096 px per side and 4,194,304 pixels.
        private const int MaximumSide = 4096;
        private const long MaximumPixels = 4194304;

        public sealed class Result
        {
            public byte[] PngBytes { get; init; }
            public int Width { get; init; }
            public int Height { get; init; }
            public double Scale { get; init; }
            /// <summary>Top-level tile bounds in image pixels, in scene order.</summary>
            public IReadOnlyList<(double X, double Y, double Width, double Height)> Tiles { get; init; }
        }

        /// <param name="sceneJson">The native scene (a saved design or its runtime template).</param>
        /// <param name="metricsJson">Readings by key; null keeps the renderer's own demo values.</param>
        /// <param name="samples">Frame samples for chart tiles; ignored without readings.</param>
        /// <param name="expectedWidth">Scene width in pixels when known, else 0.</param>
        /// <param name="expectedHeight">Scene height in pixels when known, else 0.</param>
        public static Result Render(string sceneJson, string metricsJson, IReadOnlyList<OsdStreamSample> samples,
            double expectedWidth, double expectedHeight)
        {
            var (width, height) = BitmapSize(expectedWidth, expectedHeight);
            using var preview = new OsdPreview();
            if (!preview.CreateBitmap(width, height))
                throw new OverlayDesignAuthoringException("The native OSD renderer could not create a preview bitmap.");
            if (!preview.LoadTemplateJson(sceneJson))
                throw new OverlayDesignAuthoringException("The native OSD renderer rejected the design.");
            if (metricsJson != null)
            {
                preview.SetMetricsJson(metricsJson);
                if (samples != null && samples.Count > 0) preview.PushSamples(samples);
            }

            int stride = width * 4;
            var pixels = new byte[stride * height];
            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                // Values ease towards their targets; a few seconds of frames settle them.
                for (int frame = 0; frame < 12; frame++)
                {
                    if (!preview.RenderBitmap(handle.AddrOfPinnedObject(), pixels.Length, stride, 0.25))
                        throw new OverlayDesignAuthoringException("The native OSD renderer failed to draw the preview.");
                }
            }
            finally
            {
                handle.Free();
            }

            var bounds = new OsdPreviewTileRect[64];
            int count = preview.GetLayout(out var layout, bounds);
            if (count < 0 || layout.Width <= 0 || layout.Height <= 0)
                throw new OverlayDesignAuthoringException("The native OSD renderer reported no layout for the design.");
            int left = Math.Max(0, (int)Math.Floor(layout.X) - Margin);
            int top = Math.Max(0, (int)Math.Floor(layout.Y) - Margin);
            int right = Math.Min(width, (int)Math.Ceiling(layout.X + layout.Width) + Margin);
            int bottom = Math.Min(height, (int)Math.Ceiling(layout.Y + layout.Height) + Margin);
            int croppedWidth = Math.Max(1, right - left), croppedHeight = Math.Max(1, bottom - top);

            var cropped = new byte[croppedWidth * 4 * croppedHeight];
            for (int row = 0; row < croppedHeight; row++)
                Buffer.BlockCopy(pixels, (top + row) * stride + left * 4, cropped, row * croppedWidth * 4, croppedWidth * 4);
            var bitmap = BitmapSource.Create(croppedWidth, croppedHeight, 96, 96, PixelFormats.Pbgra32, null, cropped, croppedWidth * 4);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var output = new MemoryStream();
            encoder.Save(output);

            var tiles = new List<(double, double, double, double)>();
            for (int index = 0; index < Math.Min(count, bounds.Length); index++)
                tiles.Add((Math.Round(bounds[index].X - left, 1), Math.Round(bounds[index].Y - top, 1),
                    Math.Round(bounds[index].Width, 1), Math.Round(bounds[index].Height, 1)));
            return new Result
            {
                PngBytes = output.ToArray(), Width = croppedWidth, Height = croppedHeight,
                Scale = layout.Scale, Tiles = tiles
            };
        }

        /// <summary>
        /// Plausible values for every reading the design shows, so a demo preview fills each tile.
        /// The renderer's own demo data only knows a handful of portable roles.
        /// </summary>
        public static Dictionary<string, object> DemoMetrics(OsdDesignDocument document, IReadOnlyList<OsdTelemetrySource> sources)
        {
            var values = new Dictionary<string, object>(StringComparer.Ordinal);
            int index = 0;
            void Add(string key, string unit, double? minimum, double? maximum)
            {
                if (string.IsNullOrWhiteSpace(key) || values.ContainsKey(key)) return;
                var source = sources?.FirstOrDefault(candidate => candidate.Key == key);
                if (source?.IsText == true)
                {
                    values[key] = source.Name ?? "Sample";
                    return;
                }
                double variation = 1 + 0.12 * Math.Sin(++index * 1.7);
                double value = minimum.HasValue && maximum.HasValue && maximum > minimum
                    ? minimum.Value + (maximum.Value - minimum.Value) * (0.55 + 0.25 * Math.Sin(index * 1.3))
                    : (source?.SemanticKey ?? key) switch
                    {
                        "fps" => 144, "frametime" => 6.9, "fps1pctLow" => 118, "fps01pctLow" => 96,
                        _ => (unit ?? source?.Unit ?? "").Trim() switch
                        {
                            "FPS" => 144, "ms" => 6.9, "°C" => 64, "%" => 57, "W" => 245, "MHz" => 2850,
                            "MB" or "MiB" => 8192, "GB" or "GiB" => 12.4, "V" => 1.05, "RPM" => 1450, "A" => 12,
                            _ => 42
                        } * variation
                    };
                values[key] = Math.Round(value, 2);
            }
            foreach (var tile in document.EnumerateTiles())
            {
                bool ranged = tile.Kind == "bar" || tile.Kind == "gauge" || tile.Kind == "group" && tile.GroupDisplay is "bar" or "heatmap";
                if (tile.Kind == "metric" || tile.Kind == "bar" || tile.Kind == "gauge")
                {
                    Add(tile.Key, tile.Unit, ranged ? tile.Minimum : null, ranged ? tile.Maximum : null);
                    Add(tile.SecondaryKey, tile.SecondaryUnit, null, null);
                }
                else if (tile.Kind == "group" && tile.GroupMembers != null)
                {
                    foreach (var member in tile.GroupMembers.Where(member => member?.IsEnabled == true))
                        Add(member.Key, tile.Unit, ranged ? tile.Minimum : null, ranged ? tile.Maximum : null);
                }
            }
            return values;
        }

        /// <summary>
        /// Gives every heatmap cell its source's reading. A single image has no history to average,
        /// so the current value stands in for the window's mean.
        /// </summary>
        public static void AddHeatmapReadings(OsdDesignDocument document, IDictionary<string, object> values)
        {
            foreach (var tile in document.EnumerateTiles().Where(tile => tile.IsGroupHeatmap && tile.GroupMembers != null))
            {
                int seconds = tile.GroupWindowSeconds ?? OsdDesignDocument.DefaultHeatmapWindowSeconds;
                foreach (var member in tile.GroupMembers.Where(member => member?.IsEnabled == true))
                    if (values.TryGetValue(member.Key, out object value))
                        values[OsdDesignDocument.AveragedKey(seconds, member.Key)] = value;
            }
        }

        /// <summary>Five seconds of plausible frames at <paramref name="fps"/>, so chart tiles show a trace.</summary>
        public static IReadOnlyList<OsdStreamSample> FrameSamples(double fps)
        {
            double frametime = 1000.0 / (double.IsFinite(fps) && fps > 1 ? fps : 120);
            var samples = new List<OsdStreamSample>();
            double time = 0;
            for (int index = 0; time < 5000 && index < 16384; index++)
            {
                double value = frametime * (1 + 0.08 * Math.Sin(index * 0.37) + 0.04 * Math.Sin(index * 1.9))
                    + (index % 97 == 50 ? frametime * 0.6 : 0);
                time += value;
                samples.Add(new OsdStreamSample(time, value, frametime));
            }
            return samples;
        }

        /// <summary>A bitmap that shows a known scene at full size, or a generous square for flowing grids.</summary>
        private static (int Width, int Height) BitmapSize(double expectedWidth, double expectedHeight)
        {
            if (expectedWidth <= 0 || expectedHeight <= 0) return (2048, 2048);
            double width = Math.Ceiling(expectedWidth) + 4 * Margin, height = Math.Ceiling(expectedHeight) + 4 * Margin;
            double reduce = Math.Min(1, Math.Min(MaximumSide / width, MaximumSide / height));
            reduce = Math.Min(reduce, Math.Sqrt(MaximumPixels / (width * height)));
            return ((int)Math.Max(64, Math.Floor(width * reduce)), (int)Math.Max(64, Math.Floor(height * reduce)));
        }
    }
}

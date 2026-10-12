using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CapFrameX.OSD.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [STATestClass]
    [DoNotParallelize]
    public class OverlayImageImportTest
    {
        private string _folder;
        private readonly List<string> _files = new List<string>();

        [TestInitialize]
        public void Initialize()
        {
            _folder = Path.Combine(Path.GetTempPath(), "CapFrameX-image-import-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        [TestCleanup]
        public void Cleanup()
        {
            foreach (string file in _files)
                if (File.Exists(file)) File.Delete(file);
            if (Directory.Exists(_folder)) Directory.Delete(_folder);
        }

        [TestMethod]
        public void SvgImportEmbedsATransparentPngThatSurvivesDeletingTheSource()
        {
            using var host = new EditorHost();
            string path = SvgFile("""
                <svg xmlns="http://www.w3.org/2000/svg" width="64" height="48" viewBox="0 0 64 48">
                  <rect x="16" y="12" width="32" height="24" fill="#00ff00" />
                </svg>
                """);

            Assert.IsTrue(host.Editor.ImportImage(path));

            string dataUri = host.Editor.SelectedElement.DataUri;
            var image = Decode(dataUri, "image/png");
            Assert.AreEqual(1024, image.Width);
            Assert.AreEqual(768, image.Height);
            Assert.AreEqual((byte)0, image.RelativePixel(0.05, 0.05).A, "SVG transparency must survive conversion to the embedded PNG.");
            var center = image.RelativePixel(0.5, 0.5);
            Assert.IsTrue(center.A > 250 && center.G > 240 && center.R < 20 && center.B < 20,
                "The SVG geometry must be rendered, not replaced with a blank or placeholder image.");
            File.Delete(path);
            string json = host.Editor.GetTemplateJson();
            Assert.IsFalse(json.Contains(path, StringComparison.OrdinalIgnoreCase), "Profiles must not retain a dependency on the import path.");
            var restored = OsdDesignDocument.FromJson(json);
            Assert.AreEqual(dataUri, restored.Tiles[1].DataUri);
            Assert.AreEqual(json, restored.ToJson());
            Assert.AreEqual((byte)0, Decode(restored.Tiles[1].DataUri, "image/png").RelativePixel(0.05, 0.05).A);
        }

        [TestMethod]
        public void SvgGradientTransformAndLocalUseRenderIntoTheEmbeddedImage()
        {
            using var host = new EditorHost();
            string path = SvgFile("""
                <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink"
                     width="120" height="80" viewBox="0 0 120 80">
                  <defs>
                    <linearGradient id="shade" x1="0" y1="0" x2="1" y2="0">
                      <stop offset="0" stop-color="#ff0000" />
                      <stop offset="1" stop-color="#0000ff" />
                    </linearGradient>
                    <g id="badge"><rect width="60" height="40" fill="url(#shade)" /></g>
                  </defs>
                  <g transform="translate(30 20)"><use xlink:href="#badge" /></g>
                </svg>
                """);

            Assert.IsTrue(host.Editor.ImportImage(path), "Ordinary self-contained SVG gradients and reusable geometry must be supported.");

            var image = Decode(host.Editor.SelectedElement.DataUri, "image/png");
            Assert.AreEqual(1024, image.Width);
            Assert.AreEqual(683, image.Height);
            Assert.AreEqual((byte)0, image.RelativePixel(10.0 / 120, 30.0 / 80).A, "The translated shape must not be drawn at its untransformed origin.");
            Assert.AreEqual((byte)0, image.RelativePixel(100.0 / 120, 30.0 / 80).A);
            var left = image.RelativePixel(35.0 / 120, 0.5);
            var right = image.RelativePixel(85.0 / 120, 0.5);
            Assert.IsTrue(left.A > 250 && right.A > 250, "The local use reference must produce visible geometry.");
            Assert.IsTrue(left.R > left.B + 150 && right.B > right.R + 150,
                "The left-to-right color gradient must remain intact after rasterization.");
        }

        [TestMethod]
        [DataRow(64, 32, 1024, 512)]
        [DataRow(4096, 2048, 1024, 512)]
        [DataRow(2048, 4096, 512, 1024)]
        public void SvgImportPreservesAspectRatioAndBoundsRasterDimensions(int width, int height, int expectedWidth, int expectedHeight)
        {
            using var host = new EditorHost();
            string path = SvgFile($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\"><rect width=\"{width}\" height=\"{height}\" fill=\"#76b900\" /></svg>");

            Assert.IsTrue(host.Editor.ImportImage(path));

            var image = Decode(host.Editor.SelectedElement.DataUri, "image/png");
            Assert.AreEqual(expectedWidth, image.Width);
            Assert.AreEqual(expectedHeight, image.Height);
            Assert.IsTrue(image.Width <= 1024 && image.Height <= 1024);
            Assert.IsTrue(image.EncodedLength <= 128 * 1024, "The rasterized SVG must obey the same portable payload budget as other image imports.");
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void MillimeterSvgViewportPreservesItsAspectRatioAndGeometryBounds(bool transparentPadding)
        {
            using var host = new EditorHost();
            string rectangle = transparentPadding
                ? "<rect x=\"16\" y=\"8\" width=\"32\" height=\"16\" fill=\"#00ff80\" />"
                : "<rect width=\"64\" height=\"32\" fill=\"#00ff80\" />";
            string path = SvgFile("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"64mm\" height=\"32mm\" viewBox=\"0 0 64 32\">" + rectangle + "</svg>");

            Assert.IsTrue(host.Editor.ImportImage(path));

            var image = Decode(host.Editor.SelectedElement.DataUri, "image/png");
            Assert.AreEqual(1024, image.Width);
            Assert.AreEqual(512, image.Height, "Physical root units must preserve the viewBox aspect ratio.");
            Rect visible = FindOpaqueBounds(image);
            Assert.IsFalse(visible.IsEmpty);
            Rect expected = transparentPadding ? new Rect(256, 128, 512, 256) : new Rect(0, 0, 1024, 512);
            Assert.AreEqual(expected.Left, visible.Left, 1.5, "Millimeter conversion must not shift the rendered geometry or crop intentional padding.");
            Assert.AreEqual(expected.Top, visible.Top, 1.5);
            Assert.AreEqual(expected.Width, visible.Width, 1.5, "The viewBox geometry must fill its intended viewport instead of shrinking by the physical-unit conversion factor.");
            Assert.AreEqual(expected.Height, visible.Height, 1.5);
            Assert.AreEqual(transparentPadding ? (byte)0 : (byte)255, image.RelativePixel(0.05, 0.05).A);
            Assert.AreEqual((byte)255, image.RelativePixel(0.5, 0.5).A);
        }

        [TestMethod]
        public void SvgImportIsOneUndoableReplacementOfTheSelectedImage()
        {
            using var host = new EditorHost();
            string before = host.Editor.GetTemplateJson();
            string path = SvgFile("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"40\" height=\"40\"><circle cx=\"20\" cy=\"20\" r=\"15\" fill=\"red\" /></svg>");

            Assert.IsTrue(host.Editor.ImportImage(path));
            Assert.AreNotEqual(before, host.Editor.GetTemplateJson());
            Assert.AreEqual(2, host.Editor.Document.Tiles.Count, "Importing into an image tile must replace its contents without adding another tile.");
            ((Button)host.Editor.FindName("UndoButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.AreEqual(before, host.Editor.GetTemplateJson());
            Assert.IsFalse(((Button)host.Editor.FindName("UndoButton")).IsEnabled, "One undo must completely revert the image import.");
        }

        [TestMethod]
        [DataRow("malformed")]
        [DataRow("external-image")]
        [DataRow("external-use")]
        [DataRow("external-style")]
        [DataRow("external-entity")]
        [DataRow("script")]
        [DataRow("cyclic-use")]
        public void InvalidOrExternalSvgLeavesTheSelectedImageAndUndoHistoryUnchanged(string kind)
        {
            string svg = kind switch
            {
                "malformed" => "<svg xmlns=\"http://www.w3.org/2000/svg\"><path",
                "external-image" => "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"40\" height=\"40\"><image href=\"https://example.invalid/external.png\" width=\"40\" height=\"40\" /></svg>",
                "external-use" => "<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" width=\"40\" height=\"40\"><use xlink:href=\"other.svg#logo\" /></svg>",
                "external-style" => "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"40\" height=\"40\"><style>@import url('https://example.invalid/external.css');</style><rect width=\"40\" height=\"40\" /></svg>",
                "external-entity" => "<!DOCTYPE svg [<!ENTITY external SYSTEM \"https://example.invalid/external.txt\">]><svg xmlns=\"http://www.w3.org/2000/svg\" width=\"40\" height=\"40\"><text>&external;</text></svg>",
                "script" => "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"40\" height=\"40\"><script>var unexpected = true;</script><rect width=\"40\" height=\"40\" /></svg>",
                "cyclic-use" => "<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" width=\"40\" height=\"40\"><defs><g id=\"loop\"><use xlink:href=\"#loop\" /></g></defs><use xlink:href=\"#loop\" /></svg>",
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
            using var host = new EditorHost();
            AssertRejectedWithoutMutation(host, SvgFile(svg));
        }

        [TestMethod]
        public void SvgLargerThanTheSourceFileBudgetIsRejectedWithoutChangingTheImage()
        {
            string path = NewPath(".svg");
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                writer.Write("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"40\" height=\"40\"><!--");
                string padding = new string(' ', 4096);
                for (int index = 0; index < 4096; index++) writer.Write(padding);
                writer.Write("--><rect width=\"40\" height=\"40\" /></svg>");
            }
            Assert.IsTrue(new FileInfo(path).Length > 16 * 1024 * 1024);
            using var host = new EditorHost();
            AssertRejectedWithoutMutation(host, path);
        }

        [TestMethod]
        [DataRow(".png", "image/png")]
        [DataRow(".jpg", "image/jpeg")]
        [DataRow(".jpeg", "image/jpeg")]
        public void ExistingRasterFormatsStillImportAsPortableImages(string extension, string mimeType)
        {
            const int width = 160, height = 80;
            string path = NewPath(extension);
            var pixels = new byte[width * height * 4];
            for (int index = 0; index < pixels.Length; index += 4)
            {
                pixels[index + 2] = 255;
                pixels[index + 3] = 255;
            }
            var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
            BitmapEncoder encoder = extension == ".png" ? new PngBitmapEncoder() : new JpegBitmapEncoder { QualityLevel = 95 };
            encoder.Frames.Add(BitmapFrame.Create(source));
            using (var output = File.Create(path)) encoder.Save(output);
            using var host = new EditorHost();

            Assert.IsTrue(host.Editor.ImportImage(path));

            var image = Decode(host.Editor.SelectedElement.DataUri, mimeType);
            Assert.AreEqual(width, image.Width);
            Assert.AreEqual(height, image.Height);
            var center = image.Pixel(width / 2, height / 2);
            Assert.IsTrue(center.R > 220 && center.G < 40 && center.B < 40 && center.A > 250);
        }

        private static void AssertRejectedWithoutMutation(EditorHost host, string path)
        {
            string before = host.Editor.GetTemplateJson();
            var selected = host.Editor.SelectedElement;
            Assert.IsFalse(host.Editor.ImportImage(path));
            Assert.AreSame(selected, host.Editor.SelectedElement);
            Assert.AreEqual(before, host.Editor.GetTemplateJson(), "A failed import must retain the existing image and all tile properties.");
            Assert.IsFalse(((Button)host.Editor.FindName("UndoButton")).IsEnabled, "A failed import must not introduce an undo step.");
            Assert.IsTrue(host.Editor.TryCommitEdits(), "Rejecting a file must leave the existing design valid and editable.");
        }

        private string SvgFile(string contents)
        {
            string path = NewPath(".svg");
            File.WriteAllText(path, contents, new UTF8Encoding(false));
            return path;
        }

        private string NewPath(string extension)
        {
            string path = Path.Combine(_folder, _files.Count.ToString() + extension);
            _files.Add(path);
            return path;
        }

        private static DecodedImage Decode(string dataUri, string mimeType)
        {
            string prefix = "data:" + mimeType + ";base64,";
            StringAssert.StartsWith(dataUri, prefix);
            byte[] encoded = Convert.FromBase64String(dataUri.Substring(prefix.Length));
            using var stream = new MemoryStream(encoded);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var bitmap = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);
            var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
            bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
            return new DecodedImage(bitmap.PixelWidth, bitmap.PixelHeight, encoded.Length, pixels);
        }

        private static Rect FindOpaqueBounds(DecodedImage image)
        {
            int minX = image.Width, minY = image.Height, maxX = -1, maxY = -1;
            for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++)
            {
                if (image.Pixel(x, y).A < 200) continue;
                minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
            }
            return maxX < minX ? Rect.Empty : new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private sealed class DecodedImage
        {
            public int Width { get; }
            public int Height { get; }
            public int EncodedLength { get; }
            private readonly byte[] _pixels;
            public DecodedImage(int width, int height, int encodedLength, byte[] pixels)
            {
                Width = width; Height = height; EncodedLength = encodedLength; _pixels = pixels;
            }
            public (byte B, byte G, byte R, byte A) Pixel(int x, int y)
            {
                int offset = (y * Width + x) * 4;
                return (_pixels[offset], _pixels[offset + 1], _pixels[offset + 2], _pixels[offset + 3]);
            }
            public (byte B, byte G, byte R, byte A) RelativePixel(double x, double y) =>
                Pixel(Math.Clamp((int)Math.Floor(x * Width), 0, Width - 1), Math.Clamp((int)Math.Floor(y * Height), 0, Height - 1));
        }

        private sealed class EditorHost : IDisposable
        {
            public OsdEditorControl Editor { get; } = new OsdEditorControl();
            public EditorHost()
            {
                var document = new OsdDesignDocument { Width = 640, CanvasHeight = 480, IsCanvas = true };
                document.Tiles.Add(new OsdDesignTile
                {
                    Kind = "text", Label = "Image fixture", Text = "Image import",
                    X = 200, Y = 200, FrameWidth = 240, FrameHeight = 100
                });
                Editor.SetTemplateJson(document.ToJson());
                Editor.AddElement("image");
                Assert.IsTrue(Editor.TryCommitEdits());
                Editor.ResetHistory();
            }
            public void Dispose() => Editor.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        }
    }
}

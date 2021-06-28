using OxyPlot;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace CapFrameX.Data
{
    public static class ImageExport
    {

        private static MemoryStream ExportToStream(PlotModel plot, int horizontalRes, int verticalRes)
        {
            var exporter = new SvgExporter { Width = horizontalRes, Height = verticalRes, IsDocument = true };
            var memStream = new MemoryStream();
            exporter.Export(plot, memStream);
            return memStream;
        }

        private static void SaveFile(string fileName, string filter, string extension, byte[] data)
        {
            var illegalFilenameCharsRegex = new Regex(@"[/:*?<>""|]");

            SaveFileDialog dialog = new SaveFileDialog()
            {
                Filter = filter,
                FileName = $"{illegalFilenameCharsRegex.Replace(fileName, string.Empty)}",
                DefaultExt = extension,
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                File.WriteAllBytes(dialog.FileName, data);
            }
        }

        public static void SavePlotAsSVG(PlotModel plot,string filename, int horizontalRes, int verticalRes)
        {
            var stream = ExportToStream(plot, horizontalRes, verticalRes);
            SaveFile(filename, "SVG files|*.svg", "svg", stream.ToArray());
            stream.Dispose();
        }

        public static void SavePlotAsPNG(PlotModel plot, string filename, int horizontalRes, int verticalRes, bool isDarkMode)
        {
            plot.Background = isDarkMode ? OxyColor.Parse("#414b54") : OxyColor.Parse("#f2f2f2");

            var exporter = new OxyPlot.Wpf.PngExporter {
                Width = horizontalRes,
                Height = verticalRes
            };  

            using(var memoryStream = new MemoryStream())
            {
                exporter.Export(plot, memoryStream);
                SaveFile(filename, "PNG files|*.png", "png", memoryStream.ToArray());
            }
        }
    }
}

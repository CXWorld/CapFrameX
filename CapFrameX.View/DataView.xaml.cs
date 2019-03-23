using CapFrameX.Configuration;
using CapFrameX.Statistics;
using CapFrameX.ViewModel;
using LiveCharts;
using LiveCharts.Wpf;
using Prism.Events;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CapFrameX.View
{
    /// <summary>
    /// Interaction logic for DataView.xaml
    /// </summary>
    public partial class DataView : UserControl
    {
        public DataView()
        {
            InitializeComponent();

            // Design time!
            if (DesignerProperties.GetIsInDesignMode(this))
            {
                DataContext = new DataViewModel(new FrametimeStatisticProvider(),
                    new FrametimeAnalyzer(), new EventAggregator(), new CapFrameXConfiguration());
            }
        }

        private void ResetChart_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            //Use the axis MinValue/MaxValue properties to specify the values to display.
            //use double.Nan to clear it.

            FrametimesX.MinValue = double.NaN;
            FrametimesX.MaxValue = double.NaN;
            FrametimesY.MinValue = double.NaN;
            FrametimesY.MaxValue = double.NaN;

            LShapeX.MinValue = double.NaN;
            LShapeX.MaxValue = double.NaN;
            LShapeY.MinValue = double.NaN;
            LShapeY.MaxValue = double.NaN;
        }

        private void Chart_OnDataClick(object sender, ChartPoint chartpoint)
        {
            var chart = (PieChart)chartpoint.ChartView;

            //clear selected slice.
            foreach (PieSeries series in chart.Series)
                series.PushOut = 0;

            var selectedSeries = (PieSeries)chartpoint.SeriesView;
            selectedSeries.PushOut = 8;
        }

        private void GitHubButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/DevTechProfile/CapFrameX");
        }

        private bool WriteTransformedBitmapToFile<T>(BitmapSource bitmapSource, string fileName) where T : BitmapEncoder, new()
        {
            if (string.IsNullOrEmpty(fileName) || bitmapSource == null)
                return false;

            //creating frame and putting it to Frames collection of selected encoder
            var frame = BitmapFrame.Create(bitmapSource);
            var encoder = new T();
            encoder.Frames.Add(frame);
            try
            {
                using (var fs = new FileStream(fileName, FileMode.Create))
                {
                    encoder.Save(fs);
                }
            }
            catch (Exception e)
            {
                return false;
            }
            return true;
        }

        private BitmapImage GetBitmapImage<T>(BitmapSource bitmapSource) where T : BitmapEncoder, new()
        {
            var frame = BitmapFrame.Create(bitmapSource);
            var encoder = new T();
            encoder.Frames.Add(frame);
            var bitmapImage = new BitmapImage();
            bool isCreated;
            try
            {
                using (var ms = new MemoryStream())
                {
                    encoder.Save(ms);

                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = ms;
                    bitmapImage.EndInit();
                    isCreated = true;
                }
            }
            catch
            {
                isCreated = false;
            }
            return isCreated ? bitmapImage : null;
        }

        private Bitmap ResizeImage(Bitmap imgToResize, System.Drawing.Size size)
        {
            int sourceWidth = imgToResize.Width;
            int sourceHeight = imgToResize.Height;

            float nPercent = 0;
            float nPercentW = 0;
            float nPercentH = 0;

            nPercentW = ((float)size.Width / (float)sourceWidth);
            nPercentH = ((float)size.Height / (float)sourceHeight);

            if (nPercentH < nPercentW)
                nPercent = nPercentH;
            else
                nPercent = nPercentW;

            int destWidth = (int)(sourceWidth * nPercent);
            int destHeight = (int)(sourceHeight * nPercent);

            Bitmap b = new Bitmap(destWidth, destHeight);
            Graphics g = Graphics.FromImage(b);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;

            g.DrawImage(imgToResize, 0, 0, destWidth, destHeight);
            g.Dispose();

            return b;
        }

        /// <summary>
        /// Exporting png pictures
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TakeScreenShotButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(DataContext is DataViewModel viewModel))
            {
                return;
            }

            string path = viewModel.AppConfiguration.ScreenshotDirectory;

            try
            {
                if (path.Contains(@"MyDocuments\OCAT\Screenshots"))
                {
                    var documentFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    path = Path.Combine(documentFolder, @"OCAT\Screenshots");
                }

                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                var filename = Path.Combine(path, viewModel.RecordInfo.GameName + "_" +
                    DateTime.Now.ToString("yyyy-dd-M_HH-mm-ss") + "_CX_Analysis.png");

                VisualBrush visualBrush = new VisualBrush(ScreenshotAreaGrid);

                // Gets the size of the images (I assume each image has the same size)
                int imageWidth = (int)ScreenshotAreaGrid.ActualWidth;
                int imageHeight = (int)ScreenshotAreaGrid.ActualHeight;

                // Draws the images into a DrawingVisual component
                DrawingVisual drawingVisual = new DrawingVisual();
                using (DrawingContext drawingContext = drawingVisual.RenderOpen())
                {
                    drawingContext.DrawRectangle(visualBrush, null, new Rect(new System.Windows.Point(0, 0), new System.Windows.Point(imageWidth, imageHeight)));
                }

                double dpi = 1.3 * 96;
                double scale = dpi / 96;

                // Converts the Visual (DrawingVisual) into a BitmapSource
                RenderTargetBitmap bmp = new RenderTargetBitmap((int)(scale * imageWidth), (int)(scale * imageHeight), dpi, dpi, PixelFormats.Pbgra32);
                bmp.Render(drawingVisual);

                //PngBitmapEncoder pngImage = new PngBitmapEncoder();
                MemoryStream stream = new MemoryStream();
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bmp));
                encoder.Save(stream);

                Bitmap bitmap = new Bitmap(stream);
                bitmap.Save(filename);


                //pngImage.Frames.Add(BitmapFrame.Create(ResizeImage(bmp, new System.Drawing.Size(imageWidth, imageHeight))));
                //using (Stream fileStream = File.Create(filename))
                //{
                //    pngImage.Save(fileStream);
                //}
            }
            catch
            {
                return;
            }
        }
    }
}

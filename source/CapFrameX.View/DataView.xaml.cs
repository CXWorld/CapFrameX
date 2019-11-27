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
using System.IO;
using System.Reactive.Linq;
using System.Threading;
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
				var appConfiguration = new CapFrameXConfiguration();
				DataContext = new DataViewModel(new FrametimeStatisticProvider(appConfiguration),
					new FrametimeAnalyzer(), new EventAggregator(), appConfiguration);
			}

			var context = SynchronizationContext.Current;
			(DataContext as DataViewModel)?.ResetLShapeChart
				.ObserveOn(context)
				.SubscribeOn(context)
				.Subscribe(dummy => ResetLShapeChart());
		}

		private void Chart_OnDataClick(object sender, ChartPoint chartpoint)
		{
			var chart = (PieChart)chartpoint.ChartView;

			//clear selected slice
			foreach (PieSeries series in chart.Series)
				series.PushOut = 0;

			var selectedSeries = (PieSeries)chartpoint.SeriesView;
			selectedSeries.PushOut = 8;
		}

		private void ResetChart_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
			=> ResetLShapeChart();

		private void ResetLShapeChart()
		{
			//Use the axis MinValue/MaxValue properties to specify the values to display.
			//use double.Nan to clear it.

			LShapeX.MinValue = double.NaN;
			LShapeX.MaxValue = double.NaN;
			LShapeY.MinValue = double.NaN;
			LShapeY.MaxValue = double.NaN;
		}

		// Unused code
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
				if (path.Contains(@"MyDocuments\CapFrameX\Screenshots"))
				{
					var documentFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
					path = Path.Combine(documentFolder, @"CapFrameX\Screenshots");
				}

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

				VisualBrush visualBrush = new VisualBrush(ScreenshotArea);

				// Gets the size of the images (I assume each image has the same size)
				int imageWidth = (int)ScreenshotArea.ActualWidth;
				int imageHeight = (int)ScreenshotArea.ActualHeight;


				// Draws the images into a DrawingVisual component
				DrawingVisual drawingVisual = new DrawingVisual();
				using (DrawingContext drawingContext = drawingVisual.RenderOpen())
				{
					drawingContext.DrawRectangle(visualBrush, null, new Rect(new System.Windows.Point(0, 0), new System.Windows.Point(imageWidth, imageHeight)));
				}


				// Converts the Visual (DrawingVisual) into a BitmapSource
				RenderTargetBitmap rtb = new RenderTargetBitmap(
				imageWidth, imageHeight, 96, 96, PixelFormats.Pbgra32);
				rtb.Render(drawingVisual);

				using (MemoryStream stream = new MemoryStream())
				{
					BitmapEncoder encoder = new PngBitmapEncoder();
					encoder.Frames.Add(BitmapFrame.Create(rtb));
					encoder.Save(stream);

					Bitmap bitmap = new Bitmap(stream);
					bitmap.Save(filename);
				}
			}
			catch
			{ return; }
		}
	}
}

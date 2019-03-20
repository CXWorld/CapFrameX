using CapFrameX.Configuration;
using CapFrameX.Statistics;
using CapFrameX.ViewModel;
using LiveCharts;
using LiveCharts.Wpf;
using Prism.Events;
using System;
using System.ComponentModel;
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

		private void TakeScreenShotButton_Click(object sender, RoutedEventArgs e)
		{
			DataViewModel viewModel = DataContext as DataViewModel;

			if (viewModel == null)
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

				var filename = Path.Combine(path, viewModel.RecordInfo.GameName + "_" + DateTime.Now.ToString("yyyy-dd-M_HH-mm-ss") + "_CX_Analysis.png");

				VisualBrush visualBrush = new VisualBrush(ScreenshotAreaGrid);

				// Gets the size of the images (I assume each image has the same size)
				int imageWidth = (int)ScreenshotAreaGrid.ActualWidth;
				int imageHeight = (int)ScreenshotAreaGrid.ActualHeight;

				// Draws the images into a DrawingVisual component
				DrawingVisual drawingVisual = new DrawingVisual();
				using (DrawingContext drawingContext = drawingVisual.RenderOpen())
				{
					drawingContext.DrawRectangle(visualBrush, null, new Rect(new Point(0, 0), new Point(imageWidth, imageHeight)));
				}

				// Converts the Visual (DrawingVisual) into a BitmapSource
				RenderTargetBitmap bmp = new RenderTargetBitmap(imageWidth, imageHeight, 96, 96, PixelFormats.Pbgra32);
				bmp.Render(drawingVisual);

				PngBitmapEncoder pngImage = new PngBitmapEncoder();
				pngImage.Frames.Add(BitmapFrame.Create(bmp));
				using (Stream fileStream = File.Create(filename))
				{
					pngImage.Save(fileStream);
				}
			}
			catch
			{
				return;
			}
		}
	}
}

using CapFrameX.Configuration;
using CapFrameX.Data;
using CapFrameX.ViewModel;
using Microsoft.Extensions.Logging;
using Prism.Events;
using Prism.Regions;
using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CapFrameX.View
{
	/// <summary>
	/// Interaction logic for ColorbarView.xaml
	/// </summary>
	public partial class ColorbarView : UserControl
	{
		public ColorbarView()
		{
			InitializeComponent();

			// Design time!
			if (DesignerProperties.GetIsInDesignMode(this))
			{
				var appConfiguration = new CapFrameXConfiguration();
				DataContext = new ColorbarViewModel(new RegionManager(), new RecordDirectoryObserver(appConfiguration,
					new LoggerFactory().CreateLogger<RecordDirectoryObserver>()), 
					new EventAggregator(), appConfiguration, null);
			}
		}

		private void PopupBox_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e) {}

		private void GitHubButton_Click(object sender, RoutedEventArgs e)
		{
			System.Diagnostics.Process.Start("https://github.com/DevTechProfile/CapFrameX#capframex");
		}

		private void Donate_Button_Click(object sender, RoutedEventArgs e)
		{
			System.Diagnostics.Process.Start("https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=A4VJPT9NB7G28&source=url");
		}

		/// <summary>
		/// Exporting png pictures
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void TakeScreenShotButton_Click(object sender, RoutedEventArgs e)
		{
			string path = (DataContext as ColorbarViewModel).AppConfiguration.ScreenshotDirectory;

			try
			{
				if (path.Contains(@"MyDocuments\CapFrameX\Screenshots"))
				{
					var documentFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
					path = Path.Combine(documentFolder, @"CapFrameX\Screenshots");
				}

				if (!Directory.Exists(path))
				{
					Directory.CreateDirectory(path);
				}

				var filename = Path.Combine(path, "Screenshot" + "_" +
					DateTime.Now.ToString("yyyy-dd-M_HH-mm-ss") + "_CX_Analysis.png");

				var screenShotArea = (DataContext as ColorbarViewModel).Shell.GlobalScreenshotArea;

				if (screenShotArea == null)
					return;

				VisualBrush visualBrush = new VisualBrush(screenShotArea);

				// Gets the size of the images (I assume each image has the same size)
				int imageWidth = (int)screenShotArea.ActualWidth;
				int imageHeight = (int)screenShotArea.ActualHeight;

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
					System.Drawing.Image logoName = (System.Drawing.Image)Properties.Resources.ResourceManager.GetObject("CX_Screen_Logo_Name");
					System.Drawing.Image logoDescription = (System.Drawing.Image)Properties.Resources.ResourceManager.GetObject("CX_Screen_Logo_Description");


					// Add fill rectangle
					AddFillRectangle(bitmap, new System.Drawing.Point(0, imageHeight - logoDescription.Height),
						new System.Drawing.Size(imageWidth, logoDescription.Height), new SolidBrush(System.Drawing.Color.FromArgb(255, 32, 141, 228)));

					// Add frame
					AddRectangle(bitmap, new System.Drawing.Point(1, 1),
						new System.Drawing.Size(imageWidth - 2, imageHeight), new SolidBrush(System.Drawing.Color.FromArgb(255, 32, 141, 228)));

					// Add CX logos
					AddLogo(bitmap, logoName, new System.Drawing.Point(0, imageHeight - logoName.Height));
					AddLogo(bitmap, logoDescription, new System.Drawing.Point(imageWidth - logoDescription.Width, imageHeight - logoDescription.Height));

					bitmap.Save(filename);
				}
			}
			catch
			{ return; }
		}

		private static Bitmap AddRectangle(Bitmap bitmap, System.Drawing.Point position, System.Drawing.Size size, System.Drawing.Brush brush)
		{
			using (Graphics grf = Graphics.FromImage(bitmap))
			{
				Rectangle rect = new Rectangle(position, size);
				grf.DrawRectangle(new System.Drawing.Pen(brush, 2), rect);
			}

			return bitmap;
		}

		private static Bitmap AddFillRectangle(Bitmap bitmap, System.Drawing.Point position, System.Drawing.Size size, System.Drawing.Brush brush)
		{
			using (Graphics grf = Graphics.FromImage(bitmap))
			{
				Rectangle rect = new Rectangle(position, size);
				grf.FillRectangle(brush, rect);
			}

			return bitmap;
		}

		private static Bitmap AddLogo(Bitmap bitmap, System.Drawing.Image image, System.Drawing.Point location)
		{
			using (Graphics grf = Graphics.FromImage(bitmap))
			{
				grf.DrawImageUnscaledAndClipped(image, new Rectangle(location, new System.Drawing.Size(image.Width, image.Height)));
			}

			return bitmap;
		}

		private void PopupBox_Closed(object sender, RoutedEventArgs e)
		{
			(DataContext as ColorbarViewModel).OptionsViewSelected = true;
		}
	}
}

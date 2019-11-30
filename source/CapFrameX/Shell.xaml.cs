using CapFrameX.Configuration;
using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CapFrameX
{
	/// <summary>
	/// Interaction logic for Shell.xaml
	/// </summary>
	public partial class Shell : Window
	{
		public Shell()
		{
			InitializeComponent();

			// Start tracking the Window instance.
			WindowStatServices.Tracker.Track(this);
		}

		/// <summary>
		/// Exporting png pictures
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void TakeScreenShotButton_Click(object sender, RoutedEventArgs e)
		{
			string path = CapFrameXConfiguration.Instance.ScreenshotDirectory;

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
					Image logo = (Image)Properties.Resources.ResourceManager.GetObject("CX_Screen_Logo");

					// Add fill rectangle
					AddFillRectangle(bitmap, new System.Drawing.Point(0, imageHeight - logo.Height),
						new System.Drawing.Size(imageWidth, logo.Height), new SolidBrush(System.Drawing.Color.FromArgb(255, 32, 141, 228)));

					// Add frame
					AddRectangle(bitmap, new System.Drawing.Point(1, 1),
						new System.Drawing.Size(imageWidth - 2, imageHeight), new SolidBrush(System.Drawing.Color.FromArgb(255, 32, 141, 228)));

					// Add CX logo
					AddLogo(bitmap, logo, new System.Drawing.Point(0, imageHeight - logo.Height));

					bitmap.Save(filename);
				}
			}
			catch (Exception ex)
			{
				return;
			}
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

		private static Bitmap AddLogo(Bitmap bitmap, Image image, System.Drawing.Point location)
		{
			using (Graphics grf = Graphics.FromImage(bitmap))
			{
				grf.DrawImageUnscaledAndClipped(image, new Rectangle(location, new System.Drawing.Size(image.Width, image.Height)));
			}

			return bitmap;
		}
	}
}

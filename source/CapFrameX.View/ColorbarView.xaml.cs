using CapFrameX.Configuration;
using CapFrameX.Data;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.ViewModel;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Logging;
using Prism.Events;
using Prism.Regions;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace CapFrameX.View
{
	/// <summary>
	/// Interaction logic for ColorbarView.xaml
	/// </summary>
	public partial class ColorbarView : UserControl
	{
		private static readonly string REGEX_SEARCH = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());

		public ColorbarView()
		{
			InitializeComponent();
		}

		private void PopupBox_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e) { }

		private void GitHubButton_Click(object sender, RoutedEventArgs e)
		{
			Process.Start("https://github.com/DevTechProfile/CapFrameX#capframex");
		}

		private void Donate_Button_Click(object sender, RoutedEventArgs e)
		{
			Process.Start("https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=A4VJPT9NB7G28&source=url");
		}

		/// <summary>
		/// Exporting png pictures
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void TakeScreenShotButton_Click(object sender, RoutedEventArgs e)
		{
			string path = (DataContext as ColorbarViewModel).AppConfiguration.ScreenshotDirectory;
			var filename = string.Empty;
			var currentPageName = (DataContext as ColorbarViewModel).CurrentPageName;
			var currentRecordInfo = (DataContext as ColorbarViewModel).RecordInfo;

			try
			{
				if (path.Contains(@"MyDocuments\CapFrameX\Screenshots"))
				{
					var documentFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
					path = Path.Combine(documentFolder, @"CapFrameX\Screenshots");
				}

				if (currentPageName == "Analysis")
				{
					var name = "CX" + "_" +
						  DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "_" + $"{currentRecordInfo.GameName}" + "_" + $"{ currentRecordInfo.Comment}.png";

					Regex r = new Regex(string.Format("[{0}]", Regex.Escape(REGEX_SEARCH)));
					var adjustedName = r.Replace(name, " ");
					filename = Path.Combine(path, adjustedName);
				}
				else
				{
					filename = Path.Combine(path, "CX" + "_" +
						   DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "_" + $"{currentPageName}.png");
				}

				if (!Directory.Exists(path))
				{
					Directory.CreateDirectory(path);
				}

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
			catch (Exception ex)
			{
				var logger = (DataContext as ColorbarViewModel).Logger;
				logger.LogError(ex, "Screenshot {filename} could not be created", filename);
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
			var viewModel = DataContext as ColorbarViewModel;
			viewModel.OptionsViewSelected = true;
			viewModel.OptionPopupClosed.Publish(new ViewMessages.OptionPopupClosed());
		}

		private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
			e.Handled = true;
		}

		private void Login_Click(object sender, RoutedEventArgs e)
		{
			(DataContext as ColorbarViewModel).OpenLoginWindow();
		}

		private void Logout_Click(object sender, RoutedEventArgs e)
		{
			(DataContext as ColorbarViewModel).Logout();
		}

		private void MenuDarkModeButton_Click(object sender, RoutedEventArgs e)
		{
			var toggleButton = sender as ToggleButton;
			ModifyTheme(theme => theme.SetBaseTheme(toggleButton.IsChecked == true ? Theme.Dark : Theme.Light));
		}

		private static void ModifyTheme(Action<ITheme> modificationAction)
		{
			PaletteHelper paletteHelper = new PaletteHelper();
			ITheme theme = paletteHelper.GetTheme();
			modificationAction?.Invoke(theme);
			paletteHelper.SetTheme(theme);
		}
    }
}

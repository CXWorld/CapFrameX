using CapFrameX.EventAggregation.Messages;
using CapFrameX.View.Themes;
using CapFrameX.ViewModel;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
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
            ModifyTheme((DataContext as ColorbarViewModel).AppConfiguration.UseDarkMode);
        }

        private void PopupBox_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e) { }

        private void GitHubButton_Click(object sender, RoutedEventArgs e)
        {
            _ = Process.Start("https://github.com/DevTechProfile/CapFrameX#capframex");
        }

        private void Donate_Button_Click(object sender, RoutedEventArgs e)
        {
            _ = Process.Start("https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=A4VJPT9NB7G28&source=url");
        }

        /// <summary>
        /// Exporting png pictures
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveScreenShotButton_Click(object sender, RoutedEventArgs e)
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
                    _ = Directory.CreateDirectory(path);
                }

                var bitmap = GetBitmapFromScreenshotArea();
                bitmap.Save(filename);
            }
            catch (Exception ex)
            {
                var logger = (DataContext as ColorbarViewModel).Logger;
                logger.LogError(ex, "Screenshot {filename} could not be created", filename);
            }
        }

        /// <summary>
        /// Copy png to clipboard
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CopyScreenShotButton_Click(object sender, RoutedEventArgs e)
        {
            var bitmap = GetBitmapFromScreenshotArea();
            Clipboard.SetDataObject(bitmap);
        }

        private Bitmap GetBitmapFromScreenshotArea()
        {
            var screenShotArea = (DataContext as ColorbarViewModel).Shell.GlobalScreenshotArea;

            if (screenShotArea == null)
                return null;

            Bitmap bitmap = null;
            VisualBrush visualBrush = new VisualBrush(screenShotArea);

            double factor = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice.M11;

            // Gets the size of the images (I assume each image has the same size)
            const int upperRectangleHeight = 3;
            int lowerRectangleHeight = (int)(60 * factor);

            int imageWidth = (int)(screenShotArea.ActualWidth * factor);
            int imageHeight = (int)(screenShotArea.ActualHeight * factor + upperRectangleHeight * factor);

            // Draws the images into a DrawingVisual component
            DrawingVisual drawingVisual = new DrawingVisual();
            using (DrawingContext drawingContext = drawingVisual.RenderOpen())
            {
                drawingContext.DrawRectangle(visualBrush, null, new Rect(new System.Windows.Point(0, upperRectangleHeight), new System.Windows.Point(imageWidth, imageHeight)));
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

                bitmap = new Bitmap(stream);
                System.Drawing.Image logoName = (System.Drawing.Image)Properties.Resources.ResourceManager.GetObject("CX_Screen_Logo_Name");

                // Add upper rectangle
                AddFillRectangle(bitmap, new System.Drawing.Point(0, 0),
                    new System.Drawing.Size(imageWidth, upperRectangleHeight), new SolidBrush(System.Drawing.Color.FromArgb(255, 32, 141, 228)));

                // Add lower rectangle
                AddFillRectangle(bitmap, new System.Drawing.Point(0, imageHeight - lowerRectangleHeight),
                    new System.Drawing.Size(imageWidth, lowerRectangleHeight), new SolidBrush(System.Drawing.Color.FromArgb(255, 32, 141, 228)));

                // Add frame
                AddRectangle(bitmap, new System.Drawing.Point(1, 1),
                    new System.Drawing.Size(imageWidth - 2, imageHeight), new SolidBrush(System.Drawing.Color.FromArgb(255, 32, 141, 228)));

                // Add CX logos
                AddLogo(bitmap, logoName, new System.Drawing.Point(20, imageHeight - logoName.Height - (lowerRectangleHeight - logoName.Height) / 2));
            }

            return bitmap;
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

        private void TextBox_MouseLeave(object sender, MouseEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox.Text == string.Empty || textBox.Text == "0")
                textBox.Text = "500";

            Keyboard.ClearFocus();
        }

        private void ResolutionTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var key = e.Key;
            var textBox = sender as TextBox;
            if (key == Key.Enter)
            {
                if (textBox.Text == string.Empty || textBox.Text == "0")
                    textBox.Text = "500";

                Keyboard.ClearFocus();
            }
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9.]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void IntegerValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
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
            ModifyTheme(toggleButton.IsChecked == true);
        }

        private static void ModifyTheme(bool isDarkTheme)
        {
            PaletteHelper paletteHelper = new PaletteHelper();
            ITheme theme = paletteHelper.GetTheme();
            theme.SetBaseTheme(isDarkTheme ? new DarkTheme() : (IBaseTheme)new LightTheme());
            paletteHelper.SetTheme(theme);
        }

        private void HorizontalRes_PreviewMouseDown(object sender, MouseButtonEventArgs e) { }

        private void ScreenshotPopupBox_Open(object sender, RoutedEventArgs e)
        {
            ScreenshotPopupBox.IsPopupOpen = true;
        }

        private void HardwareDescription_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var key = e.Key;
            if (key == Key.Enter)
            {
                Keyboard.ClearFocus();
            }
        }

        private void TextBox_KeyEnterUpdate(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TextBox tBox = (TextBox)sender;
                DependencyProperty prop = TextBox.TextProperty;

                BindingExpression binding = BindingOperations.GetBindingExpression(tBox, prop);
                if (binding != null) { binding.UpdateSource(); }

                Keyboard.ClearFocus();
            }
        }
    }
}

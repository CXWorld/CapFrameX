using CapFrameX.EventAggregation.Messages;
using CapFrameX.ViewModel;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
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
            // UseShellExecute is required to open a URL in the default browser on .NET Core+
            _ = Process.Start(new ProcessStartInfo("https://github.com/DevTechProfile/CapFrameX#capframex") { UseShellExecute = true });
        }

        private void Donate_Button_Click(object sender, RoutedEventArgs e)
        {
            _ = Process.Start(new ProcessStartInfo("https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=A4VJPT9NB7G28&source=url") { UseShellExecute = true });
        }

        /// <summary>
        /// Exporting png pictures
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SaveScreenShotButton_Click(object sender, RoutedEventArgs e)
        {
            var viewModel = DataContext as ColorbarViewModel;
            string path = viewModel.ResolveDocumentsPath(viewModel.AppConfiguration.ScreenshotDirectory);
            var filename = string.Empty;
            var currentPageName = viewModel.CurrentPageName;
            var currentRecordInfo = viewModel.RecordInfo;

            try
            {

                if (currentPageName == "Analysis")
                {
                    var name = "CX" + "_" +
                          DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "_" + $"{currentRecordInfo.GameName}" + "_" + $"{currentRecordInfo.Comment}.png";

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

        // CapFrameX brand palette, carried over from the pre-5.x custom IBaseTheme
        // implementations (VS-code-like greys in dark mode). Keys follow the
        // MaterialDesign 5.x brush names; entries are (key, dark, light).
        private static readonly (string Key, string Dark, string Light)[] BrandPalette =
        {
            ("MaterialDesign.Brush.Card.Background", "#252526", "#f8f8f8"),
            ("MaterialDesign.Brush.ForegroundLight", "#89FFFFFF", "#89000000"),
            ("MaterialDesign.Brush.Header.Foreground", "#BCFFFFFF", "#BC000000"),
            ("MaterialDesign.Brush.TextBox.FilledBackground", "#2d2d30", "#ededed"),
            ("MaterialDesign.Brush.TextBox.HoverBackground", "#1FFFFFFF", "#14000000"),
            ("MaterialDesign.Brush.TextBox.DisabledBackground", "#0DFFFFFF", "#08000000"),
            ("MaterialDesign.Brush.TextBox.OutlineInactiveBorder", "#1AFFFFFF", "#0F000000"),
            ("MaterialDesign.Brush.DataGrid.RowHoverBackground", "#14FFFFFF", "#0A000000"),
            ("MaterialDesign.Brush.ToolBar.Background", "#FF212121", "#FFF5F5F5"),
            ("MaterialDesign.Brush.ToolBar.Item.Background", "#2196F3", "#2298f3"),
            ("MaterialDesign.Brush.ToolBar.Item.Foreground", "#FF616161", "#FF616161"),
            ("MaterialDesign.Brush.Button.FlatClick", "#19757575", "#FFDEDEDE"),
            ("MaterialDesign.Brush.Button.Ripple", "#FFB6B6B6", "#FFB6B6B6"),
            ("MaterialDesign.Brush.ToolTip.Background", "#eeeeee", "#757575"),
            ("MaterialDesign.Brush.Chip.Background", "#FF2E3C43", "#12000000"),
            ("MaterialDesign.Brush.SnackBar.Background", "#FFCDCDCD", "#FF323232"),
            ("MaterialDesign.Brush.SnackBar.MouseOver", "#FFB9B9BD", "#FF464642"),
            ("MaterialDesign.Brush.CheckBox.Disabled", "#FF647076", "#FFBDBDBD"),
            ("MaterialDesign.Brush.ValidationError", "#f44336", "#F44336"),
            // Not part of the 5.x obsolete-alias set, but still used by the views.
            ("MaterialDesignSelection", "#757575", "#FFDEDEDE"),

            // Rounded scrollbar restyle (templates in CapFrameX/Themes/CxScrollBar.xaml)
            ("Cx.Brush.ScrollBar.Thumb", "#5A5A60", "#C4C4CA"),
            ("Cx.Brush.ScrollBar.ThumbHover", "#8A8A92", "#9B9BA2"),
            ("Cx.Brush.ScrollBar.Glyph", "#9A9AA2", "#8A8A90"),
            ("Cx.Brush.ScrollBar.GlyphHover", "#E0E0E0", "#3A3A40"),
        };

        private static void ModifyTheme(bool isDarkTheme)
        {
            PaletteHelper paletteHelper = new PaletteHelper();
            Theme theme = paletteHelper.GetTheme();
            theme.SetBaseTheme(isDarkTheme ? BaseTheme.Dark : BaseTheme.Light);

            // Base surfaces and text via the Theme API so derived colors stay consistent.
            theme.Background = ParseColor(isDarkTheme ? "#2d2d30" : "#fafbfc");
            theme.Foreground = ParseColor(isDarkTheme ? "#DDFFFFFF" : "#DD000000");

            paletteHelper.SetTheme(theme);

            // Surface fine-tuning: override the brand-specific brushes after every
            // SetTheme call, since SetTheme rewrites the application resources.
            foreach (var (key, dark, light) in BrandPalette)
            {
                var brush = new System.Windows.Media.SolidColorBrush(ParseColor(isDarkTheme ? dark : light));
                brush.Freeze();
                Application.Current.Resources[key] = brush;
            }
        }

        private static System.Windows.Media.Color ParseColor(string value)
            => (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value);

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

        private void PopupBoxOpened(object sender, RoutedEventArgs e) { }
    }
}

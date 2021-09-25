using CapFrameX.Configuration;
using CapFrameX.Data;
using CapFrameX.Extensions;
using CapFrameX.Hotkey;
using CapFrameX.Overlay;
using CapFrameX.Statistics;
using CapFrameX.ViewModel;
using Microsoft.Extensions.Logging;
using Prism.Events;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CapFrameX.View
{
    /// <summary>
    /// Interaction logic for OverlayView.xaml
    /// </summary>
    public partial class OverlayView : UserControl
    {
        public static readonly DependencyProperty OverlayHotkeyProperty =
        DependencyProperty.Register(nameof(OverlayHotkey), typeof(CXHotkey), typeof(OverlayView),
         new FrameworkPropertyMetadata(default(CXHotkey), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty OverlayConfigHotkeyProperty =
        DependencyProperty.Register(nameof(OverlayConfigHotkey), typeof(CXHotkey), typeof(OverlayView),
        new FrameworkPropertyMetadata(default(CXHotkey), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public CXHotkey OverlayHotkey
        {
            get => (CXHotkey)GetValue(OverlayHotkeyProperty);
            set => SetValue(OverlayHotkeyProperty, value);
        }

        public CXHotkey OverlayConfigHotkey
        {
            get => (CXHotkey)GetValue(OverlayConfigHotkeyProperty);
            set => SetValue(OverlayConfigHotkeyProperty, value);
        }


        public OverlayView()
        {
            InitializeComponent();

            // Overlay hotkey
            try
            {
                var overlayHotkeyString = (DataContext as OverlayViewModel).AppConfiguration.OverlayHotKey;
                var keyStrings = overlayHotkeyString.Split('+');

                OverlayHotkey = CXHotkey.Create(keyStrings, Key.O, ModifierKeys.Alt);
            }
            catch { OverlayHotkey = new CXHotkey(Key.O, ModifierKeys.Alt); }

            // Overlay config hotkey
            try
            {
                var overlayConfigHotkeyString = (DataContext as OverlayViewModel).AppConfiguration.OverlayConfigHotKey;
                var keyStrings = overlayConfigHotkeyString.Split('+');

                OverlayConfigHotkey = CXHotkey.Create(keyStrings, Key.C, ModifierKeys.Alt);
            }
            catch { OverlayConfigHotkey = new CXHotkey(Key.C, ModifierKeys.Alt); }
        }

        private void OverlayHotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            var modifiers = Keyboard.Modifiers;
            var key = e.Key;

            if (key == Key.System)
            {
                key = e.SystemKey;
            }

            if (modifiers == ModifierKeys.None && key.IsEither(Key.Delete, Key.Back, Key.Escape))
            {
                OverlayHotkey = null;
                return;
            }

            if (key.IsEither(
                Key.LeftCtrl, Key.RightCtrl, Key.LeftAlt, Key.RightAlt,
                Key.LeftShift, Key.RightShift, Key.LWin, Key.RWin,
                Key.Clear, Key.OemClear, Key.Apps))
            {
                return;
            }

            OverlayHotkey = new CXHotkey(key, modifiers);
            var dataContext = DataContext as OverlayViewModel;
            dataContext.OverlayHotkeyString = OverlayHotkey.ToString();

            Keyboard.ClearFocus();
        }

        private void ConfigHotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            var modifiers = Keyboard.Modifiers;
            var key = e.Key;

            if (key == Key.System)
            {
                key = e.SystemKey;
            }

            if (modifiers == ModifierKeys.None && key.IsEither(Key.Delete, Key.Back, Key.Escape))
            {
                OverlayConfigHotkey = null;
                return;
            }

            if (key.IsEither(
                Key.LeftCtrl, Key.RightCtrl, Key.LeftAlt, Key.RightAlt,
                Key.LeftShift, Key.RightShift, Key.LWin, Key.RWin,
                Key.Clear, Key.OemClear, Key.Apps))
            {
                return;
            }

            OverlayConfigHotkey = new CXHotkey(key, modifiers);
            var dataContext = DataContext as OverlayViewModel;
            dataContext.OverlayConfigHotkeyString = OverlayConfigHotkey.ToString();

            Keyboard.ClearFocus();
        }

        private void OSDRefreshPeriodComboBox_MouseLeave(object sender, MouseEventArgs e)
        {
            Keyboard.ClearFocus();
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e) { }

        private void LostFocus_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var key = e.Key;

            if (key == Key.Enter)
            {
                OverlayItemDataGrid.Focus();
            }
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9.-]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void IntegerNumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            CopyButtonPopup.IsOpen = true;
        }
    }
}

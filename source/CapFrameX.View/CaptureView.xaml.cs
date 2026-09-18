using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CapFrameX.Extensions;
using CapFrameX.Hotkey;
using CapFrameX.ViewModel;

namespace CapFrameX.View
{
    /// <summary>
    /// Interaction logic for CaptureView.xaml
    /// </summary>
    public partial class CaptureView : UserControl
    {
        public static readonly DependencyProperty CaptureHotkeyProperty =
            DependencyProperty.Register(nameof(CaptureHotkey), typeof(CXHotkey), typeof(CaptureView),
             new FrameworkPropertyMetadata(default(CXHotkey), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public static readonly DependencyProperty ResetHistoryHotkeyProperty =
            DependencyProperty.Register(nameof(ResetHistoryHotkey), typeof(CXHotkey), typeof(CaptureView),
            new FrameworkPropertyMetadata(default(CXHotkey), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public CXHotkey CaptureHotkey
        {
            get => (CXHotkey)GetValue(CaptureHotkeyProperty);
            set => SetValue(CaptureHotkeyProperty, value);
        }

        public CXHotkey ResetHistoryHotkey
        {
            get => (CXHotkey)GetValue(ResetHistoryHotkeyProperty);
            set => SetValue(ResetHistoryHotkeyProperty, value);
        }

        public CaptureView()
        {
            InitializeComponent();

            // Capture Hotkey
            try
            {
                var captureHotkeyString = (DataContext as CaptureViewModel).AppConfiguration.CaptureHotKey;
                var keyStrings = captureHotkeyString.Split('+');

                CaptureHotkey = CXHotkey.Create(keyStrings, Key.F12);
            }
            catch { CaptureHotkey = new CXHotkey(Key.F12); }

            // Reset history hotkey
            try
            {
                var resetHistoryHotkeyString = (DataContext as CaptureViewModel).AppConfiguration.ResetHistoryHotkey;
                var keyStrings = resetHistoryHotkeyString.Split('+');

                ResetHistoryHotkey = CXHotkey.Create(keyStrings, Key.R, ModifierKeys.Control);
            }
            catch
            {
                ResetHistoryHotkey = new CXHotkey(Key.R, ModifierKeys.Control);
            }
        }

        private void CaptureHotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
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
                CaptureHotkey = null;
                (DataContext as CaptureViewModel).CaptureHotkeyString = string.Empty;
                Keyboard.ClearFocus();
                return;
            }

            if (key.IsEither(
                Key.LeftCtrl, Key.RightCtrl, Key.LeftAlt, Key.RightAlt,
                Key.LeftShift, Key.RightShift, Key.LWin, Key.RWin,
                Key.Clear, Key.OemClear, Key.Apps))
            {
                return;
            }

            CaptureHotkey = new CXHotkey(key, modifiers);
            var dataContext = DataContext as CaptureViewModel;
            dataContext.CaptureHotkeyString = CaptureHotkey.ToString();

            Keyboard.ClearFocus();
        }

        private bool CommitCaptureTime()
        {
            if (!(DataContext is CaptureViewModel viewModel) || !viewModel.AreButtonsActive)
                return false;

            var binding = CaptureTimeTextBox.GetBindingExpression(TextBox.TextProperty);
            binding?.UpdateSource();
            if (viewModel.CommitCaptureTime())
            {
                if (binding != null)
                    Validation.ClearInvalid(binding);
                return true;
            }

            const string message = "Enter a valid duration of 0 seconds or more (0 = no limit).";
            if (binding != null)
                Validation.MarkInvalid(binding, new ValidationError(new ExceptionValidationRule(), binding, message, null));
            CaptureTimeTextBox.ToolTip = message;
            return false;
        }

        private void CaptureTimeTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            CommitCaptureTime();
        }

        private void CaptureTimeTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                if (CommitCaptureTime())
                    Keyboard.ClearFocus();
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                (DataContext as CaptureViewModel)?.RestoreCaptureTime();
                Keyboard.ClearFocus();
            }
        }

        private void CaptureTimeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = (TextBox)sender;
            var binding = textBox.GetBindingExpression(TextBox.TextProperty);
            if (binding != null)
                Validation.ClearInvalid(binding);
            textBox.ToolTip = "0 = no limit. Press Enter or leave the field to save.";
        }

        private void CaptureTimeScopeButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CommitCaptureTime())
                return;

            CaptureTimeScopeButton.ContextMenu.PlacementTarget = CaptureTimeScopeButton;
            CaptureTimeScopeButton.ContextMenu.IsOpen = true;
        }

        private void CaptureTimeScopeButton_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            e.Handled = !CommitCaptureTime();
        }

        private void RememberGameCaptureTime_Click(object sender, RoutedEventArgs e)
        {
            // Run after the menu command and closing animation restore keyboard focus.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                CaptureTimeTextBox.Focus();
                CaptureTimeTextBox.SelectAll();
            }));
        }

        private void TextBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var textBox = sender as TextBox;
            textBox.Text = string.Empty;
        }

        private void TextBox_MouseLeave(object sender, MouseEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox.Text == string.Empty)
                textBox.Text = "0";

            Keyboard.ClearFocus();
        }

        private void Slider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            (DataContext as CaptureViewModel).OnSoundLevelChanged();
        }

        private void ResetChart_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // FrametimePlotView.ResetAllAxes();
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var key = e.Key;

            if (key == Key.Enter)
            {
                Keyboard.ClearFocus();
            }
        }

        private void LoggingPeriodComboBox_MouseLeave(object sender, MouseEventArgs e)
        {
            Keyboard.ClearFocus();
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9.]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.ExtentHeightChange != 0)
            {
                var scrollViewer = sender as ScrollViewer;
                scrollViewer?.ScrollToBottom();
            }
        }

        private void ResetHistoryHotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
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
                ResetHistoryHotkey = null;
                (DataContext as CaptureViewModel).ResetHistoryHotkeyString = string.Empty;
                Keyboard.ClearFocus();
                return;
            }

            if (key.IsEither(
                Key.LeftCtrl, Key.RightCtrl, Key.LeftAlt, Key.RightAlt,
                Key.LeftShift, Key.RightShift, Key.LWin, Key.RWin,
                Key.Clear, Key.OemClear, Key.Apps))
            {
                return;
            }

            ResetHistoryHotkey = new CXHotkey(key, modifiers);
            var dataContext = DataContext as CaptureViewModel;
            dataContext.ResetHistoryHotkeyString = ResetHistoryHotkey.ToString();

            Keyboard.ClearFocus();
        }

    }
}

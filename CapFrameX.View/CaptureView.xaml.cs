using CapFrameX.Configuration;
using CapFrameX.Extensions;
using CapFrameX.MVVM;
using CapFrameX.PresentMonInterface;
using CapFrameX.ViewModel;
using Prism.Events;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CapFrameX.View
{
    /// <summary>
    /// Interaction logic for CaptureView.xaml
    /// </summary>
    public partial class CaptureView : UserControl
    {
        public static readonly DependencyProperty CaptureHotkeyProperty =
            DependencyProperty.Register(nameof(CaptureHotkey), typeof(Hotkey), typeof(CaptureView),
             new FrameworkPropertyMetadata(default(Hotkey), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public Hotkey CaptureHotkey
        {
            get => (Hotkey)GetValue(CaptureHotkeyProperty);
            set => SetValue(CaptureHotkeyProperty, value);
        }

        public CaptureView()
        {
            InitializeComponent();

            try
            {
                var captureHotkeyString = (DataContext as CaptureViewModel).AppConfiguration.CaptureHotKey;
                var keyStrings = captureHotkeyString.Split('+');

                if (keyStrings.Length == 1)
                {
                    var key = (Key)Enum.Parse(typeof(Key), keyStrings[0], true);
                    CaptureHotkey = new Hotkey(key, ModifierKeys.None);
                }
                else if (keyStrings.Length == 2)
                {
                    var keyModifier = (ModifierKeys)Enum.Parse(typeof(ModifierKeys), keyStrings[0], true);
                    var key = (Key)Enum.Parse(typeof(Key), keyStrings[1], true);

                    CaptureHotkey = new Hotkey(key, keyModifier);
                }
                else if (keyStrings.Length == 3)
                {
                    var keyModifierA = (ModifierKeys)Enum.Parse(typeof(ModifierKeys), keyStrings[0], true);
                    var keyModifierB = (ModifierKeys)Enum.Parse(typeof(ModifierKeys), keyStrings[1], true);
                    var key = (Key)Enum.Parse(typeof(Key), keyStrings[2], true);

                    CaptureHotkey = new Hotkey(key, keyModifierA | keyModifierB);
                }
            }
            catch { CaptureHotkey = new Hotkey(); }

            // Design time!
            if (DesignerProperties.GetIsInDesignMode(this))
            {
                DataContext = new CaptureViewModel(new CapFrameXConfiguration(), new PresentMonCaptureService(), new EventAggregator());
            }
        }

        private void HotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
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
                return;
            }

            if (key.IsEither(
                Key.LeftCtrl, Key.RightCtrl, Key.LeftAlt, Key.RightAlt,
                Key.LeftShift, Key.RightShift, Key.LWin, Key.RWin,
                Key.Clear, Key.OemClear, Key.Apps))
            {
                return;
            }

            CaptureHotkey = new Hotkey(key, modifiers);
            var dataContext = DataContext as CaptureViewModel;
            dataContext.CaptureHotkeyString = CaptureHotkey.ToString();
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
        }
    }
}

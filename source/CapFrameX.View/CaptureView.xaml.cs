using CapFrameX.Configuration;
using CapFrameX.Data;
using CapFrameX.Extensions;
using CapFrameX.Hotkey;
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
            DependencyProperty.Register(nameof(CaptureHotkey), typeof(CaptureHotkey), typeof(CaptureView),
             new FrameworkPropertyMetadata(default(CaptureHotkey), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public CaptureHotkey CaptureHotkey
        {
            get => (CaptureHotkey)GetValue(CaptureHotkeyProperty);
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
                    CaptureHotkey = new CaptureHotkey(key, ModifierKeys.None);
                }
                else if (keyStrings.Length == 2)
                {
                    var keyModifier = (ModifierKeys)Enum.Parse(typeof(ModifierKeys), keyStrings[0], true);
                    var key = (Key)Enum.Parse(typeof(Key), keyStrings[1], true);

                    CaptureHotkey = new CaptureHotkey(key, keyModifier);
                }
                else if (keyStrings.Length == 3)
                {
                    var keyModifierA = (ModifierKeys)Enum.Parse(typeof(ModifierKeys), keyStrings[0], true);
                    var keyModifierB = (ModifierKeys)Enum.Parse(typeof(ModifierKeys), keyStrings[1], true);
                    var key = (Key)Enum.Parse(typeof(Key), keyStrings[2], true);

                    CaptureHotkey = new CaptureHotkey(key, keyModifierA | keyModifierB);
                }
            }
            catch { CaptureHotkey = new CaptureHotkey(); }

            // Design time!
            if (DesignerProperties.GetIsInDesignMode(this))
            {
                var appConfiguration = new CapFrameXConfiguration();
                DataContext = new CaptureViewModel(appConfiguration, new PresentMonCaptureService(), 
                    new EventAggregator(), new RecordDataProvider(new RecordDirectoryObserver(appConfiguration)));
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

            CaptureHotkey = new CaptureHotkey(key, modifiers);
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

        private void Slider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            (DataContext as CaptureViewModel).OnSoundLevelChanged();
        }

		private void ResetChart_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			// FrametimePlotView.ResetAllAxes();
		}
	}
}

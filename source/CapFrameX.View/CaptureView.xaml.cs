using CapFrameX.Configuration;
using CapFrameX.Data;
using CapFrameX.Extensions;
using CapFrameX.Hotkey;
using CapFrameX.Overlay;
using CapFrameX.PresentMonInterface;
using CapFrameX.ViewModel;
using Prism.Events;
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
			DependencyProperty.Register(nameof(CaptureHotkey), typeof(CXHotkey), typeof(CaptureView),
			 new FrameworkPropertyMetadata(default(CXHotkey), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

		public static readonly DependencyProperty OverlayHotkeyProperty =
			DependencyProperty.Register(nameof(OverlayHotkey), typeof(CXHotkey), typeof(CaptureView),
			 new FrameworkPropertyMetadata(default(CXHotkey), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

		public CXHotkey CaptureHotkey
		{
			get => (CXHotkey)GetValue(CaptureHotkeyProperty);
			set => SetValue(CaptureHotkeyProperty, value);
		}

		public CXHotkey OverlayHotkey
		{
			get => (CXHotkey)GetValue(OverlayHotkeyProperty);
			set => SetValue(OverlayHotkeyProperty, value);
		}

		public CaptureView()
		{
			InitializeComponent();

			try
			{
				var captureHotkeyString = (DataContext as CaptureViewModel).AppConfiguration.CaptureHotKey;
				var keyStrings = captureHotkeyString.Split('+');

				CaptureHotkey = CXHotkey.Create(keyStrings, Key.F12);
			}
			catch { CaptureHotkey = new CXHotkey(Key.F12); }

			try
			{
				var overlayHotkeyString = (DataContext as CaptureViewModel).AppConfiguration.OverlayHotKey;
				var keyStrings = overlayHotkeyString.Split('+');

				OverlayHotkey = CXHotkey.Create(keyStrings, Key.O, ModifierKeys.Alt);
			}
			catch { OverlayHotkey = new CXHotkey(Key.O, ModifierKeys.Alt); }

			// Design time!
			if (DesignerProperties.GetIsInDesignMode(this))
			{
				var appConfiguration = new CapFrameXConfiguration();
				DataContext = new CaptureViewModel(appConfiguration, new PresentMonCaptureService(),
					new EventAggregator(), new RecordDataProvider(new RecordDirectoryObserver(appConfiguration), appConfiguration), new OverlayService());
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
			var dataContext = DataContext as CaptureViewModel;
			dataContext.OverlayHotkeyString = OverlayHotkey.ToString();

			Keyboard.ClearFocus();
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

		private void ResetChart_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			// FrametimePlotView.ResetAllAxes();
		}

		private void CaptureTimeTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			var key = e.Key;

			if (key == Key.Enter)
			{
				Keyboard.ClearFocus();
			}
		}
	}
}

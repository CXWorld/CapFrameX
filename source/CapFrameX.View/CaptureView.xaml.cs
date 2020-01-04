using CapFrameX.Configuration;
using CapFrameX.Data;
using CapFrameX.Extensions;
using CapFrameX.Hotkey;
using CapFrameX.Overlay;
using CapFrameX.PresentMonInterface;
using CapFrameX.Statistics;
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

		public CXHotkey CaptureHotkey
		{
			get => (CXHotkey)GetValue(CaptureHotkeyProperty);
			set => SetValue(CaptureHotkeyProperty, value);
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

			// Design time!
			if (DesignerProperties.GetIsInDesignMode(this))
			{
				var appConfiguration = new CapFrameXConfiguration();
				var statisticProvider = new FrametimeStatisticProvider( appConfiguration);
				var recordDirectoryObserver = new RecordDirectoryObserver(appConfiguration);
				var recordDataProvider = new RecordDataProvider(recordDirectoryObserver, appConfiguration);
				var overlayEntryProvider = new OverlayEntryProvider();
				DataContext = new CaptureViewModel(appConfiguration, new PresentMonCaptureService(),
					new EventAggregator(), new RecordDataProvider(new RecordDirectoryObserver(appConfiguration), appConfiguration), 
					new OverlayService(statisticProvider, recordDataProvider, overlayEntryProvider, appConfiguration), statisticProvider);
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

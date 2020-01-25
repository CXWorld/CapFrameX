using CapFrameX.Configuration;
using CapFrameX.Data;
using CapFrameX.Extensions;
using CapFrameX.Hotkey;
using CapFrameX.Overlay;
using CapFrameX.Statistics;
using CapFrameX.ViewModel;
using Prism.Events;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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

		public static readonly DependencyProperty ResetHistoryHotkeyProperty =
		DependencyProperty.Register(nameof(ResetHistoryHotkey), typeof(CXHotkey), typeof(OverlayView),
		 new FrameworkPropertyMetadata(default(CXHotkey), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

		public CXHotkey OverlayHotkey
		{
			get => (CXHotkey)GetValue(OverlayHotkeyProperty);
			set => SetValue(OverlayHotkeyProperty, value);
		}

		public CXHotkey ResetHistoryHotkey
		{
			get => (CXHotkey)GetValue(ResetHistoryHotkeyProperty);
			set => SetValue(ResetHistoryHotkeyProperty, value);
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

			// Reset history hotkey
			try
			{
				var resetHistoryHotkeyString = (DataContext as OverlayViewModel).AppConfiguration.ResetHistoryHotkey;
				var keyStrings = resetHistoryHotkeyString.Split('+');

				ResetHistoryHotkey = CXHotkey.Create(keyStrings, Key.R, ModifierKeys.Control);
			}
			catch { ResetHistoryHotkey = new CXHotkey(Key.R, ModifierKeys.Control); }

			if (DesignerProperties.GetIsInDesignMode(this))
			{
				var appConfiguration = new CapFrameXConfiguration();
				var statisticProvider = new FrametimeStatisticProvider(appConfiguration);
				var recordDirectoryObserver = new RecordDirectoryObserver(appConfiguration);
				var recordDataProvider = new RecordDataProvider(recordDirectoryObserver, appConfiguration);
				var overlayEntryProvider = new OverlayEntryProvider();
				DataContext = new OverlayViewModel(new OverlayService(statisticProvider, recordDataProvider, overlayEntryProvider, appConfiguration),
					overlayEntryProvider, appConfiguration, new EventAggregator());
			}
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
			var dataContext = DataContext as OverlayViewModel;
			dataContext.ResetHistoryHotkeyString = ResetHistoryHotkey.ToString();

			Keyboard.ClearFocus();
		}
		private void OSDRefreshPeriodComboBox_MouseLeave(object sender, MouseEventArgs e)
		{
			Keyboard.ClearFocus();
		}

		private void OverlayItemDataGrid_MouseLeave(object sender, MouseEventArgs e)
		{
			(DataContext as OverlayViewModel).SelectedOverlayEntryIndex = -1;
			Keyboard.ClearFocus();
		}

		private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
		{
			Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
			e.Handled = true;
		}
	}
}

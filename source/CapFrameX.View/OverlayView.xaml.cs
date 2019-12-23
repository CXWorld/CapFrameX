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
	/// Interaction logic for OverlayView.xaml
	/// </summary>
	public partial class OverlayView : UserControl
	{
		public static readonly DependencyProperty OverlayHotkeyProperty =
		DependencyProperty.Register(nameof(OverlayHotkey), typeof(CXHotkey), typeof(OverlayView),
		 new FrameworkPropertyMetadata(default(CXHotkey), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

		public CXHotkey OverlayHotkey
		{
			get => (CXHotkey)GetValue(OverlayHotkeyProperty);
			set => SetValue(OverlayHotkeyProperty, value);
		}

		public OverlayView()
		{
			InitializeComponent();

			try
			{
				var overlayHotkeyString = (DataContext as OverlayViewModel).AppConfiguration.OverlayHotKey;
				var keyStrings = overlayHotkeyString.Split('+');

				OverlayHotkey = CXHotkey.Create(keyStrings, Key.O, ModifierKeys.Alt);
			}
			catch { OverlayHotkey = new CXHotkey(Key.O, ModifierKeys.Alt); }

			if (DesignerProperties.GetIsInDesignMode(this))
			{
				var appConfiguration = new CapFrameXConfiguration();
				DataContext = new OverlayViewModel(new OverlayService(), appConfiguration, new EventAggregator());
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
	}
}

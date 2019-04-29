using CapFrameX.Configuration;
using CapFrameX.Extensions;
using CapFrameX.MVVM;
using CapFrameX.PresentMonInterface;
using CapFrameX.ViewModel;
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
			CaptureHotkey = new Hotkey();

			// Design time!
			if (DesignerProperties.GetIsInDesignMode(this))
			{
				DataContext = new CaptureViewModel(new CapFrameXConfiguration(), new PresentMonCaptureService());
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
	}
}

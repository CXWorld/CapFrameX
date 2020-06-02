using CapFrameX.Configuration;
using CapFrameX.Contracts.MVVM;
using CapFrameX.MVVM;
using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CapFrameX
{
	/// <summary>
	/// Interaction logic for Shell.xaml
	/// </summary>
	public partial class Shell : Window, IShell
	{
		private bool _exitApp = false;

		public System.Windows.Controls.ContentControl GlobalScreenshotArea => ScreenshotArea;

		public Shell()
		{
			InitializeComponent();

			// Start tracking the Window instance.
			WindowStatServices.Tracker.Track(this);
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			if (!_exitApp)
			{
				e.Cancel = true;
				this.Hide();
			}
		}

		private void SystemTray_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
		{
			if (Visibility == Visibility.Visible)
			{
				Hide();
			}
			else this.ShowAndFocus();
		}

		private void ShowMainWindow_Click(object sender, RoutedEventArgs e)
		{
			this.ShowAndFocus();
		}

		private void Exit_Click(object sender, RoutedEventArgs e)
		{
			_exitApp = true;
			Close();
		}
	}
}

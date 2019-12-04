using CapFrameX.Configuration;
using CapFrameX.Contracts.MVVM;
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
		public System.Windows.Controls.ContentControl GlobalScreenshotArea => ScreenshotArea;

		public Shell()
		{
			InitializeComponent();

			// Start tracking the Window instance.
			WindowStatServices.Tracker.Track(this);
		}
	}
}

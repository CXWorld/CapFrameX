using CapFrameX.Data;
using Microsoft.Toolkit.Wpf.UI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace CapFrameX
{
	/// <summary>
	/// Window for CapFrameX Webservice Login via embedded Browser
	/// </summary>
	public partial class LoginWindow : Window
	{
		private readonly LoginManager _loginManager;

		public LoginWindow(LoginManager loginManager)
		{
			_loginManager = loginManager;
			InitializeComponent();
		}

		private async void OnBrowserInitialized(object sender, EventArgs e)
		{
			await _loginManager.HandleRedirect(async (url) =>
				{
					await Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() =>
					{
						var browser = (WebView)sender;
						browser.Navigate(url);
					}));
				});
			_loginManager.EnableTokenRefresh(new CancellationToken());
			Close();
		}
	}
}

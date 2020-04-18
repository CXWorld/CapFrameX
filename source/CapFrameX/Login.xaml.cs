using CapFrameX.Data;
using CapFrameX.EventAggregation.Messages;
using CefSharp;
using CefSharp.Wpf;
using Prism.Events;
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
		private readonly PubSubEvent<AppMessages.LoginState> _loginEvent;

		public LoginWindow(LoginManager loginManager)
		{
			_loginManager = loginManager;
			InitializeComponent();
		}

		private async void OnBrowserInitialized(object sender, EventArgs e)
		{
			await SetupBrowser();

			Cef.GetGlobalCookieManager().DeleteCookies();
			await _loginManager.HandleRedirect(async (url) =>
				{
					await Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() =>
					{
						var browser = (ChromiumWebBrowser)sender;
						browser.Load(url);
					}));
				});
			Close();
		}

		private async Task SetupBrowser()
		{
			if (!Cef.IsInitialized)
			{
				Cef.Initialize(new CefSettings()
				{
					UserAgent = "Chrome"
				});
			}

			do
			{
				await Task.Delay(200);
			} while (!Cef.IsInitialized);
		}
	}
}

using CapFrameX.Data;
using CapFrameX.EventAggregation.Messages;
using CefSharp;
using CefSharp.Handler;
using CefSharp.Wpf;
using Microsoft.Extensions.Logging;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
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
		private readonly ILogger<LoginWindow> _logger;

		public LoginWindow(LoginManager loginManager, ILogger<LoginWindow> logger)
		{
			_loginManager = loginManager;
			_logger = logger;
			InitializeComponent();
		}

		private async void OnBrowserInitialized(object sender, EventArgs evt)
		{
			try
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
			} catch (Exception ecx)
			{
				_logger.LogError(ecx, "Login Failed");
				Grid.Children.Add(new TextBlock() { 
					Text = "Sorry, something failed :(" + Environment.NewLine + "See Logfile for more information"
				});
			}
		}

		private async Task SetupBrowser()
		{
			Wb.RequestHandler = new CustomRequestHandler();
		}
	}

	class CustomRequestHandler: RequestHandler
	{
		protected override IResourceRequestHandler GetResourceRequestHandler(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IRequest request, bool isNavigation, bool isDownload, string requestInitiator, ref bool disableDefaultHandling)
		{
			return new CustomResourceRequestHandler();
		}
	}

	class CustomResourceRequestHandler: ResourceRequestHandler
    {

        protected override CefReturnValue OnBeforeResourceLoad(IWebBrowser browserControl, IBrowser browser, IFrame frame, IRequest request, IRequestCallback callback)
        {
            var headers = request.Headers;
            headers["User-Agent"] = "Chrome";
            request.Headers = headers;

            return CefReturnValue.Continue;
        }
    }
}

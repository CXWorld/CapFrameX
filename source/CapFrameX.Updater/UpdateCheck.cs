using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.UpdateCheck;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;

namespace CapFrameX.Updater
{
	public class UpdateCheck: IUpdateCheck
	{
		private readonly IAppVersionProvider _appVersionProvider;
		private readonly IWebVersionProvider _webVersionProvider;

		public UpdateCheck(IAppVersionProvider appVersionProvider, IWebVersionProvider webVersionProvider)
		{
			_appVersionProvider = appVersionProvider;
			_webVersionProvider = webVersionProvider;
		}

		public bool IsUpdateAvailable()
		{
			return _appVersionProvider.GetAppVersion() < _webVersionProvider.GetWebVersion();
		}

	}
}

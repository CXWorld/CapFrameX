using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.UpdateCheck;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

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

		public async Task<(bool, Version)> IsUpdateAvailable()
		{
			var versionAvailable = await _webVersionProvider.GetWebVersionAsync();
			var isUpdateAvailable = _appVersionProvider.GetAppVersion() < versionAvailable;
			return  (isUpdateAvailable, versionAvailable);
		}

	}
}

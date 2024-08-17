using CapFrameX.Contracts.Data;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Data
{
	public class WebVersionProvider : IWebVersionProvider
	{
		private readonly string _webVersionCheckUrl;
		private Version _version;

		public WebVersionProvider(string updateCheckUrl = "https://raw.githubusercontent.com/DevTechProfile/CapFrameX/master/version/Version.txt")
		{
			_webVersionCheckUrl = updateCheckUrl;
		}

		public Version GetWebVersion()
		{
			return GetWebVersionAsync().GetAwaiter().GetResult();
		}

		public async Task<Version> GetWebVersionAsync()
		{
			try
			{
				if (_version != null)
				{
					return _version;
				}
				using (var client = new HttpClient() { Timeout = TimeSpan.FromSeconds(2)})
				{
					var versionString = await client.GetStringAsync(_webVersionCheckUrl);
					_version = new Version(versionString);
					return _version;
				}
			}
			catch
			{
				return null;
			}
		}
	}
}

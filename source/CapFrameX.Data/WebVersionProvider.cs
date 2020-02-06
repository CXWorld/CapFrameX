using CapFrameX.Contracts.Data;
using System;
using System.Linq;
using System.Net;
using System.Text;

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
			if(_version != null)
			{
				return _version;
			}
			try
			{
				ServicePointManager.Expect100Continue = true;
				ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
				// Use SecurityProtocolType.Ssl3 if needed for compatibility reasons

				WebClient wc = new WebClient();
				byte[] raw = wc.DownloadData(_webVersionCheckUrl);

				if (raw == null || !raw.Any())
				{
					return null;
				}

				string webVersionString = Encoding.UTF8.GetString(raw);
				_version = new Version(webVersionString);
				return _version;
			}
			catch
			{
				return null;
			}
		}
	}
}

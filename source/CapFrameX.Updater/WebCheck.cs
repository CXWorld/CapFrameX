using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;

namespace CapFrameX.Updater
{
	public class WebCheck
	{
		public static bool IsCXUpdateAvailable(string url, Func<string> getGetCurrentVersionString = null)
		{
			try
			{
				if (getGetCurrentVersionString == null)
					getGetCurrentVersionString = GetCurrentVersionString;

				Version webVersion = GetWebVersion(url);
				Version currentVersion = new Version(getGetCurrentVersionString.Invoke());

				return webVersion > currentVersion;
			}
			catch { return false; }
		}

		public static Version GetWebVersion(string url)
		{
			try
			{
				// using System.Net;
				ServicePointManager.Expect100Continue = true;
				ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
				// Use SecurityProtocolType.Ssl3 if needed for compatibility reasons

				WebClient wc = new WebClient();
				// dev branch: "https://raw.githubusercontent.com/DevTechProfile/CapFrameX/develop/feature/rtss_client_implementation/version/Version.txt
				// master branch: get by raw button
				byte[] raw = wc.DownloadData(url);

				if (raw == null || !raw.Any())
					return null;

				string webVersionString = Encoding.UTF8.GetString(raw);
				return new Version(webVersionString);
			}
			catch { return null; }
		}

		public static string GetCurrentVersionString()
		{
			Assembly assembly = GetAssemblyByName("CapFrameX");
			var fileVersion = FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion;

			var numbers = fileVersion.Split('.');
			return $"{numbers[0]}.{numbers[1]}.{numbers[2]}";
		}

		private static Assembly GetAssemblyByName(string name)
		{
			return AppDomain.CurrentDomain.GetAssemblies().
				   SingleOrDefault(assembly => assembly.GetName().Name == name);
		}
	}
}

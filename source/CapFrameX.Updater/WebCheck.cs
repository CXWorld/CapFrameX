using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CapFrameX.Updater
{
	public class WebCheck
	{
		public static bool IsCXUpdateAvailable()
		{
			System.Net.WebClient wc = new System.Net.WebClient();
			byte[] raw = wc.DownloadData("https://github.com/DevTechProfile/CapFrameX/tree/master/version/Version.txt");

			if (raw == null || !raw.Any())
				return false;

			string webVersionString = Encoding.UTF8.GetString(raw);
			Version webVersion = new Version(webVersionString);
			Version currentVersion = new Version(GetCurrentVersionString());

			return webVersion > currentVersion;
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

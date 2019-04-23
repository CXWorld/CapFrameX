using CapFrameX.Contracts.PresentMonInterface;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CapFrameX.PresentMonInterface
{
	public static class CaptureServiceConfiguration
	{
		private static readonly string _ignoreListFileName = Path.Combine("PresentMon", "ProcessIgnoreList.txt");

		public static IServiceStartInfo GetServiceStartInfo(string arguments)
		{
			var startInfo = new PresentMonStartInfo
			{
				FileName = Path.Combine("PresentMon", "PresentMon64-1.3.1.exe"),
				Arguments = arguments,
				CreateNoWindow = true,
				RunWithAdminRights = true,
				RedirectStandardOutput = true,
				UseShellExecute = false
			};

			return startInfo;
		}

		public static HashSet<string> GetProcessIgnoreList()
		{
			string[] processes = File.ReadAllLines(_ignoreListFileName);
			return new HashSet<string>(processes);
		}

		public static void AddProcessToIgnoreList(string processName)
		{
			List<string> processes = File.ReadAllLines(_ignoreListFileName).ToList();
			processes.Add(processName);
			var orderedList = processes.OrderBy(name => name);

			File.WriteAllLines(_ignoreListFileName, orderedList);
		}

		public static void RemoveProcessFromIgnoreList(string processName)
		{
			List<string> processes = File.ReadAllLines(_ignoreListFileName).ToList();
			processes.Remove(processName);

			File.WriteAllLines(_ignoreListFileName, processes);
		}
	}
}

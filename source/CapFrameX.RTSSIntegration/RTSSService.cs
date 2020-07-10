using CapFrameX.Contracts.RTSS;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;

namespace CapFrameX.RTSSIntegration
{
    public class RTSSService: RTSSCSharpWrapper, IRTSSService
    {
		private const string RTSSPROCESSNAME = "RTSS";

		private static ILogger<RTSSService> _logger;

		public ISubject<uint> ProcessIdStream { get; }

		public RTSSService(ILogger<RTSSService> logger): base(ExceptionAction)
        {
            _logger = logger;
			ProcessIdStream = new BehaviorSubject<uint>(default);
		}

		public bool IsRTSSInstalled()
        {
			return !string.IsNullOrEmpty(GetRTSSFullPath());
		}

		public void CheckRTSSRunningAndRefresh()
		{
			var processes = Process.GetProcessesByName(RTSSPROCESSNAME);
			if (!processes.Any())
			{
				try
				{
					Process proc = new Process();
					proc.StartInfo.FileName = Path.Combine(GetRTSSFullPath());
					proc.StartInfo.UseShellExecute = false;
					proc.StartInfo.Verb = "runas";
					proc.Start();
				}
				catch (Exception ex) { _logger.LogError(ex, "Error while starting RTSS process"); }
			}
			Refresh();
		}

		private static void ExceptionAction(Exception ex)
        {
            _logger.LogError(ex, "Exception thrown in RTSSCSharpWrapper");
        }

		private string GetRTSSFullPath()
		{
			string installPath = string.Empty;

			try
			{
				// SOFTWARE\WOW6432Node\Unwinder\RTSS
				using (RegistryKey key = Registry.LocalMachine.OpenSubKey("Software\\WOW6432Node\\Unwinder\\RTSS"))
				{
					if (key != null)
					{
						object o = key.GetValue("InstallPath");
						if (o != null)
						{
							installPath = o as string;  //"as" because it's REG_SZ...otherwise ToString() might be safe(r)
						}
					}
				}

				// SOFTWARE\Unwinder\RTSS
				if (string.IsNullOrWhiteSpace(installPath))
				{
					using (RegistryKey key = Registry.LocalMachine.OpenSubKey("Software\\Unwinder\\RTSS"))
					{
						if (key != null)
						{
							object o = key.GetValue("InstallPath");
							if (o != null)
							{
								installPath = o as string;  //"as" because it's REG_SZ...otherwise ToString() might be safe(r)
							}
						}
					}
				}
			}
			catch (Exception)
			{
				throw;
			}

			return installPath;
		}
    }
}

using CapFrameX.Contracts.Overlay;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;

namespace CapFrameX.Overlay
{
	public class OverlayService : RTSSCSharpWrapper, IOverlayService
	{
		private IDisposable _disposableHeartBeat;

		public double RefreshPeriod { get; }

		public Subject<bool> IsOverlayActiveStream { get; }

		public OverlayService() : base()
		{
			// default 1 second
			RefreshPeriod = 1;
			IsOverlayActiveStream = new Subject<bool>();
		}

		public void ShowOverlay()
		{
			_disposableHeartBeat = GetOverlayRefreshHeartBeat();
		}

		public void HideOverlay()
		{
			_disposableHeartBeat?.Dispose();
			ReleaseOSD();
		}

		private void CheckRTSSRunningAndRefresh()
		{
			var processes = Process.GetProcessesByName("RTSS");

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
				catch { }
			}

			Refresh();
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
						Object o = key.GetValue("InstallPath");
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
							Object o = key.GetValue("InstallPath");
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

		private IDisposable GetOverlayRefreshHeartBeat()
		{
			var context = SynchronizationContext.Current;
			return Observable.Generate(0, // dummy initialState
										x => true, // dummy condition
										x => x, // dummy iterate
										x => x, // dummy resultSelector
										x => TimeSpan.FromSeconds(RefreshPeriod))
										.ObserveOn(context)
										.SubscribeOn(context)
										.Subscribe(x => CheckRTSSRunningAndRefresh());
		}
	}
}

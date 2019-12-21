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

		/// <summary>
		/// Refresh period in milliseconds
		/// </summary>
		public int RefreshPeriod { get; private set; }

		public Subject<bool> IsOverlayActiveStream { get; }

		public OverlayService() : base()
		{
			// default 500 milliseconds
			RefreshPeriod = 500;
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

		public void UpdateRefreshRate(int milliSeconds)
		{
			RefreshPeriod = milliSeconds;
			_disposableHeartBeat?.Dispose();
			_disposableHeartBeat = GetOverlayRefreshHeartBeat();
		}

		public void StartCountdown(int seconds)
		{
			IObservable<long> obs = Extensions.ObservableExtensions.CountDown(seconds);
			SetShowCaptureTimer(true);
			obs.Subscribe(t =>
			{
				Console.WriteLine("Current counter: {0}", t);
				SetCountdownCounter((int)t);

				if (t == 0)
					OnCountDownFinished();
			});
		}

		private void SetCountdownCounter(int t)
		{
			SetCaptureTimerValue((uint)t);
		}

		private void OnCountDownFinished()
		{
			SetShowCaptureTimer(false);
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

		private IDisposable GetOverlayRefreshHeartBeat()
		{
			var context = SynchronizationContext.Current;
			return Observable.Generate(0, // dummy initialState
										x => true, // dummy condition
										x => x, // dummy iterate
										x => x, // dummy resultSelector
										x => TimeSpan.FromMilliseconds(RefreshPeriod))
										.ObserveOn(context)
										.SubscribeOn(context)
										.Subscribe(x => CheckRTSSRunningAndRefresh());
		}
	}
}

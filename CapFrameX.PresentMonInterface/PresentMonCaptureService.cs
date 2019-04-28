using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using CapFrameX.Contracts.PresentMonInterface;

namespace CapFrameX.PresentMonInterface
{
	public class PresentMonCaptureService : ICaptureService
	{
		private readonly ISubject<string> _outputStream;

		public IObservable<string> RedirectedStandardOutputStream => _outputStream.AsObservable();

		public PresentMonCaptureService()
		{
			_outputStream = new Subject<string>();
		}

		public bool StartCaptureService(IServiceStartInfo startinfo)
		{
			TryKillPresentMon();

			var process = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = startinfo.FileName,
					Arguments = startinfo.Arguments,
					UseShellExecute = startinfo.UseShellExecute,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = startinfo.CreateNoWindow,
					Verb = "runas",
				}
			};

			Task.Factory.StartNew(() =>
			{
				process.EnableRaisingEvents = true;
				process.Start();
				_outputStream.OnNext("Started process...");
				process.ErrorDataReceived += (sender, e) => _outputStream.OnNext(e.Data);
				process.OutputDataReceived += (sender, e) => _outputStream.OnNext(e.Data);
				process.BeginOutputReadLine();
				process.BeginErrorReadLine();
				process.WaitForExit();
			});

			return true;
		}

		public bool StopCaptureService()
		{
			try
			{
				TryKillPresentMon();
				return true;
			}
			catch { return false; }

		}

		public IEnumerable<string> GetAllFilteredProcesses(HashSet<string> filter)
		{
			var allRunningProcesses = Process.GetProcesses().Where(x => x.MainWindowHandle != IntPtr.Zero);
			return allRunningProcesses.Select(process => process.ProcessName)
				.Where(processName => !filter.Contains(processName));
		}

		private void TryKillPresentMon()
		{
			try
			{
				Process[] proc = Process.GetProcessesByName("PresentMon64-1.3.1");
				if (proc.Any())
					proc[0].Kill();
			}
			catch { }
		}
	}
}

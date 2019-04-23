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
	public class PresentMonCaptureService : ICaptureService, IDisposable
	{
		private readonly ISubject<string> _outputStream;

		private Process _process;

		public IObservable<string> RedirectedStandardOutputStream => _outputStream.AsObservable();

		public PresentMonCaptureService()
		{
			_outputStream = new Subject<string>();
		}

		public bool StartCaptureService(IServiceStartInfo startinfo)
		{
			TryKillPresentMon();

			_process = new Process
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
				_process.EnableRaisingEvents = true;
				_process.Start();
				_outputStream.OnNext("Started process...");
				_process.ErrorDataReceived += (sender, e) => _outputStream.OnNext(e.Data);
				_process.OutputDataReceived += (sender, e) => _outputStream.OnNext(e.Data);
				_process.BeginOutputReadLine();
				_process.BeginErrorReadLine();
				_process.WaitForExit();
			});

			return true;
		}

		public bool StopCaptureService()
		{
			try
			{
				_process?.Kill();
				return true;
			}
			catch { return false; }
			
		}

		public IEnumerable<string> GetAllFilteredProcesses(HashSet<string> filter)
		{
			var allRunningProcesses = Process.GetProcesses();
			return allRunningProcesses.Select(process => process.ProcessName)
				.Where(processName => !filter.Contains(processName));
		}

		public void Dispose()
		{
			try
			{
				_process?.Kill();
			}
			catch { }
		}

		private void TryKillPresentMon()
		{
			try
			{
				Process[] proc = Process.GetProcessesByName("PresentMon64-1.3.1");
				proc[0].Kill();
			}
			catch{ }
		}
	}
}

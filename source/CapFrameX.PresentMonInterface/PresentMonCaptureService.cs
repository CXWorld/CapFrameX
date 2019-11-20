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
		private readonly ISubject<string> _outputErrorStream;
		private readonly ISubject<string> _outputDataStream;
		private readonly object _listLock = new object();

		private HashSet<string> _presentMonProcesses;
		private bool _isUpdating;
		private IDisposable _hearBeatDisposable;
		private IDisposable _processNameDisposable;

		public IObservable<string> RedirectedOutputDataStream => _outputDataStream.AsObservable();
		public IObservable<string> RedirectedOutputErrorStream => _outputErrorStream.AsObservable();
		public Subject<bool> IsCaptureModeActiveStream { get; }

		public PresentMonCaptureService()
		{
			_outputDataStream = new Subject<string>();
			_outputErrorStream = new Subject<string>();
			IsCaptureModeActiveStream = new Subject<bool>();
			_presentMonProcesses = new HashSet<string>();
		}

		public bool StartCaptureService(IServiceStartInfo startinfo)
		{
			if (!CaptureServiceInfo.IsCompatibleWithRunningOS)
			{
				return false;
			}

			SubscribeToPresentMonCapturedProcesses();
			TryKillPresentMon();

			Task.Factory.StartNew(() =>
			{
				Process process = new Process
				{
					StartInfo = new ProcessStartInfo
					{
						FileName = startinfo.FileName,
						Arguments = startinfo.Arguments,
						UseShellExecute = startinfo.UseShellExecute,
						RedirectStandardOutput = true,
						RedirectStandardError = true,
						RedirectStandardInput = true, // is it a MUST??
						CreateNoWindow = startinfo.CreateNoWindow,
						Verb = "runas",
					}
				};

				process.PriorityClass = ProcessPriorityClass.AboveNormal;
				process.EnableRaisingEvents = true;
				process.OutputDataReceived += (sender, e) => _outputDataStream.OnNext(e.Data);
				process.ErrorDataReceived += (sender, e) => _outputErrorStream.OnNext(e.Data);

				process.Start();
				_outputDataStream.OnNext("Capture service started...");
				process.BeginOutputReadLine();
				process.BeginErrorReadLine();
			});

			return true;
		}

		public bool StopCaptureService()
		{
			_hearBeatDisposable?.Dispose();
			_processNameDisposable?.Dispose();

			try
			{
				lock (_listLock)
					_presentMonProcesses?.Clear();

				TryKillPresentMon();
				return true;
			}
			catch { return false; }

		}

		public IEnumerable<string> GetAllFilteredProcesses(HashSet<string> filter)
		{
			lock (_listLock)
				return _presentMonProcesses?.Where(processName => !filter.Contains(processName));
		}

		public static void TryKillPresentMon()
		{
			try
			{
				var proc = Process.GetProcessesByName("PresentMon64-1.5.2");
				if (proc.Any())
				{
					proc[0].Kill();
				}
			}
			catch { }
		}

		private void SubscribeToPresentMonCapturedProcesses()
		{
			_hearBeatDisposable = Observable.Generate(0, // dummy initialState
										x => true, // dummy condition
										x => x, // dummy iterate
										x => x, // dummy resultSelector
										x => TimeSpan.FromSeconds(1))
										.Subscribe(x => UpdateProcessToCaptureList());

			_processNameDisposable = _outputDataStream.Skip(2).Where(dataLine => _isUpdating == false).Subscribe(dataLine =>
			{
				if (string.IsNullOrWhiteSpace(dataLine))
					return;

				int index = dataLine.IndexOf(".exe");

				if (index > 0)
				{
					var processName = dataLine.Substring(0, index);

					if (!_presentMonProcesses.Contains(processName))
					{
						lock (_listLock)
							_presentMonProcesses.Add(processName);
					}
				}
			});
		}

		private void UpdateProcessToCaptureList()
		{
			_isUpdating = true;
			var updatedList = new List<string>();

			lock (_listLock)
			{
				foreach (var process in _presentMonProcesses)
				{
					var proc = Process.GetProcessesByName(process);
					if (proc.Any())
					{
						updatedList.Add(process);
					}
				}
			}

			_presentMonProcesses = new HashSet<string>(updatedList);
			_isUpdating = false;
		}
	}
}

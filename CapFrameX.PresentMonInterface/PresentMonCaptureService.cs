using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CapFrameX.Contracts.PresentMonInterface;

namespace CapFrameX.PresentMonInterface
{
    public class PresentMonCaptureService : ICaptureService
    {
        private readonly ISubject<string> _outputErrorStream;
        private readonly ISubject<string> _outputDataStream;

        private HashSet<string> _presentMonProcesses;
        private bool _isUpdating;

        public IObservable<string> RedirectedOutputDataStream => _outputDataStream.AsObservable();
        public IObservable<string> RedirectedOutputErrorStream => _outputErrorStream.AsObservable();

        public PresentMonCaptureService()
        {
            _outputDataStream = new Subject<string>();
            _outputErrorStream = new Subject<string>();
            _presentMonProcesses = new HashSet<string>();
            SubscribeToPresentMonCapturedProcesses();
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

            process.EnableRaisingEvents = true;
            process.Start();
            _outputDataStream.OnNext("Capture service started...");
            process.OutputDataReceived += (sender, e) => _outputDataStream.OnNext(e.Data);
            process.ErrorDataReceived += (sender, e) => _outputErrorStream.OnNext(e.Data);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return true;
        }

        public bool StopCaptureService()
        {
            try
            {
                _presentMonProcesses.Clear();
                TryKillPresentMon();
                return true;
            }
            catch { return false; }

        }

        public IEnumerable<string> GetAllFilteredProcesses(HashSet<string> filter)
        {
            return _presentMonProcesses?.Where(processName => !filter.Contains(processName));
        }

        public static void TryKillPresentMon()
        {
            try
            {
                var proc = Process.GetProcessesByName("PresentMon64-1.4.0");
                if (proc.Any())
                {
                    proc[0].Kill();
                }
            }
            catch { }
        }

        private void SubscribeToPresentMonCapturedProcesses()
        {
            Observable.Generate(0, // dummy initialState
                                        x => true, // dummy condition
                                        x => x, // dummy iterate
                                        x => x, // dummy resultSelector
                                        x => TimeSpan.FromSeconds(1))
                                        .Subscribe(x => UpdateProcessToCaptureList());

            _outputDataStream.Skip(2).Where(dataLine => _isUpdating == false).Subscribe(dataLine =>
            {
                if (string.IsNullOrWhiteSpace(dataLine))
                    return;

                int index = dataLine.IndexOf(".exe");

                if (index > 0)
                {
                    var processName = dataLine.Substring(0, index);

                    if (!_presentMonProcesses.Contains(processName))
                    {
                        _presentMonProcesses.Add(processName);
                    }
                }
            });
        }

        private void UpdateProcessToCaptureList()
        {
            _isUpdating = true;
            var updatedList = new List<string>();

            foreach (var process in _presentMonProcesses)
            {
                var proc = Process.GetProcessesByName(process);
                if (proc.Any())
                {
                    updatedList.Add(process);
                }
            }

            _presentMonProcesses = new HashSet<string>(updatedList);
            _isUpdating = false;
        }
    }
}

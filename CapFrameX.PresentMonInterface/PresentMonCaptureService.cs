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

        public IObservable<string> RedirectedOutputDataStream => _outputDataStream.AsObservable();
        public IObservable<string> RedirectedOutputErrorStream => _outputErrorStream.AsObservable();

        public PresentMonCaptureService()
        {
            _outputDataStream = new Subject<string>();
            _outputErrorStream = new Subject<string>();
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
                var proc = Process.GetProcessesByName("PresentMon64-1.3.1");
                if (proc.Any())
                {
                    proc[0].Kill();
                }
            }
            catch { }
        }
    }
}

using CapFrameX.Contracts.PresentMonInterface;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace CapFrameX.PresentMonInterface
{
    public static class CaptureServiceConfiguration
    {
        private static readonly string _ignoreListFileName
            = Path.Combine("PresentMon", "ProcessIgnoreList.txt");

        private static readonly string _ignoreLiveListFilename =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                @"CapFrameX\Resources\ProcessIgnoreList.txt");

        private static string[] _processes;

        public static IServiceStartInfo GetServiceStartInfo(string arguments)
        {
            var startInfo = new PresentMonStartInfo
            {
                FileName = Path.Combine("PresentMon", "PresentMon64-1.5.2.exe"),
                Arguments = arguments,
                CreateNoWindow = true,
                RunWithAdminRights = true,
                RedirectStandardOutput = false,
                UseShellExecute = false
            };

            return startInfo;
        }

        public static HashSet<string> GetProcessIgnoreList()
        {
            var ignoreList = new HashSet<string>();

            try
            {
                if (!File.Exists(_ignoreLiveListFilename))
                {
                    Directory.CreateDirectory(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                            @"CapFrameX\Resources"));
                    File.Copy(_ignoreListFileName, _ignoreLiveListFilename);
                }

                if (_processes == null || !_processes.Any())
                    _processes = File.ReadAllLines(_ignoreLiveListFilename);

                ignoreList = new HashSet<string>(_processes.Where(process => !string.IsNullOrEmpty(process)));
            }
            catch
            {
                return ignoreList;
            }

            return ignoreList;
        }

        public static void AddProcessToIgnoreList(string processName)
        {
            if (_processes == null || !_processes.Any())
                _processes = File.ReadAllLines(_ignoreLiveListFilename);

            List<string> processes = _processes.ToList();
            processes.Add(processName);
            var orderedList = processes.OrderBy(name => name);
            _processes = orderedList.ToArray();

            File.WriteAllLines(_ignoreLiveListFilename, orderedList);
        }

        public static void RemoveProcessFromIgnoreList(string processName)
        {
            if (_processes == null || !_processes.Any())
                _processes = File.ReadAllLines(_ignoreLiveListFilename);

            List<string> processes = _processes.ToList();
            if (processes.Contains(processName))
                processes.Remove(processName);
            _processes = processes.ToArray();

            File.WriteAllLines(_ignoreLiveListFilename, processes);
        }

        public static string GetCaptureFilename(string processName)
        {
            DateTime now = DateTime.Now;
            string dateTimeFormat = $"{now.Year}-{now.Month.ToString("d2")}-" +
				$"{now.Day.ToString("d2")}T{now.Hour}{now.Minute}{now.Second}";
            return $"CapFrameX-{processName}.exe-{dateTimeFormat}.json";
        }
    }
}

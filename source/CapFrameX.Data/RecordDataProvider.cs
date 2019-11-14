using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.OcatInterface;
using CapFrameX.PresentMonInterface;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace CapFrameX.Data
{
    public class RecordDataProvider : IRecordDataProvider
    {
        private static readonly string FILE_HEADER = 
			$"Application,ProcessID,SwapChainAddress,Runtime,SyncInterval,PresentFlags," +
			$"AllowsTearing,PresentMode,WasBatched,DwmNotified,Dropped,TimeInSeconds,MsBetweenPresents," +
			$"MsBetweenDisplayChange,MsInPresentAPI,MsUntilRenderComplete,MsUntilDisplayed,QPCTime";

        private static readonly string _matchingNameInitialFilename
            = Path.Combine("NameMatching", "ProcessGameNameMatchingList.txt");

        private static readonly string _matchingNameLiveFilename =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                @"CapFrameX\Resources\ProcessGameNameMatchingList.txt");

        private readonly IRecordDirectoryObserver _recordObserver;

        private Dictionary<string, string> _processGameMatchingDictionary = new Dictionary<string, string>();

        public RecordDataProvider(IRecordDirectoryObserver recordObserver)
        {
            _recordObserver = recordObserver;

            try
            {
                if (!File.Exists(_matchingNameLiveFilename))
                {
                    Directory.CreateDirectory(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                            @"CapFrameX\Resources"));
                    File.Copy(_matchingNameInitialFilename, _matchingNameLiveFilename);
                }
            }
            catch { }
        }

        public IFileRecordInfo GetFileRecordInfo(FileInfo fileInfo)
        {
            var fileRecordInfo = FileRecordInfo.Create(fileInfo);
            if (fileRecordInfo == null)
                return null;

            fileRecordInfo.GameName = GetGameFromMatchingList(fileRecordInfo.ProcessName);
            return fileRecordInfo;
        }

        public IList<IFileRecordInfo> GetFileRecordInfoList()
        {
            return _recordObserver.GetAllRecordFileInfo()
                .Select(fileInfo => GetFileRecordInfo(fileInfo))
				.Where(fileRecordInfo => fileRecordInfo != null)
				.OrderBy(fileRecordInfo => fileRecordInfo.GameName)
				.ToList();
		}

        public void SavePresentData(IList<string> recordLines, string filePath, string processName, int captureTime)
        {
            var csv = new StringBuilder();
            var datetime = DateTime.Now;

            var processNameAdjusted = processName.Contains(".exe") ? processName : $"{processName}.exe";

            // Create header
            var headerLines = new List<string>()
            {
                $"{FileRecordInfo.HEADER_MARKER}GameName{FileRecordInfo.INFO_SEPERATOR}{string.Empty}",
                $"{FileRecordInfo.HEADER_MARKER}ProcessName{FileRecordInfo.INFO_SEPERATOR}{processNameAdjusted}",
                $"{FileRecordInfo.HEADER_MARKER}CreationDate{FileRecordInfo.INFO_SEPERATOR}{datetime.ToString("yyyy-MM-dd")}",
                $"{FileRecordInfo.HEADER_MARKER}CreationTime{FileRecordInfo.INFO_SEPERATOR}{datetime.ToString("HH:mm:ss")}",
                $"{FileRecordInfo.HEADER_MARKER}Motherboard{FileRecordInfo.INFO_SEPERATOR}{SystemInfo.GetMotherboardName()}",
                $"{FileRecordInfo.HEADER_MARKER}OS{FileRecordInfo.INFO_SEPERATOR}{SystemInfo.GetOSVersion()}",
                $"{FileRecordInfo.HEADER_MARKER}Processor{FileRecordInfo.INFO_SEPERATOR}{SystemInfo.GetProcessorName()}",
                $"{FileRecordInfo.HEADER_MARKER}System RAM{FileRecordInfo.INFO_SEPERATOR}{SystemInfo.GetSystemRAMInfoName()}",
                $"{FileRecordInfo.HEADER_MARKER}Base Driver Version{FileRecordInfo.INFO_SEPERATOR}{string.Empty}",
                $"{FileRecordInfo.HEADER_MARKER}Driver Package{FileRecordInfo.INFO_SEPERATOR}{string.Empty}",
                $"{FileRecordInfo.HEADER_MARKER}GPU{FileRecordInfo.INFO_SEPERATOR}{SystemInfo.GetGraphicCardName()}",
                $"{FileRecordInfo.HEADER_MARKER}GPU #{FileRecordInfo.INFO_SEPERATOR}{string.Empty}",
                $"{FileRecordInfo.HEADER_MARKER}GPU Core Clock (MHz){FileRecordInfo.INFO_SEPERATOR}{string.Empty}",
                $"{FileRecordInfo.HEADER_MARKER}GPU Memory Clock (MHz){FileRecordInfo.INFO_SEPERATOR}{string.Empty}",
                $"{FileRecordInfo.HEADER_MARKER}GPU Memory (MB){FileRecordInfo.INFO_SEPERATOR}{string.Empty}",
                $"{FileRecordInfo.HEADER_MARKER}Comment{FileRecordInfo.INFO_SEPERATOR}{string.Empty}"
            };

            foreach (var headerLine in headerLines)
            {
                csv.AppendLine(headerLine);
            }

            csv.AppendLine(FILE_HEADER);
            string firstDataLine = recordLines.First();

            //start time
            var timeStart = GetStartTimeFromDataLine(firstDataLine);

            // normalize time
            var currentLineSplit = firstDataLine.Split(',');
            currentLineSplit[11] = "0";

            csv.AppendLine(string.Join(",", currentLineSplit));

            foreach (var dataLine in recordLines.Skip(1))
            {
                var extractedProcessName = GetProcessNameFromDataLine(dataLine);
                if (extractedProcessName != null)
                {
                    if (extractedProcessName == processName)
                    {
                        double currentStartTime = GetStartTimeFromDataLine(dataLine);

                        // normalize time
                        double normalizedTime = currentStartTime - timeStart;

                        // cutting offset
                        if (captureTime > 0 && normalizedTime > captureTime)
                            break;

                        currentLineSplit = dataLine.Split(',');
                        currentLineSplit[11] = normalizedTime.ToString(CultureInfo.InvariantCulture);

                        csv.AppendLine(string.Join(",", currentLineSplit));
                    }
                }
            }

            using (var sw = new StreamWriter(filePath))
            {
                sw.Write(csv.ToString());
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string GetProcessNameFromDataLine(string dataLine)
        {
            if (string.IsNullOrWhiteSpace(dataLine))
                return null;

            int index = dataLine.IndexOf(".exe");
            string processName = null;

            if (index > 0)
            {
                processName = dataLine.Substring(0, index);
            }

            return processName;
        }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static long GetQpcTimeFromDataLine(string dataLine)
		{
			if (string.IsNullOrWhiteSpace(dataLine))
				return 0;

			var lineSplit = dataLine.Split(',');
			var qpcTime = lineSplit[17];

			return Convert.ToInt64(qpcTime, CultureInfo.InvariantCulture);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double GetStartTimeFromDataLine(string dataLine)
        {
            if (string.IsNullOrWhiteSpace(dataLine))
                return 0;

            var lineSplit = dataLine.Split(',');
            var startTime = lineSplit[11];

            return Convert.ToDouble(startTime, CultureInfo.InvariantCulture);
        }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static double GetFrameTimeFromDataLine(string dataLine)
		{
			if (string.IsNullOrWhiteSpace(dataLine))
				return 0;

			var lineSplit = dataLine.Split(',');
			var startTime = lineSplit[12];

			return Convert.ToDouble(startTime, CultureInfo.InvariantCulture);
		}

		public void AddGameNameToMatchingList(string processName, string gameName)
        {
            if (string.IsNullOrWhiteSpace(processName) ||
                string.IsNullOrWhiteSpace(gameName))
                return;

            var matchings = File.ReadAllLines(_matchingNameLiveFilename).ToList();

            _processGameMatchingDictionary = new Dictionary<string, string>();

            foreach (var item in matchings)
            {
                var currentMatching = item.Split(FileRecordInfo.INFO_SEPERATOR);
                _processGameMatchingDictionary.Add(currentMatching.First(), currentMatching.Last());
            }

            // Add
            if (!_processGameMatchingDictionary.Keys.Contains(processName))
            {
                _processGameMatchingDictionary.Add(processName, gameName);
                matchings.Add($"{processName}={gameName}");

                File.WriteAllLines(_matchingNameLiveFilename, matchings.OrderBy(name => name));
            }
            // Update
            else
            {
                _processGameMatchingDictionary[processName] = gameName;
                var newMatchings = new List<string>();

                foreach (var keyValuePair in _processGameMatchingDictionary)
                {
                    newMatchings.Add($"{keyValuePair.Key}={keyValuePair.Value}");
                }

                File.WriteAllLines(_matchingNameLiveFilename, newMatchings.OrderBy(name => name));
            }
        }

        public string GetGameFromMatchingList(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
                return string.Empty;

            var gameName = processName;

            if (_processGameMatchingDictionary.Any())
            {
                if (_processGameMatchingDictionary.Keys.Contains(processName))
                    gameName = _processGameMatchingDictionary[processName];
            }
            else
            {
                var matchings = File.ReadAllLines(_matchingNameLiveFilename);

                foreach (var item in matchings)
                {
                    if (item.Contains(processName))
                    {
                        var currentMatching = item.Split('=');
                        gameName = currentMatching.Last();
                    }
                }
            }

            return gameName;
        }
    }
}

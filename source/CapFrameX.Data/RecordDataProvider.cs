using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.PresentMonInterface;
using CapFrameX.Extensions;
using CapFrameX.PresentMonInterface;
using Microsoft.Extensions.Logging;
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
		private static readonly string COLUMN_HEADER =
			$"Application,ProcessID,SwapChainAddress,Runtime,SyncInterval,PresentFlags," +
			$"AllowsTearing,PresentMode,WasBatched,DwmNotified,Dropped,TimeInSeconds,MsBetweenPresents," +
			$"MsBetweenDisplayChange,MsInPresentAPI,MsUntilRenderComplete,MsUntilDisplayed,QPCTime";

		private static readonly string _matchingNameInitialFilename
			= Path.Combine("NameMatching", "ProcessGameNameMatchingList.txt");

		private static readonly string _matchingNameLiveFilename =
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
				@"CapFrameX\Resources\ProcessGameNameMatchingList.txt");

		private readonly IRecordDirectoryObserver _recordObserver;
		private readonly IAppConfiguration _appConfiguration;
		private readonly ILogger<RecordDataProvider> _logger;

		private Dictionary<string, string> _processGameMatchingDictionary = new Dictionary<string, string>();

		public RecordDataProvider(IRecordDirectoryObserver recordObserver, IAppConfiguration appConfiguration,
			ILogger<RecordDataProvider> logger)
		{
			_recordObserver = recordObserver;
			_appConfiguration = appConfiguration;
			_logger = logger;

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
			catch (Exception ex) 
			{
				var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
							@"CapFrameX\Resources");
				_logger.LogError(ex, $"Error while creating {path}");
			}
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
			var filterList = CaptureServiceConfiguration.GetProcessIgnoreList();

			if (filterList.Contains("CapFrameX"))
				filterList.Remove("CapFrameX");

			return _recordObserver.GetAllRecordFileInfo()
				.Select(fileInfo => GetFileRecordInfo(fileInfo))
				.Where(fileRecordInfo => fileRecordInfo != null)
				.Where(fileRecordInfo => CheckNotContains(filterList, fileRecordInfo.ProcessName))
				.OrderBy(fileRecordInfo => fileRecordInfo.GameName)
				.ToList();
		}

		private bool CheckNotContains(HashSet<string> filterList, string processName)
		{
			var check = !filterList
					.Contains(processName?.Replace(".exe", string.Empty));

			return check;
		}

		public bool SavePresentData(IList<string> recordLines, string filePath,
			string processName, bool IsAggregated = false, IList<string> externalHeaderLines = null)
		{
			try
			{
				var csv = new StringBuilder();
				var datetime = DateTime.Now;

				var processNameAdjusted = processName.Contains(".exe") ? processName : $"{processName}.exe";

				// manage custom hardware info
				bool hasCustomInfo = _appConfiguration.HardwareInfoSource
					.ConvertToEnum<EHardwareInfoSource>() == EHardwareInfoSource.Custom;

				var cpuInfo = string.Empty;
				var gpuInfo = string.Empty;
				var ramInfo = string.Empty;

				if (hasCustomInfo)
				{
					cpuInfo = _appConfiguration.CustomCpuDescription;
					gpuInfo = _appConfiguration.CustomGpuDescription;
					ramInfo = _appConfiguration.CustomRamDescription;
				}
				else
				{
					cpuInfo = SystemInfo.GetProcessorName();
					gpuInfo = SystemInfo.GetGraphicCardName();
					ramInfo = SystemInfo.GetSystemRAMInfoName();
				}

				IList<string> headerLines = Enumerable.Empty<string>().ToList();

				// Create header
				if (externalHeaderLines == null)
				{
					headerLines = new List<string>()
					{
						$"{FileRecordInfo.HEADER_MARKER}Id{FileRecordInfo.INFO_SEPERATOR}{Guid.NewGuid().ToString()}",
						$"{FileRecordInfo.HEADER_MARKER}GameName{FileRecordInfo.INFO_SEPERATOR}{string.Empty}",
						$"{FileRecordInfo.HEADER_MARKER}ProcessName{FileRecordInfo.INFO_SEPERATOR}{processNameAdjusted}",
						$"{FileRecordInfo.HEADER_MARKER}CreationDate{FileRecordInfo.INFO_SEPERATOR}{datetime.ToString("yyyy-MM-dd")}",
						$"{FileRecordInfo.HEADER_MARKER}CreationTime{FileRecordInfo.INFO_SEPERATOR}{datetime.ToString("HH:mm:ss")}",
						$"{FileRecordInfo.HEADER_MARKER}Motherboard{FileRecordInfo.INFO_SEPERATOR}{SystemInfo.GetMotherboardName()}",
						$"{FileRecordInfo.HEADER_MARKER}OS{FileRecordInfo.INFO_SEPERATOR}{SystemInfo.GetOSVersion()}",
						$"{FileRecordInfo.HEADER_MARKER}Processor{FileRecordInfo.INFO_SEPERATOR}{cpuInfo}",
						$"{FileRecordInfo.HEADER_MARKER}System RAM{FileRecordInfo.INFO_SEPERATOR}{ramInfo}",
						$"{FileRecordInfo.HEADER_MARKER}Base Driver Version{FileRecordInfo.INFO_SEPERATOR}{string.Empty}",
						$"{FileRecordInfo.HEADER_MARKER}Driver Package{FileRecordInfo.INFO_SEPERATOR}{string.Empty}",
						$"{FileRecordInfo.HEADER_MARKER}GPU{FileRecordInfo.INFO_SEPERATOR}{gpuInfo}",
						$"{FileRecordInfo.HEADER_MARKER}GPU #{FileRecordInfo.INFO_SEPERATOR}{string.Empty}",
						$"{FileRecordInfo.HEADER_MARKER}GPU Core Clock (MHz){FileRecordInfo.INFO_SEPERATOR}{string.Empty}",
						$"{FileRecordInfo.HEADER_MARKER}GPU Memory Clock (MHz){FileRecordInfo.INFO_SEPERATOR}{string.Empty}",
						$"{FileRecordInfo.HEADER_MARKER}GPU Memory (MB){FileRecordInfo.INFO_SEPERATOR}{string.Empty}",
						$"{FileRecordInfo.HEADER_MARKER}Comment{FileRecordInfo.INFO_SEPERATOR}{string.Empty}",
						$"{FileRecordInfo.HEADER_MARKER}IsAggregated{FileRecordInfo.INFO_SEPERATOR}{IsAggregated.ToString()}"
					};
				}
				else
				{
					headerLines = externalHeaderLines;
				}

				foreach (var headerLine in headerLines)
				{
					csv.AppendLine(headerLine);
				}

				csv.AppendLine(COLUMN_HEADER);
				string firstDataLine = recordLines.First();

				//start time
				var timeStart = GetStartTimeFromDataLine(firstDataLine);

				// normalize time
				var currentLineSplit = firstDataLine.Split(',');
				currentLineSplit[11] = "0";

				csv.AppendLine(string.Join(",", currentLineSplit));

				foreach (var dataLine in recordLines.Skip(1))
				{
					double currentStartTime = GetStartTimeFromDataLine(dataLine);

					// normalize time
					double normalizedTime = currentStartTime - timeStart;

					currentLineSplit = dataLine.Split(',');
					currentLineSplit[11] = normalizedTime.ToString(CultureInfo.InvariantCulture);

					csv.AppendLine(string.Join(",", currentLineSplit));
				}

				var csvString = csv.ToString();
				using (var sw = new StreamWriter(filePath))
				{
					sw.Write(csvString);
				}

				_logger.LogInformation("{filePath} successfully written", filePath);
				return true;
			}
			catch (Exception ex) 
			{
				_logger.LogError(ex, "Error while creating {filePath}", filePath);
				return false; 
			}
		}

		public IList<string> CreateHeaderLinesFromRecordInfo(IFileRecordInfo recordInfo )
		{
			var datetime = DateTime.Now;

			return new List<string>()
					{
						$"{FileRecordInfo.HEADER_MARKER}Id{FileRecordInfo.INFO_SEPERATOR}{Guid.NewGuid().ToString()}",
						$"{FileRecordInfo.HEADER_MARKER}GameName{FileRecordInfo.INFO_SEPERATOR}{recordInfo.GameName}",
						$"{FileRecordInfo.HEADER_MARKER}ProcessName{FileRecordInfo.INFO_SEPERATOR}{recordInfo.ProcessName}",
						$"{FileRecordInfo.HEADER_MARKER}CreationDate{FileRecordInfo.INFO_SEPERATOR}{datetime.ToString("yyyy-MM-dd")}",
						$"{FileRecordInfo.HEADER_MARKER}CreationTime{FileRecordInfo.INFO_SEPERATOR}{datetime.ToString("HH:mm:ss")}",
						$"{FileRecordInfo.HEADER_MARKER}Motherboard{FileRecordInfo.INFO_SEPERATOR}{recordInfo.MotherboardName}",
						$"{FileRecordInfo.HEADER_MARKER}OS{FileRecordInfo.INFO_SEPERATOR}{recordInfo.OsVersion}",
						$"{FileRecordInfo.HEADER_MARKER}Processor{FileRecordInfo.INFO_SEPERATOR}{recordInfo.ProcessorName}",
						$"{FileRecordInfo.HEADER_MARKER}System RAM{FileRecordInfo.INFO_SEPERATOR}{recordInfo.SystemRamInfo}",
						$"{FileRecordInfo.HEADER_MARKER}Base Driver Version{FileRecordInfo.INFO_SEPERATOR}{recordInfo.BaseDriverVersion}",
						$"{FileRecordInfo.HEADER_MARKER}Driver Package{FileRecordInfo.INFO_SEPERATOR}{recordInfo.DriverPackage}",
						$"{FileRecordInfo.HEADER_MARKER}GPU{FileRecordInfo.INFO_SEPERATOR}{recordInfo.GraphicCardName}",
						$"{FileRecordInfo.HEADER_MARKER}GPU #{FileRecordInfo.INFO_SEPERATOR}{recordInfo.NumberGPUs}",
						$"{FileRecordInfo.HEADER_MARKER}GPU Core Clock (MHz){FileRecordInfo.INFO_SEPERATOR}{recordInfo.GPUCoreClock}",
						$"{FileRecordInfo.HEADER_MARKER}GPU Memory Clock (MHz){FileRecordInfo.INFO_SEPERATOR}{recordInfo.GPUMemoryClock}",
						$"{FileRecordInfo.HEADER_MARKER}GPU Memory (MB){FileRecordInfo.INFO_SEPERATOR}{recordInfo.GPUMemory}",
						$"{FileRecordInfo.HEADER_MARKER}Comment{FileRecordInfo.INFO_SEPERATOR}{string.Empty}",
						$"{FileRecordInfo.HEADER_MARKER}IsAggregated{FileRecordInfo.INFO_SEPERATOR}{true.ToString()}"
					};
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
		public static string GetProcessIdFromDataLine(string dataLine)
		{
			if (string.IsNullOrWhiteSpace(dataLine))
				return null;

			var lineSplit = dataLine.Split(',');
			return lineSplit[1];
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
			try
			{
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
			// ToDo: Logger
			catch { }
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

		public string GetOutputFilename(string processName)
		{
			var filename = CaptureServiceConfiguration.GetCaptureFilename(processName);
			string observedDirectory = RecordDirectoryObserver
				.GetInitialObservedDirectory(_appConfiguration.ObservedDirectory);

			return Path.Combine(observedDirectory, filename);
		}

		public bool SaveAggregatedPresentData(IList<IList<string>> aggregatedCaptureData, IList<string> externalHeaderLines = null)
		{
			IList<string> aggregatedList = new List<string>();

			var concatedCaptureData = new List<string>(aggregatedCaptureData.Sum(set => set.Count));

			foreach (var frametimeSet in aggregatedCaptureData)
			{
				concatedCaptureData.AddRange(frametimeSet);
			}

			// set first line to 0
			double startTime = 0;

			var firstLineSplit = concatedCaptureData[0].Split(',');
			firstLineSplit[11] = startTime.ToString(CultureInfo.InvariantCulture);
			aggregatedList.Add(string.Join(",", firstLineSplit));

			for (int i = 1; i < concatedCaptureData.Count; i++)
			{
				var lineSplit = concatedCaptureData[i].Split(',');
				var frametime = 1E-03 * Convert.ToDouble(lineSplit[12], CultureInfo.InvariantCulture);
				startTime += frametime;
				lineSplit[11] = startTime.ToString(CultureInfo.InvariantCulture);
				aggregatedList.Add(string.Join(",", lineSplit));
			}

			if (aggregatedList.Any())
			{
				string processName = GetProcessNameFromDataLine(aggregatedList.First());
				return SavePresentData(aggregatedList, GetOutputFilename(processName), processName, true, externalHeaderLines);
			}

			return false;
		}
	}
}

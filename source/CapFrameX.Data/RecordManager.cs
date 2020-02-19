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
	public class RecordManager: IRecordManager
	{
		private readonly ILogger<RecordManager> _logger;
		private readonly IAppConfiguration _appConfiguration;
		private readonly IRecordDirectoryObserver _recordObserver;

		public RecordManager(ILogger<RecordManager> logger, IAppConfiguration appConfiguration, IRecordDirectoryObserver recordObserver)
		{
			_logger = logger;
			_appConfiguration = appConfiguration;
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
			catch (Exception ex)
			{
				var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
							@"CapFrameX\Resources");
				_logger.LogError(ex, $"Error while creating {path}");
			}
		}
		public void UpdateCustomData(IFileRecordInfo recordInfo, string customCpuInfo,
			string customGpuInfo, string customRamInfo, string customGameName, string customComment)
		{
			if (recordInfo == null || customCpuInfo == null ||
				customGpuInfo == null || customRamInfo == null || customGameName == null ||
				customComment == null)
				return;

			try
			{
				string[] lines = File.ReadAllLines(recordInfo.FileInfo.FullName);

				if (recordInfo.HasInfoHeader)
				{
					// Processor
					int processorNameHeaderIndex = GetHeaderIndex(lines, "Processor");
					lines[processorNameHeaderIndex] = $"{FileRecordInfo.HEADER_MARKER}Processor{FileRecordInfo.INFO_SEPERATOR}{customCpuInfo}";

					// GPU
					int graphicCardNameHeaderIndex = GetHeaderIndex(lines, "GPU");
					lines[graphicCardNameHeaderIndex] = $"{FileRecordInfo.HEADER_MARKER}GPU{FileRecordInfo.INFO_SEPERATOR}{customGpuInfo}";

					// RAM
					int systemRamNameHeaderIndex = GetHeaderIndex(lines, "System RAM");
					lines[systemRamNameHeaderIndex] = $"{FileRecordInfo.HEADER_MARKER}System RAM{FileRecordInfo.INFO_SEPERATOR}{customRamInfo}";

					// GameName
					int gameNameHeaderIndex = GetHeaderIndex(lines, "GameName");
					lines[gameNameHeaderIndex] = $"{FileRecordInfo.HEADER_MARKER}GameName{FileRecordInfo.INFO_SEPERATOR}{customGameName}";

					// Comment
					int commentNameHeaderIndex = GetHeaderIndex(lines, "Comment");
					lines[commentNameHeaderIndex] = $"{FileRecordInfo.HEADER_MARKER}Comment{FileRecordInfo.INFO_SEPERATOR}{customComment}";

					File.WriteAllLines(recordInfo.FullPath, lines);
				}
				else
				{
					// Create header
					var headerLines = new List<string>()
					{
						$"{FileRecordInfo.HEADER_MARKER}GameName{FileRecordInfo.INFO_SEPERATOR}{customGameName}",
						$"{FileRecordInfo.HEADER_MARKER}ProcessName{FileRecordInfo.INFO_SEPERATOR}{recordInfo.ProcessName}",
						$"{FileRecordInfo.HEADER_MARKER}CreationDate{FileRecordInfo.INFO_SEPERATOR}{recordInfo.CreationDate}",
						$"{FileRecordInfo.HEADER_MARKER}CreationTime{FileRecordInfo.INFO_SEPERATOR}{recordInfo.CreationTime}",
						$"{FileRecordInfo.HEADER_MARKER}Motherboard{FileRecordInfo.INFO_SEPERATOR}{recordInfo.MotherboardName}",
						$"{FileRecordInfo.HEADER_MARKER}OS{FileRecordInfo.INFO_SEPERATOR}{recordInfo.OsVersion}",
						$"{FileRecordInfo.HEADER_MARKER}Processor{FileRecordInfo.INFO_SEPERATOR}{customCpuInfo}",
						$"{FileRecordInfo.HEADER_MARKER}System RAM{FileRecordInfo.INFO_SEPERATOR}{recordInfo.SystemRamInfo}",
						$"{FileRecordInfo.HEADER_MARKER}Base Driver Version{FileRecordInfo.INFO_SEPERATOR}{recordInfo.BaseDriverVersion}",
						$"{FileRecordInfo.HEADER_MARKER}Driver Package{FileRecordInfo.INFO_SEPERATOR}{recordInfo.DriverPackage}",
						$"{FileRecordInfo.HEADER_MARKER}GPU{FileRecordInfo.INFO_SEPERATOR}{customGpuInfo}",
						$"{FileRecordInfo.HEADER_MARKER}GPU #{FileRecordInfo.INFO_SEPERATOR}{recordInfo.NumberGPUs}",
						$"{FileRecordInfo.HEADER_MARKER}GPU Core Clock (MHz){FileRecordInfo.INFO_SEPERATOR}{recordInfo.GPUCoreClock}",
						$"{FileRecordInfo.HEADER_MARKER}GPU Memory Clock (MHz){FileRecordInfo.INFO_SEPERATOR}{recordInfo.GPUMemoryClock}",
						$"{FileRecordInfo.HEADER_MARKER}GPU Memory (MB){FileRecordInfo.INFO_SEPERATOR}{recordInfo.GPUMemory}",
						$"{FileRecordInfo.HEADER_MARKER}Comment{FileRecordInfo.INFO_SEPERATOR}{customComment}"
					};

					File.WriteAllLines(recordInfo.FullPath, headerLines.Concat(lines));
				}
			}
			catch(Exception ex) {
				_logger.LogError(ex, "Error writing Lines");
			}
		}

		private int GetHeaderIndex(string[] lines, string headerEntry)
		{
			int index = 0;
			while (!lines[index].Contains(headerEntry)) index++;
			return index;
		}

		public List<ISystemInfoEntry> GetSystemInfos(IFileRecordInfo recordInfo)
		{
			_logger.LogInformation("Getting Systeminfos");
			var systemInfos = new List<ISystemInfoEntry>();

			if (!string.IsNullOrWhiteSpace(recordInfo.CreationDate))
				systemInfos.Add(new SystemInfoEntry() { Key = "Creation Date & Time", Value = recordInfo.CreationDate + "  |  " + recordInfo.CreationTime });
			if (!string.IsNullOrWhiteSpace(recordInfo.Comment))
				systemInfos.Add(new SystemInfoEntry() { Key = "Comment", Value = recordInfo.Comment });
			if (!string.IsNullOrWhiteSpace(recordInfo.ProcessorName))
				systemInfos.Add(new SystemInfoEntry() { Key = "Processor", Value = recordInfo.ProcessorName });
			if (!string.IsNullOrWhiteSpace(recordInfo.SystemRamInfo))
				systemInfos.Add(new SystemInfoEntry() { Key = "System RAM", Value = recordInfo.SystemRamInfo });
			if (!string.IsNullOrWhiteSpace(recordInfo.GraphicCardName))
				systemInfos.Add(new SystemInfoEntry() { Key = "Graphics Card", Value = recordInfo.GraphicCardName });
			if (!string.IsNullOrWhiteSpace(recordInfo.MotherboardName))
				systemInfos.Add(new SystemInfoEntry() { Key = "Motherboard", Value = recordInfo.MotherboardName });
			if (!string.IsNullOrWhiteSpace(recordInfo.OsVersion))
				systemInfos.Add(new SystemInfoEntry() { Key = "OS Version", Value = recordInfo.OsVersion });
			if (!string.IsNullOrWhiteSpace(recordInfo.NumberGPUs))
				systemInfos.Add(new SystemInfoEntry() { Key = "GPU #", Value = recordInfo.NumberGPUs });
			if (!string.IsNullOrWhiteSpace(recordInfo.GPUCoreClock))
				systemInfos.Add(new SystemInfoEntry() { Key = "GPU Core Clock (MHz)", Value = recordInfo.GPUCoreClock });
			if (!string.IsNullOrWhiteSpace(recordInfo.GPUMemoryClock))
				systemInfos.Add(new SystemInfoEntry() { Key = "GPU Memory Clock (MHz)", Value = recordInfo.GPUMemoryClock });
			if (!string.IsNullOrWhiteSpace(recordInfo.GPUMemory))
				systemInfos.Add(new SystemInfoEntry() { Key = "GPU Memory (MB)", Value = recordInfo.GPUMemory });
			if (!string.IsNullOrWhiteSpace(recordInfo.BaseDriverVersion))
				systemInfos.Add(new SystemInfoEntry() { Key = "Base Driver Version", Value = recordInfo.BaseDriverVersion });
			if (!string.IsNullOrWhiteSpace(recordInfo.DriverPackage))
				systemInfos.Add(new SystemInfoEntry() { Key = "Driver Package", Value = recordInfo.DriverPackage });

			return systemInfos;
		}

		public ISession LoadData(string csvFile)
		{
			_logger.LogInformation("Loading data from: {path}", csvFile);
			if (string.IsNullOrWhiteSpace(csvFile))
			{
				return null;
			}

			if (!File.Exists(csvFile))
			{
				return null;
			}

			if (new FileInfo(csvFile).Length == 0)
			{
				return null;
			}

			var session = new Session
			{
				Path = csvFile,
				IsVR = false
			};

			int index = csvFile.LastIndexOf('\\');
			session.Filename = csvFile.Substring(index + 1);

			session.FrameStart = new List<double>();
			session.FrameEnd = new List<double>();
			session.FrameTimes = new List<double>();
			session.ReprojectionStart = new List<double>();
			session.ReprojectionEnd = new List<double>();
			session.ReprojectionTimes = new List<double>();
			session.VSync = new List<double>();
			session.AppMissed = new List<bool>();
			session.WarpMissed = new List<bool>();
			session.DisplayTimes = new List<double>();
			session.QPCTimes = new List<double>();
			session.InPresentAPITimes = new List<double>();
			session.UntilDisplayedTimes = new List<double>();
			session.WarpMissesCount = 0;
			session.LastFrameTime = 0;
			session.ValidReproFrames = 0;

			try
			{
				using (var reader = new StreamReader(csvFile))
				{
					string line = reader.ReadLine();

					// skip header
					while (line.Contains(FileRecordInfo.HEADER_MARKER))
					{
						line = reader.ReadLine();
					}

					int indexFrameStart = -1;
					int indexFrameTimes = -1;
					int indexFrameEnd = -1;
					int indexUntilDisplayedTimes = -1;
					int indexVSync = -1;
					int indexAppMissed = -1;
					int indexWarpMissed = -1;
					int indexMsInPresentAPI = -1;
					int indexDisplayTimes = -1;
					int indexQPCTimes = -1;

					var metrics = line.Split(',');
					for (int i = 0; i < metrics.Count(); i++)
					{
						if (string.Compare(metrics[i], "AppRenderStart") == 0 || string.Compare(metrics[i], "TimeInSeconds") == 0)
						{
							indexFrameStart = i;
						}
						// MsUntilRenderComplete needs to be added to AppRenderStart to get the timestamp
						if (string.Compare(metrics[i], "AppRenderEnd") == 0 || string.Compare(metrics[i], "MsUntilRenderComplete") == 0)
						{
							indexFrameEnd = i;
						}
						if (string.Compare(metrics[i], "MsBetweenAppPresents") == 0 || string.Compare(metrics[i], "MsBetweenPresents") == 0)
						{
							indexFrameTimes = i;
						}
						if (string.Compare(metrics[i], "MsUntilDisplayed") == 0)
						{
							indexUntilDisplayedTimes = i;
						}
						if (string.Compare(metrics[i], "VSync") == 0)
						{
							indexVSync = i;
							session.IsVR = true;
						}
						if (string.Compare(metrics[i], "AppMissed") == 0 || string.Compare(metrics[i], "Dropped") == 0)
						{
							indexAppMissed = i;
						}
						if (string.Compare(metrics[i], "WarpMissed") == 0 || string.Compare(metrics[i], "LsrMissed") == 0)
						{
							indexWarpMissed = i;
						}
						if (string.Compare(metrics[i], "MsInPresentAPI") == 0)
						{
							indexMsInPresentAPI = i;
						}
						if (string.Compare(metrics[i], "MsBetweenDisplayChange") == 0)
						{
							indexDisplayTimes = i;
						}
						if (string.Compare(metrics[i], "QPCTime") == 0)
						{
							indexQPCTimes = i;
						}
					}

					int lineCount = 0;
					while (!reader.EndOfStream)
					{
						line = reader.ReadLine();
						var lineCharList = new List<char>();
						lineCount++;
						string[] values = new string[0];

						if (lineCount < 2)
						{
							int isInner = -1;
							for (int i = 0; i < line.Length; i++)
							{
								if (line[i] == '"')
									isInner *= -1;

								if (!(line[i] == ',' && isInner == 1))
									lineCharList.Add(line[i]);

							}

							line = new string(lineCharList.ToArray());
						}

						values = line.Split(',');
						double frameStart = 0;

						if (indexFrameStart > 0 && indexFrameTimes > 0)
						{
							if (double.TryParse(GetStringFromArray(values, indexFrameStart), NumberStyles.Any, CultureInfo.InvariantCulture, out frameStart)
								&& double.TryParse(GetStringFromArray(values, indexFrameTimes), NumberStyles.Any, CultureInfo.InvariantCulture, out var frameTimes))
							{
								if (frameStart > 0)
								{
									session.LastFrameTime = frameStart;
								}
								session.FrameStart.Add(frameStart);
								session.FrameTimes.Add(frameTimes);
							}
						}

						if (indexAppMissed > 0)
						{
							if (int.TryParse(GetStringFromArray(values, indexAppMissed), NumberStyles.Any, CultureInfo.InvariantCulture, out var appMissed))
							{
								session.AppMissed.Add(Convert.ToBoolean(appMissed));
							}
							else
							{
								session.AppMissed.Add(true);
							}
						}

						if (indexDisplayTimes > 0)
						{
							if (double.TryParse(GetStringFromArray(values, indexDisplayTimes), NumberStyles.Any, CultureInfo.InvariantCulture, out var displayTime))
							{
								session.DisplayTimes.Add(displayTime);
							}
						}

						if (indexUntilDisplayedTimes > 0)
						{
							if (double.TryParse(GetStringFromArray(values, indexUntilDisplayedTimes), NumberStyles.Any, CultureInfo.InvariantCulture, out var untilDisplayTime))
							{
								session.UntilDisplayedTimes.Add(untilDisplayTime);
							}
						}

						if (indexMsInPresentAPI > 0)
						{
							if (double.TryParse(GetStringFromArray(values, indexMsInPresentAPI), NumberStyles.Any, CultureInfo.InvariantCulture, out var inPresentAPITime))
							{
								session.InPresentAPITimes.Add(inPresentAPITime);
							}
						}

						if (indexQPCTimes > 0)
						{
							if (double.TryParse(GetStringFromArray(values, indexQPCTimes), NumberStyles.Any, CultureInfo.InvariantCulture, out var qPCTime))
							{
								session.QPCTimes.Add(qPCTime);
							}
						}

						if (indexFrameEnd > 0)
						{
							if (double.TryParse(GetStringFromArray(values, indexFrameEnd), NumberStyles.Any, CultureInfo.InvariantCulture, out var frameEnd))
							{
								if (session.IsVR)
								{
									session.FrameEnd.Add(frameEnd);
								}
								else
								{
									session.FrameEnd.Add(frameStart + frameEnd / 1000.0);
								}
							}
						}

						if (indexVSync > 0 && indexWarpMissed > 0)
						{
							if (double.TryParse(GetStringFromArray(values, indexVSync), NumberStyles.Any, CultureInfo.InvariantCulture, out var vSync)
							 && int.TryParse(GetStringFromArray(values, indexWarpMissed), NumberStyles.Any, CultureInfo.InvariantCulture, out var warpMissed))
							{
								session.VSync.Add(vSync);
								session.WarpMissed.Add(Convert.ToBoolean(warpMissed));
								session.WarpMissesCount += warpMissed;
							}
						}
					}
				}
			}
			catch (IOException ex)
			{
				_logger.LogError(ex, "Error loading Data");
				return null;
			}

			return session;
		}

		public IList<string> LoadPresentData(string csvFile)
		{
			if (string.IsNullOrWhiteSpace(csvFile))
			{
				return null;
			}

			if (!File.Exists(csvFile))
			{
				return null;
			}

			if (new FileInfo(csvFile).Length == 0)
			{
				return null;
			}

			var dataLines = new List<string>();

			try
			{
				using (var reader = new StreamReader(csvFile))
				{
					string line = reader.ReadLine();

					// skip header
					while (line.Contains(FileRecordInfo.HEADER_MARKER))
					{
						line = reader.ReadLine();
					}

					//skip column header
					_ = reader.ReadLine();

					while (!reader.EndOfStream)
					{
						dataLines.Add(reader.ReadLine());
					}
				}

				return dataLines;
			}
			catch (IOException ex)
			{
				_logger.LogError(ex, "Error loading Data");
				return null;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private string GetStringFromArray(string[] array, int index)
		{
			var value = string.Empty;

			if (index < array.Length)
			{
				value = array[index];
			}

			return value;
		}

		private static readonly string COLUMN_HEADER =
			$"Application,ProcessID,SwapChainAddress,Runtime,SyncInterval,PresentFlags," +
			$"AllowsTearing,PresentMode,WasBatched,DwmNotified,Dropped,TimeInSeconds,MsBetweenPresents," +
			$"MsBetweenDisplayChange,MsInPresentAPI,MsUntilRenderComplete,MsUntilDisplayed,QPCTime";

		private static readonly string _matchingNameInitialFilename
			= Path.Combine("NameMatching", "ProcessGameNameMatchingList.txt");

		private static readonly string _matchingNameLiveFilename =
			Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
				@"CapFrameX\Resources\ProcessGameNameMatchingList.txt");

		private Dictionary<string, string> _processGameMatchingDictionary = new Dictionary<string, string>();


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

		public IList<string> CreateHeaderLinesFromRecordInfo(IFileRecordInfo recordInfo)
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
		public string GetProcessNameFromDataLine(string dataLine)
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
		public string GetProcessIdFromDataLine(string dataLine)
		{
			if (string.IsNullOrWhiteSpace(dataLine))
				return null;

			var lineSplit = dataLine.Split(',');
			return lineSplit[1];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public long GetQpcTimeFromDataLine(string dataLine)
		{
			if (string.IsNullOrWhiteSpace(dataLine))
				return 0;

			var lineSplit = dataLine.Split(',');
			var qpcTime = lineSplit[17];

			return Convert.ToInt64(qpcTime, CultureInfo.InvariantCulture);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public double GetStartTimeFromDataLine(string dataLine)
		{
			if (string.IsNullOrWhiteSpace(dataLine))
				return 0;

			var lineSplit = dataLine.Split(',');
			var startTime = lineSplit[11];

			return Convert.ToDouble(startTime, CultureInfo.InvariantCulture);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public double GetFrameTimeFromDataLine(string dataLine)
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
				if (firstLineSplit.Length != lineSplit.Length)
					continue;

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

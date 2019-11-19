using CapFrameX.Contracts.Data;
using CapFrameX.Data;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CapFrameX.Data
{
	public static class RecordManager
	{
		public static void UpdateCustomData(IFileRecordInfo recordInfo, string customCpuInfo,
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
			//Todo: write message to logger
			catch { }
		}

		private static int GetHeaderIndex(string[] lines, string headerEntry)
		{
			int index = 0;
			while (!lines[index].Contains(headerEntry)) index++;
			return index;
		}

		public static List<SystemInfoEntry> GetSystemInfos(IFileRecordInfo recordInfo)
		{
			var systemInfos = new List<SystemInfoEntry>();

			if (recordInfo.MotherboardName != null)
				systemInfos.Add(new SystemInfoEntry() { Key = "Motherboard", Value = recordInfo.MotherboardName });
			if (recordInfo.OsVersion != null)
				systemInfos.Add(new SystemInfoEntry() { Key = "OS Version", Value = recordInfo.OsVersion });
			if (recordInfo.ProcessorName != null)
				systemInfos.Add(new SystemInfoEntry() { Key = "Processor", Value = recordInfo.ProcessorName });
			if (recordInfo.SystemRamInfo != null)
				systemInfos.Add(new SystemInfoEntry() { Key = "System RAM Info", Value = recordInfo.SystemRamInfo });
			if (recordInfo.BaseDriverVersion != null)
				systemInfos.Add(new SystemInfoEntry() { Key = "Base Driver Version", Value = recordInfo.BaseDriverVersion });
			if (recordInfo.DriverPackage != null)
				systemInfos.Add(new SystemInfoEntry() { Key = "Driver Package", Value = recordInfo.DriverPackage });
			if (recordInfo.NumberGPUs != null)
				systemInfos.Add(new SystemInfoEntry() { Key = "GPU #", Value = recordInfo.NumberGPUs });
			if (recordInfo.GraphicCardName != null)
				systemInfos.Add(new SystemInfoEntry() { Key = "Graphic Card", Value = recordInfo.GraphicCardName });
			if (recordInfo.GPUCoreClock != null)
				systemInfos.Add(new SystemInfoEntry() { Key = "GPU Core Clock (MHz)", Value = recordInfo.GPUCoreClock });
			if (recordInfo.GPUMemoryClock != null)
				systemInfos.Add(new SystemInfoEntry() { Key = "GPU Memory Clock (MHz)", Value = recordInfo.GPUMemoryClock });
			if (recordInfo.GPUMemory != null)
				systemInfos.Add(new SystemInfoEntry() { Key = "GPU Memory (MB)", Value = recordInfo.GPUMemory });
			if (recordInfo.Comment != null)
				systemInfos.Add(new SystemInfoEntry() { Key = "Comment", Value = recordInfo.Comment });

			return systemInfos;
		}

		public static Session LoadData(string csvFile)
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
			session.Displaytimes = new List<double>();
			session.QPCTimes = new List<double>();

			session.AppMissesCount = 0;
			session.WarpMissesCount = 0;
			session.ValidAppFrames = 0;
			session.LastFrameTime = 0;
			session.ValidReproFrames = 0;
			session.LastReprojectionTime = 0;

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
					int indexReprojectionStart = -1;
					int indexReprojectionTimes = -1;
					int indexReprojectionEnd = -1;
					int indexVSync = -1;
					int indexAppMissed = -1;
					int indexWarpMissed = -1;
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
						if (string.Compare(metrics[i], "ReprojectionStart") == 0)
						{
							indexReprojectionStart = i;
						}
						//MsUntilDisplayed needs to be added to AppRenderStart, we don't have a reprojection start timestamp in this case
						if (string.Compare(metrics[i], "ReprojectionEnd") == 0 || string.Compare(metrics[i], "MsUntilDisplayed") == 0)
						{
							indexReprojectionEnd = i;
						}
						if (string.Compare(metrics[i], "MsBetweenReprojections") == 0 || string.Compare(metrics[i], "MsBetweenLsrs") == 0)
						{
							indexReprojectionTimes = i;
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

						if (indexFrameStart > 0 && indexFrameTimes > 0 && indexAppMissed > 0)
						{
							// non VR titles only have app render start and frame times metrics
							// app render end and reprojection end get calculated based on ms until render complete and ms until displayed metric
							if (double.TryParse(GetStringFromArray(values, indexFrameStart), NumberStyles.Any, CultureInfo.InvariantCulture, out frameStart)
								&& double.TryParse(GetStringFromArray(values, indexFrameTimes), NumberStyles.Any, CultureInfo.InvariantCulture, out var frameTimes)
								&& int.TryParse(GetStringFromArray(values, indexAppMissed), NumberStyles.Any, CultureInfo.InvariantCulture, out var appMissed))
							{
								if (frameStart > 0)
								{
									session.ValidAppFrames++;
									session.LastFrameTime = frameStart;
								}
								session.FrameStart.Add(frameStart);
								session.FrameTimes.Add(frameTimes);

								session.AppMissed.Add(Convert.ToBoolean(appMissed));
								session.AppMissesCount += appMissed;
							}
						}

						if (indexDisplayTimes > 0)
						{
							if (double.TryParse(GetStringFromArray(values, indexDisplayTimes), NumberStyles.Any, CultureInfo.InvariantCulture, out var displayTime))
							{
								session.Displaytimes.Add(displayTime);
							}
						}

						if (indexQPCTimes > 0)
						{
							if (double.TryParse(GetStringFromArray(values, indexQPCTimes), NumberStyles.Any, CultureInfo.InvariantCulture, out var qPCTime))
							{
								session.Displaytimes.Add(qPCTime);
							}
						}

						if (indexFrameEnd > 0 && indexReprojectionEnd > 0)
						{
							if (double.TryParse(GetStringFromArray(values, indexFrameEnd), NumberStyles.Any, CultureInfo.InvariantCulture, out var frameEnd)
							 && double.TryParse(GetStringFromArray(values, indexReprojectionEnd), NumberStyles.Any, CultureInfo.InvariantCulture, out var reprojectionEnd))
							{
								if (session.IsVR)
								{
									session.FrameEnd.Add(frameEnd);
									session.ReprojectionEnd.Add(reprojectionEnd);
								}
								else
								{
									session.FrameEnd.Add(frameStart + frameEnd / 1000.0);
									session.ReprojectionEnd.Add(frameStart + reprojectionEnd / 1000.0);
								}
							}
						}

						if (indexReprojectionStart > 0 && indexReprojectionTimes > 0 && indexVSync > 0 && indexWarpMissed > 0)
						{
							if (double.TryParse(GetStringFromArray(values, indexReprojectionStart), NumberStyles.Any, CultureInfo.InvariantCulture, out var reprojectionStart)
							 && double.TryParse(GetStringFromArray(values, indexReprojectionTimes), NumberStyles.Any, CultureInfo.InvariantCulture, out var reprojectionTimes)
							 && double.TryParse(GetStringFromArray(values, indexVSync), NumberStyles.Any, CultureInfo.InvariantCulture, out var vSync)
							 && int.TryParse(GetStringFromArray(values, indexWarpMissed), NumberStyles.Any, CultureInfo.InvariantCulture, out var warpMissed))
							{
								if (reprojectionStart > 0)
								{
									session.ValidReproFrames++;
									session.LastReprojectionTime = reprojectionStart;
								}
								session.ReprojectionStart.Add(reprojectionStart);
								session.ReprojectionTimes.Add(reprojectionTimes);
								session.VSync.Add(vSync);
								session.WarpMissed.Add(Convert.ToBoolean(warpMissed));
								session.WarpMissesCount += warpMissed;
							}
						}
					}
				}
			}
			catch (IOException)
			{
				return null;
			}

			return session;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static string GetStringFromArray(string[] array, int index)
		{
			var value = string.Empty;

			if (index < array.Length)
			{
				value = array[index];
			}

			return value;
		}
	}
}

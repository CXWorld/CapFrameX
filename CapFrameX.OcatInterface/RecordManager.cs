using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CapFrameX.OcatInterface
{
	public static class RecordManager
	{
		public static void UpdateCustomData(OcatRecordInfo recordInfo, string customCpuInfo, string customGpuInfo, string customComment)
		{
			string[] lines = File.ReadAllLines(recordInfo.FullPath);

			if (!lines[0].Contains("Comment"))
			{
				lines[0] = lines[0] + ",Comment";
				lines[1] = lines[1] + ",-";
			}

			int indexProcessorName = -1;
			int indexGraphicCardName = -1;
			int indexComment = -1;

			var metrics = lines[0].Split(',');

			for (int i = 0; i < metrics.Length; i++)
			{
				if (String.Compare(metrics[i], "Processor") == 0)
				{
					indexProcessorName = i;
				}
				if (String.Compare(metrics[i], "GPU") == 0)
				{
					indexGraphicCardName = i;
				}
				if (String.Compare(metrics[i], "Comment") == 0)
				{
					indexComment = i;
				}
			}

			if (indexProcessorName < 0 || indexGraphicCardName < 0 || indexComment < 0)
				return;

			var customData = lines[1].Split(',');
			customData[indexProcessorName] = customCpuInfo;
			customData[indexGraphicCardName] = customGpuInfo;
			customData[indexComment] = customComment;

			lines[1] = string.Join(",", customData);
			File.WriteAllLines(recordInfo.FullPath, lines);
		}

		public static List<SystemInfo> GetSystemInfos(Session session)
		{
			var systemInfos = new List<SystemInfo>();

			if (session.MotherboardName != null)
				systemInfos.Add(new SystemInfo() { Key = "Motherboard", Value = session.MotherboardName });
			if (session.OsVersion != null)
				systemInfos.Add(new SystemInfo() { Key = "OS Version", Value = session.OsVersion });
			if (session.ProcessorName != null)
				systemInfos.Add(new SystemInfo() { Key = "Processor", Value = session.ProcessorName });
			if (session.SystemRamInfo != null)
				systemInfos.Add(new SystemInfo() { Key = "System RAM Info", Value = session.SystemRamInfo });
			if (session.BaseDriverVersion != null)
				systemInfos.Add(new SystemInfo() { Key = "Base Driver Version", Value = session.BaseDriverVersion });
			if (session.DriverPackage != null)
				systemInfos.Add(new SystemInfo() { Key = "Driver Package", Value = session.DriverPackage });
			if (session.NumberGPUs != null)
				systemInfos.Add(new SystemInfo() { Key = "GPU #", Value = session.NumberGPUs });
			if (session.GraphicCardName != null)
				systemInfos.Add(new SystemInfo() { Key = "Graphic Card", Value = session.GraphicCardName });
			if (session.GPUCoreClock != null)
				systemInfos.Add(new SystemInfo() { Key = "GPU Core Clock (MHz)", Value = session.GPUCoreClock });
			if (session.GPUMemoryClock != null)
				systemInfos.Add(new SystemInfo() { Key = "GPU Memory Clock (MHz)", Value = session.GPUMemoryClock });
			if (session.GPUMemory != null)
				systemInfos.Add(new SystemInfo() { Key = "GPU Memory (MB)", Value = session.GPUMemory });
			if (session.Comment != null)
				systemInfos.Add(new SystemInfo() { Key = "Comment", Value = session.Comment });

			return systemInfos;
		}

		public static Session LoadData(string csvFile)
		{
			if (csvFile == null || csvFile == "")
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
					// header -> csv layout may differ, identify correct columns based on column title
					var line = reader.ReadLine();
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

					// System info
					int indexMotherboardName = -1;
					int indexOsVersion = -1;
					int indexProcessorName = -1;
					int indexSystemRamInfo = -1;
					int indexBaseDriverVersion = -1;
					int indexDriverPackage = -1;
					int indexNumberGPUs = -1;
					int indexGraphicCardName = -1;
					int indexGPUCoreClock = -1;
					int indexGPUMemoryClock = -1;
					int indexGPUMemory = -1;
					int indexComment = -1;

					var metrics = line.Split(',');
					for (int i = 0; i < metrics.Count(); i++)
					{
						if (String.Compare(metrics[i], "AppRenderStart") == 0 || String.Compare(metrics[i], "TimeInSeconds") == 0)
						{
							indexFrameStart = i;
						}
						// MsUntilRenderComplete needs to be added to AppRenderStart to get the timestamp
						if (String.Compare(metrics[i], "AppRenderEnd") == 0 || String.Compare(metrics[i], "MsUntilRenderComplete") == 0)
						{
							indexFrameEnd = i;
						}
						if (String.Compare(metrics[i], "MsBetweenAppPresents") == 0 || String.Compare(metrics[i], "MsBetweenPresents") == 0)
						{
							indexFrameTimes = i;
						}
						if (String.Compare(metrics[i], "ReprojectionStart") == 0)
						{
							indexReprojectionStart = i;
						}
						//MsUntilDisplayed needs to be added to AppRenderStart, we don't have a reprojection start timestamp in this case
						if (String.Compare(metrics[i], "ReprojectionEnd") == 0 || String.Compare(metrics[i], "MsUntilDisplayed") == 0)
						{
							indexReprojectionEnd = i;
						}
						if (String.Compare(metrics[i], "MsBetweenReprojections") == 0 || String.Compare(metrics[i], "MsBetweenLsrs") == 0)
						{
							indexReprojectionTimes = i;
						}
						if (String.Compare(metrics[i], "VSync") == 0)
						{
							indexVSync = i;
							session.IsVR = true;
						}
						if (String.Compare(metrics[i], "AppMissed") == 0 || String.Compare(metrics[i], "Dropped") == 0)
						{
							indexAppMissed = i;
						}
						if (String.Compare(metrics[i], "WarpMissed") == 0 || String.Compare(metrics[i], "LsrMissed") == 0)
						{
							indexWarpMissed = i;
						}
						if (String.Compare(metrics[i], "MsBetweenDisplayChange") == 0)
						{
							indexDisplayTimes = i;
						}

						// System info
						if (String.Compare(metrics[i], "Motherboard") == 0)
						{
							indexMotherboardName = i;
						}
						if (String.Compare(metrics[i], "OS") == 0)
						{
							indexOsVersion = i;
						}
						if (String.Compare(metrics[i], "Processor") == 0)
						{
							indexProcessorName = i;
						}
						if (String.Compare(metrics[i], "System RAM") == 0)
						{
							indexSystemRamInfo = i;
						}
						if (String.Compare(metrics[i], "Base Driver Version") == 0)
						{
							indexBaseDriverVersion = i;
						}
						if (String.Compare(metrics[i], "Driver Package") == 0)
						{
							indexDriverPackage = i;
						}
						if (String.Compare(metrics[i], "GPU #") == 0)
						{
							indexNumberGPUs = i;
						}
						if (String.Compare(metrics[i], "GPU") == 0)
						{
							indexGraphicCardName = i;
						}
						if (String.Compare(metrics[i], "GPU Core Clock (MHz)") == 0)
						{
							indexGPUCoreClock = i;
						}
						if (String.Compare(metrics[i], "GPU Memory Clock (MHz)") == 0)
						{
							indexGPUMemoryClock = i;
						}
						if (String.Compare(metrics[i], "GPU Memory (MB)") == 0)
						{
							indexGPUMemory = i;
						}
						if (String.Compare(metrics[i], "Comment") == 0)
						{
							indexComment = i;
						}
					}

					int lineCount = 0;
					while (!reader.EndOfStream)
					{
						line = reader.ReadLine();
						lineCount++;
						var values = line.Split(',');
						double frameStart = 0;

						if (indexFrameStart > 0 && indexFrameTimes > 0 && indexAppMissed > 0)
						{
							// non VR titles only have app render start and frame times metrics
							// app render end and reprojection end get calculated based on ms until render complete and ms until displayed metric
							if (double.TryParse(values[indexFrameStart], NumberStyles.Any, CultureInfo.InvariantCulture, out frameStart)
								&& double.TryParse(values[indexFrameTimes], NumberStyles.Any, CultureInfo.InvariantCulture, out var frameTimes)
								&& int.TryParse(values[indexAppMissed], NumberStyles.Any, CultureInfo.InvariantCulture, out var appMissed))
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
							if (double.TryParse(values[indexDisplayTimes], NumberStyles.Any, CultureInfo.InvariantCulture, out var displayTime))
							{
								session.Displaytimes.Add(displayTime);
							}
						}

						if (indexFrameEnd > 0 && indexReprojectionEnd > 0)
						{
							if (double.TryParse(values[indexFrameEnd], NumberStyles.Any, CultureInfo.InvariantCulture, out var frameEnd)
							 && double.TryParse(values[indexReprojectionEnd], NumberStyles.Any, CultureInfo.InvariantCulture, out var reprojectionEnd))
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
							if (double.TryParse(values[indexReprojectionStart], NumberStyles.Any, CultureInfo.InvariantCulture, out var reprojectionStart)
							 && double.TryParse(values[indexReprojectionTimes], NumberStyles.Any, CultureInfo.InvariantCulture, out var reprojectionTimes)
							 && double.TryParse(values[indexVSync], NumberStyles.Any, CultureInfo.InvariantCulture, out var vSync)
							 && int.TryParse(values[indexWarpMissed], NumberStyles.Any, CultureInfo.InvariantCulture, out var warpMissed))
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

						if (lineCount < 2 && indexMotherboardName > 0 && indexOsVersion > 0 && indexProcessorName > 0 &&
							indexSystemRamInfo > 0 && indexBaseDriverVersion > 0 && indexDriverPackage > 0 &&
							indexNumberGPUs > 0 && indexGraphicCardName > 0 && indexGPUCoreClock > 0 &&
							indexGPUMemoryClock > 0 && indexGPUMemory > 0)
						{
							session.MotherboardName = values[indexMotherboardName].Trim(new Char[] { ' ', '"' });
							session.OsVersion = values[indexOsVersion].Trim(new Char[] { ' ', '"' });
							session.ProcessorName = values[indexProcessorName].Trim(new Char[] { ' ', '"' });
							session.SystemRamInfo = values[indexSystemRamInfo].Trim(new Char[] { ' ', '"' });
							session.BaseDriverVersion = values[indexBaseDriverVersion].Trim(new Char[] { ' ', '"' });
							session.DriverPackage = values[indexDriverPackage].Trim(new Char[] { ' ', '"' });
							session.NumberGPUs = values[indexNumberGPUs].Trim(new Char[] { ' ', '"' });
							session.GraphicCardName = values[indexGraphicCardName].Trim(new Char[] { ' ', '"' });
							session.GPUCoreClock = values[indexGPUCoreClock].Trim(new Char[] { ' ', '"' });
							session.GPUMemoryClock = values[indexGPUMemoryClock].Trim(new Char[] { ' ', '"' });
							session.GPUMemory = values[indexGPUMemory].Trim(new Char[] { ' ', '"' });
						}

						//Custom extension
						if (lineCount < 2 && indexComment > 0)
							session.Comment = values[indexComment].Trim(new Char[] { ' ', '"' });
					}
				}
			}
			catch (IOException)
			{
				return null;
			}

			return session;
		}
	}
}

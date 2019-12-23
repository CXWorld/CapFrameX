using CapFrameX.Contracts.Data;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace CapFrameX.Data
{
	public class FileRecordInfo : IFileRecordInfo
	{
		public static string HEADER_MARKER = "//";
		public static readonly char INFO_SEPERATOR = '=';

		private string[] _lines;

		public string GameName { get; set; }
		public string ProcessName { get; private set; }
		public string CreationDate { get; private set; }
		public string CreationTime { get; private set; }
		public double RecordTime { get; private set; }
		public string FullPath { get; private set; }
		public FileInfo FileInfo { get; private set; }
		public string CombinedInfo { get; private set; }
		public string MotherboardName { get; private set; }
		public string OsVersion { get; private set; }
		public string ProcessorName { get; private set; }
		public string SystemRamInfo { get; private set; }
		public string BaseDriverVersion { get; private set; }
		public string DriverPackage { get; private set; }
		public string NumberGPUs { get; private set; }
		public string GraphicCardName { get; private set; }
		public string GPUCoreClock { get; private set; }
		public string GPUMemoryClock { get; private set; }
		public string GPUMemory { get; private set; }
		public string Comment { get; private set; }
		public string IsAggregated { get; private set; }
		public bool IsValid { get; private set; }
		public bool HasInfoHeader { get; private set; }
		public string Id { get; private set; }

		private FileRecordInfo(FileInfo fileInfo)
		{
			if (fileInfo != null && File.Exists(fileInfo.FullName))
			{
				FileInfo = fileInfo;
				FullPath = fileInfo.FullName;
				_lines = File.ReadAllLines(fileInfo.FullName);

				if (_lines != null && _lines.Any())
				{
					Dictionary<string, string> infoKeyValueDictionary = new Dictionary<string, string>();

					HasInfoHeader = GetHasInfoHeader(_lines[0]);
					IsValid = GetIsValid(_lines);

					if (IsValid)
					{						
						if (HasInfoHeader)
						{
							FillPropertyDictionary(infoKeyValueDictionary);
						}
						else
						{
							// OCAT column headers
							// Application,ProcessID,SwapChainAddress,Runtime,SyncInterval,PresentFlags,
							// AllowsTearing,PresentMode,WasBatched,DwmNotified,Dropped,TimeInSeconds,
							// MsBetweenPresents,MsBetweenDisplayChange,MsInPresentAPI,MsUntilRenderComplete,
							// MsUntilDisplayed,Motherboard,OS,Processor,System RAM,Base Driver Version,
							// Driver Package,GPU #,GPU,GPU Core Clock (MHz),GPU Memory Clock (MHz),GPU Memory (MB)

							var columnHeader = _lines[0].Split(',');

							var infosCharList = new List<char>();
							var line = _lines[1];

							int isInner = -1;
							for (int i = 0; i < line.Length; i++)
							{
								if (line[i] == '"')
									isInner *= -1;

								if (!(line[i] == ',' && isInner == 1))
									infosCharList.Add(line[i]);

							}
							var infos = new string(infosCharList.ToArray()).Split(',');

							var lastDataSet = _lines.Last().Split(',');

							infoKeyValueDictionary.Add("Id", FullPath);
							infoKeyValueDictionary.Add("GameName", infos[0]);
							infoKeyValueDictionary.Add("ProcessName", infos[0]);
							infoKeyValueDictionary.Add("CreationDate", fileInfo.LastWriteTime.ToString("yyyy-MM-dd"));
							infoKeyValueDictionary.Add("CreationTime", fileInfo.LastWriteTime.ToString("HH:mm:ss"));

							int timeInSecondsIndex = Array.IndexOf(columnHeader, "TimeInSeconds");
							int motherboardNameIndex = Array.IndexOf(columnHeader, "Motherboard");
							int osVersionIndex = Array.IndexOf(columnHeader, "OS");
							int processorNameIndex = Array.IndexOf(columnHeader, "Processor");
							int systemRamInfoIndex = Array.IndexOf(columnHeader, "System RAM");
							int baseDriverVersionIndex = Array.IndexOf(columnHeader, "Base Driver Version");
							int driverPackageNameIndex = Array.IndexOf(columnHeader, "Driver Package");
							int graphicCardNameIndex = Array.IndexOf(columnHeader, "GPU");
							int numberGPUsIndex = Array.IndexOf(columnHeader, "GPU #");
							int gPUCoreClockIndex = Array.IndexOf(columnHeader, "GPU Core Clock (MHz)");
							int gPUMemoryClockIndex = Array.IndexOf(columnHeader, "GPU Memory Clock (MHz)");
							int gPUMemoryIndex = Array.IndexOf(columnHeader, "GPU Memory (MB)");
							int commentIndex = Array.IndexOf(columnHeader, "Comment");

							if (timeInSecondsIndex > -1 && timeInSecondsIndex < lastDataSet.Length)
								infoKeyValueDictionary.Add("RecordTime", lastDataSet[timeInSecondsIndex]);
							if (motherboardNameIndex > -1 && motherboardNameIndex < infos.Length)
								infoKeyValueDictionary.Add("Motherboard", infos[motherboardNameIndex].Replace("\"", ""));
							if (osVersionIndex > -1 && osVersionIndex < infos.Length)
								infoKeyValueDictionary.Add("OS", infos[osVersionIndex].Replace("\"", ""));
							if (processorNameIndex > -1 && processorNameIndex < infos.Length)
								infoKeyValueDictionary.Add("Processor", infos[processorNameIndex].Replace("\"", ""));
							if (systemRamInfoIndex > -1 && systemRamInfoIndex < infos.Length)
								infoKeyValueDictionary.Add("System RAM", infos[systemRamInfoIndex].Replace("\"", ""));
							if (baseDriverVersionIndex > -1 && baseDriverVersionIndex < infos.Length)
								infoKeyValueDictionary.Add("BaseDriverVersion", infos[baseDriverVersionIndex].Replace("\"", ""));
							if (driverPackageNameIndex > -1 && driverPackageNameIndex < infos.Length)
								infoKeyValueDictionary.Add("Base Driver Version", infos[driverPackageNameIndex].Replace("\"", ""));
							if (graphicCardNameIndex > -1 && graphicCardNameIndex < infos.Length)
								infoKeyValueDictionary.Add("GPU", infos[graphicCardNameIndex].Replace("\"", ""));
							if (numberGPUsIndex > -1 && numberGPUsIndex < infos.Length)
								infoKeyValueDictionary.Add("GPU #", infos[numberGPUsIndex].Replace("\"", ""));
							if (gPUCoreClockIndex > -1 && gPUCoreClockIndex < infos.Length)
								infoKeyValueDictionary.Add("GPU Core Clock (MHz)", infos[gPUCoreClockIndex].Replace("\"", ""));
							if (gPUMemoryClockIndex > -1 && gPUMemoryClockIndex < infos.Length)
								infoKeyValueDictionary.Add("GPU Memory Clock (MHz)", infos[gPUMemoryClockIndex].Replace("\"", ""));
							if (gPUMemoryIndex > -1 && gPUMemoryIndex < infos.Length)
								infoKeyValueDictionary.Add("GPU Memory (MB)", infos[gPUMemoryIndex].Replace("\"", ""));
							if (commentIndex > -1 && commentIndex < infos.Length)
								infoKeyValueDictionary.Add("Comment", infos[commentIndex].Replace("\"", ""));
						}

						SetInfoProperties(infoKeyValueDictionary);

						// set search string info
						CombinedInfo = $"{GameName} {ProcessName} {Comment}";

						// Free record data
						_lines = null;
					}
					else
					{
						if (HasInfoHeader)
						{
							FillPropertyDictionary(infoKeyValueDictionary);
							SetInfoProperties(infoKeyValueDictionary);

							// set search string info
							CombinedInfo = $"{GameName} {ProcessName} {Comment}";

							// Free record data
							_lines = null;
						}
					}
				}
				else
				{
					IsValid = false;
				}
			}
			else
			{
				IsValid = false;
			}
		}

		private void FillPropertyDictionary(Dictionary<string, string> infoKeyValueDictionary)
		{
			int headerCount = 0;
			while (_lines[headerCount].Contains(HEADER_MARKER))
			{
				var currentLine = _lines[headerCount];
				var currentLineWithoutMarker = currentLine.Replace(HEADER_MARKER, "");
				var infoKeyValue = currentLineWithoutMarker.Split(INFO_SEPERATOR);
				infoKeyValueDictionary.Add(infoKeyValue[0], infoKeyValue[1]);
				headerCount++;
			}
		}

		private void SetInfoProperties(Dictionary<string, string> infoKeyValueDictionary)
		{
			if (infoKeyValueDictionary.Any())
			{
				if (infoKeyValueDictionary.Keys.Contains("Id"))
					Id = infoKeyValueDictionary["Id"];
				else
					Id = FullPath;

				if (infoKeyValueDictionary.Keys.Contains("GameName"))
					GameName = infoKeyValueDictionary["GameName"];

				if (infoKeyValueDictionary.Keys.Contains("ProcessName"))
					ProcessName = infoKeyValueDictionary["ProcessName"];

				if (infoKeyValueDictionary.Keys.Contains("CreationDate"))
					CreationDate = infoKeyValueDictionary["CreationDate"];

				if (infoKeyValueDictionary.Keys.Contains("CreationTime"))
					CreationTime = infoKeyValueDictionary["CreationTime"];

				if (infoKeyValueDictionary.Keys.Contains("RecordTime"))
					RecordTime = Convert.ToDouble(infoKeyValueDictionary["RecordTime"], CultureInfo.InvariantCulture);

				if (infoKeyValueDictionary.Keys.Contains("Motherboard"))
					MotherboardName = infoKeyValueDictionary["Motherboard"];

				if (infoKeyValueDictionary.Keys.Contains("OS"))
					OsVersion = infoKeyValueDictionary["OS"];

				if (infoKeyValueDictionary.Keys.Contains("Processor"))
					ProcessorName = infoKeyValueDictionary["Processor"];

				if (infoKeyValueDictionary.Keys.Contains("System RAM"))
					SystemRamInfo = infoKeyValueDictionary["System RAM"];

				if (infoKeyValueDictionary.Keys.Contains("Base Driver Version"))
					BaseDriverVersion = infoKeyValueDictionary["Base Driver Version"];

				if (infoKeyValueDictionary.Keys.Contains("Driver Package"))
					DriverPackage = infoKeyValueDictionary["Driver Package"];

				if (infoKeyValueDictionary.Keys.Contains("GPU #"))
					NumberGPUs = infoKeyValueDictionary["GPU #"];

				if (infoKeyValueDictionary.Keys.Contains("GPU"))
					GraphicCardName = infoKeyValueDictionary["GPU"];

				if (infoKeyValueDictionary.Keys.Contains("GPU Core Clock (MHz)"))
					GPUCoreClock = infoKeyValueDictionary["GPU Core Clock (MHz)"];

				if (infoKeyValueDictionary.Keys.Contains("GPU Memory Clock (MHz)"))
					GPUMemoryClock = infoKeyValueDictionary["GPU Memory Clock (MHz)"];

				if (infoKeyValueDictionary.Keys.Contains("GPU Memory (MB)"))
					GPUMemory = infoKeyValueDictionary["GPU Memory (MB)"];

				if (infoKeyValueDictionary.Keys.Contains("Comment"))
					Comment = infoKeyValueDictionary["Comment"];

				if (infoKeyValueDictionary.Keys.Contains("IsAggregated"))
					IsAggregated = infoKeyValueDictionary["IsAggregated"];
				else
					IsAggregated = "false";
			}
		}

		public static IFileRecordInfo Create(FileInfo fileInfo)
		{
			FileRecordInfo recordInfo = null;

			try
			{
				recordInfo = new FileRecordInfo(fileInfo);
			}
			catch (ArgumentException)
			{
				// Log
			}
			catch (Exception)
			{
				// Log
			}

			return recordInfo;
		}

		private bool GetIsValid(string[] _lines)
		{
			bool isValid = false;

			// first check file length
			if (HasInfoHeader)
			{
				if (_lines.Length > 40)
					isValid = true;
			}
			else
			{
				if (_lines.Length > 20)
					isValid = true;
			}

			if (isValid)
			{
				// check column header
				int headerCount = 0;
				if (HasInfoHeader)
					while (_lines[headerCount].Contains(HEADER_MARKER)) headerCount++;

				var columnHeaderLine = _lines[headerCount];

				isValid = columnHeaderLine.Contains("Application") &&
					columnHeaderLine.Contains("Dropped") &&
					columnHeaderLine.Contains("TimeInSeconds") &&
					columnHeaderLine.Contains("MsBetweenPresents") &&
					columnHeaderLine.Contains("MsBetweenDisplayChange");
			}

			return isValid;
		}

		private bool GetHasInfoHeader(string firstLine)
		{
			if (firstLine == null)
				return false;

			return firstLine.Contains(HEADER_MARKER);
		}
	}
}
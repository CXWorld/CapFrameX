using CapFrameX.Contracts.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CapFrameX.Data
{
    public class FileRecordInfo : IFileRecordInfo
    {
        public static string HEADER_MARKER = "//";

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
        public bool IsValid { get; private set; }
        public bool HasInfoHeader { get; private set; }

        private FileRecordInfo(FileInfo fileInfo)
        {
            if (fileInfo != null && File.Exists(fileInfo.FullName))
            {
                FileInfo = fileInfo;
                FullPath = fileInfo.FullName;
                _lines = File.ReadAllLines(fileInfo.FullName);

                if (_lines != null && _lines.Any())
                {
                    HasInfoHeader = GetHasInfoHeader(_lines[0]);
                    IsValid = GetIsValid(_lines);

                    if (IsValid)
                    {
                        Dictionary<string, string> infoKeyValueDictionary = new Dictionary<string, string>();

                        if (HasInfoHeader)
                        {
                            int headerCount = 0;
                            while (_lines[headerCount].Contains(HEADER_MARKER))
                            {
                                var currentLine = _lines[headerCount];
                                var currentLineWithoutMarker = currentLine.Replace(HEADER_MARKER, "");
                                var infoKeyValue = currentLineWithoutMarker.Split(':');
                                infoKeyValueDictionary.Add(infoKeyValue[0], infoKeyValue[1]);
                                headerCount++;
                            }
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
                            var infos = _lines[1].Split(',');
                            var lastDataSet = _lines.Last().Split(',');

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
                                infoKeyValueDictionary.Add("MotherboardName", infos[motherboardNameIndex]);
                            if (osVersionIndex > -1 && osVersionIndex < infos.Length)
                                infoKeyValueDictionary.Add("OsVersion", infos[osVersionIndex]);
                            if (processorNameIndex > -1 && processorNameIndex < infos.Length)
                                infoKeyValueDictionary.Add("ProcessorName", infos[processorNameIndex]);
                            if (systemRamInfoIndex > -1 && systemRamInfoIndex < infos.Length)
                                infoKeyValueDictionary.Add("SystemRamInfo", infos[systemRamInfoIndex]);
                            if (baseDriverVersionIndex > -1 && baseDriverVersionIndex < infos.Length)
                                infoKeyValueDictionary.Add("BaseDriverVersion", infos[baseDriverVersionIndex]);
                            if (driverPackageNameIndex > -1 && driverPackageNameIndex < infos.Length)
                                infoKeyValueDictionary.Add("DriverPackage", infos[driverPackageNameIndex]);
                            if (graphicCardNameIndex > -1 && graphicCardNameIndex < infos.Length)
                                infoKeyValueDictionary.Add("GraphicCardName", infos[graphicCardNameIndex]);
                            if (numberGPUsIndex > -1 && numberGPUsIndex < infos.Length)
                                infoKeyValueDictionary.Add("NumberGPUs", infos[numberGPUsIndex]);
                            if (gPUCoreClockIndex > -1 && gPUCoreClockIndex < infos.Length)
                                infoKeyValueDictionary.Add("GPUCoreClock", infos[gPUCoreClockIndex]);
                            if (gPUMemoryClockIndex > -1 && gPUMemoryClockIndex < infos.Length)
                                infoKeyValueDictionary.Add("GPUMemoryClock", infos[gPUMemoryClockIndex]);
                            if (gPUMemoryIndex > -1 && gPUMemoryIndex < infos.Length)
                                infoKeyValueDictionary.Add("GPUMemory", infos[gPUMemoryIndex]);
                            if (commentIndex > -1 && commentIndex < infos.Length)
                                infoKeyValueDictionary.Add("Comment", infos[commentIndex]);
                        }

                        SetInfoProperties(infoKeyValueDictionary);

                        // set search string info
                        CombinedInfo = $"{GameName} {ProcessName} {Comment}";

                        // Free record data
                        _lines = null;
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

        private void SetInfoProperties(Dictionary<string, string> infoKeyValueDictionary)
        {
            if (infoKeyValueDictionary.Any())
            {
                if (infoKeyValueDictionary.Keys.Contains(nameof(GameName)))
                    GameName = infoKeyValueDictionary[nameof(GameName)];

                if (infoKeyValueDictionary.Keys.Contains(nameof(ProcessName)))
                    ProcessName = infoKeyValueDictionary[nameof(ProcessName)];

                if (infoKeyValueDictionary.Keys.Contains(nameof(CreationDate)))
                    CreationDate = infoKeyValueDictionary[nameof(CreationDate)];

                if (infoKeyValueDictionary.Keys.Contains(nameof(CreationTime)))
                    CreationTime = infoKeyValueDictionary[nameof(CreationTime)];

                if (infoKeyValueDictionary.Keys.Contains(nameof(RecordTime)))
                    RecordTime = Convert.ToDouble(infoKeyValueDictionary[nameof(RecordTime)]);

                if (infoKeyValueDictionary.Keys.Contains(nameof(MotherboardName)))
                    MotherboardName = infoKeyValueDictionary[nameof(MotherboardName)];

                if (infoKeyValueDictionary.Keys.Contains(nameof(OsVersion)))
                    OsVersion = infoKeyValueDictionary[nameof(OsVersion)];

                if (infoKeyValueDictionary.Keys.Contains(nameof(ProcessorName)))
                    ProcessorName = infoKeyValueDictionary[nameof(ProcessorName)];

                if (infoKeyValueDictionary.Keys.Contains(nameof(SystemRamInfo)))
                    SystemRamInfo = infoKeyValueDictionary[nameof(SystemRamInfo)];

                if (infoKeyValueDictionary.Keys.Contains(nameof(BaseDriverVersion)))
                    BaseDriverVersion = infoKeyValueDictionary[nameof(BaseDriverVersion)];

                if (infoKeyValueDictionary.Keys.Contains(nameof(DriverPackage)))
                    DriverPackage = infoKeyValueDictionary[nameof(DriverPackage)];

                if (infoKeyValueDictionary.Keys.Contains(nameof(NumberGPUs)))
                    NumberGPUs = infoKeyValueDictionary[nameof(NumberGPUs)];

                if (infoKeyValueDictionary.Keys.Contains(nameof(GraphicCardName)))
                    GraphicCardName = infoKeyValueDictionary[nameof(GraphicCardName)];

                if (infoKeyValueDictionary.Keys.Contains(nameof(GPUCoreClock)))
                    GPUCoreClock = infoKeyValueDictionary[nameof(GPUCoreClock)];

                if (infoKeyValueDictionary.Keys.Contains(nameof(GPUMemoryClock)))
                    GPUMemoryClock = infoKeyValueDictionary[nameof(GPUMemoryClock)];

                if (infoKeyValueDictionary.Keys.Contains(nameof(GPUMemory)))
                    GPUMemory = infoKeyValueDictionary[nameof(GPUMemory)];

                if (infoKeyValueDictionary.Keys.Contains(nameof(Comment)))
                    Comment = infoKeyValueDictionary[nameof(Comment)];
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

            if (recordInfo == null)
                return null;

            if (!recordInfo.IsValid)
                return null;

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
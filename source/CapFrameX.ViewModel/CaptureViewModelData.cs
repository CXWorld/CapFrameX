using CapFrameX.Data;
using CapFrameX.PresentMonInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace CapFrameX.ViewModel
{
	public partial class CaptureViewModel
	{
		private void WriteCaptureDataToFile()
		{
			// explicit hook, only one process
			if (!string.IsNullOrWhiteSpace(SelectedProcessToCapture))
			{
				Task.Run(() => WriteExtractedCaptureDataToFile(SelectedProcessToCapture));
			}
			// auto hook with filtered process list
			else
			{
				var filter = CaptureServiceConfiguration.GetProcessIgnoreList();
				var process = ProcessesToCapture.FirstOrDefault();

				Task.Run(() => WriteExtractedCaptureDataToFile(process));
			}
		}

		private void WriteExtractedCaptureDataToFile(string processName)
		{
			if (string.IsNullOrWhiteSpace(processName))
				return;

			var adjustedCaptureData = GetAdjustedCaptureData(processName);

			if (adjustedCaptureData == null)
			{
				AddLoggerEntry("Error while extracting capture data. No file will be written.");
				return;
			}

			if (!adjustedCaptureData.Any())
			{
				AddLoggerEntry("Error while extracting capture data. Empty list. No file will be written.");
				return;
			}

			if (AppConfiguration.UseRunHistory)
			{
				Task.Factory.StartNew(() => _overlayService.AddRunToHistory(adjustedCaptureData));
			}

			StartFillArchive();

			Application.Current.Dispatcher.Invoke(new Action(() =>
			{
				// turn locking off 
				_dataOffsetRunning = false;
			}));

			// if aggregation mode is active and "Save aggregated result only" is checked, don't save single history items
			if (AppConfiguration.UseAggregation && AppConfiguration.SaveAggregationOnly)
				return;

			var filePath = _recordDataProvider.GetOutputFilename(processName);
			bool checkSave = _recordDataProvider.SavePresentData(adjustedCaptureData, filePath, processName);

			if (!checkSave)
				AddLoggerEntry("Error while saving capture data.");
			else
				AddLoggerEntry("Capture file is successfully written into directory.");
		}

		private List<string> GetAdjustedCaptureData(string processName)
		{
			if (!_captureData.Any())
				return Enumerable.Empty<string>().ToList();

			var startTimeWithOffset = RecordDataProvider.GetStartTimeFromDataLine(_captureData.First());
			var stopwatchTime = (_timestampStopCapture - _timestampStartCapture) / 1000d;

			if (string.IsNullOrWhiteSpace(CaptureTimeString))
			{
				CaptureTimeString = "0";
				AddLoggerEntry($"Wrong capture time string. Value will be set to default (0).");
			}

			var definedTime = Convert.ToInt32(CaptureTimeString);
			bool autoTermination = Convert.ToInt32(CaptureTimeString) > 0;

			if (autoTermination)
			{
				if (stopwatchTime < definedTime - 0.2 && stopwatchTime > 0)
					autoTermination = false;
			}

			var uniqueProcessIdDict = new Dictionary<string, HashSet<string>>();

			foreach (var filteredCaptureDataLine in _captureData)
			{
				var currentProcess = RecordDataProvider.GetProcessNameFromDataLine(filteredCaptureDataLine);
				var currentProcessId = RecordDataProvider.GetProcessIdFromDataLine(filteredCaptureDataLine);

				if (!uniqueProcessIdDict.ContainsKey(currentProcess))
				{
					var idHashSet = new HashSet<string>
					{
						currentProcessId
					};
					uniqueProcessIdDict.Add(currentProcess, idHashSet);
				}
				else
					uniqueProcessIdDict[currentProcess].Add(currentProcessId);
			}

			if (uniqueProcessIdDict.Any(dict => dict.Value.Count() > 1))
				AddLoggerEntry($"Multi instances detected. Capture data is not valid.");

			var filteredArchive = _captureDataArchive.Where(line =>
				{
					var currentProcess = RecordDataProvider.GetProcessNameFromDataLine(line);
					return currentProcess == processName && uniqueProcessIdDict[currentProcess].Count() == 1;
				}).ToList();
			var filteredCaptureData = _captureData.Where(line =>
				{
					var currentProcess = RecordDataProvider.GetProcessNameFromDataLine(line);
					return currentProcess == processName && uniqueProcessIdDict[currentProcess].Count() == 1;
				}).ToList();

			AddLoggerEntry($"Using archive with {filteredArchive.Count} frames.");

			if (!filteredArchive.Any())
			{
				AddLoggerEntry($"Empty archive. No file will be written.");
				return Enumerable.Empty<string>().ToList();
			}

			// Distinct archive and live stream
			var lastArchiveTime = RecordDataProvider.GetStartTimeFromDataLine(filteredArchive.Last());
			int distinctIndex = 0;
			for (int i = 0; i < filteredCaptureData.Count; i++)
			{
				if (RecordDataProvider.GetStartTimeFromDataLine(filteredCaptureData[i]) <= lastArchiveTime)
					distinctIndex++;
				else
					break;
			}

			if (distinctIndex == 0)
				return null;

			var unionCaptureData = filteredArchive.Concat(filteredCaptureData.Skip(distinctIndex)).ToList();
			var unionCaptureDataStartTime = RecordDataProvider.GetStartTimeFromDataLine(unionCaptureData.First());
			var unionCaptureDataEndTime = RecordDataProvider.GetStartTimeFromDataLine(unionCaptureData.Last());

			AddLoggerEntry($"Length captured data + archive in sec: " +
				$"{ Math.Round(unionCaptureDataEndTime - unionCaptureDataStartTime, 2)}");

			var captureInterval = new List<string>();

			double startTime = 0;

			// find first dataline that fits start of valid interval
			for (int i = 0; i < unionCaptureData.Count - 1; i++)
			{
				var currentQpcTime = RecordDataProvider.GetQpcTimeFromDataLine(unionCaptureData[i + 1]);

				if (currentQpcTime >= _qpcTimeStart)
				{
					startTime = RecordDataProvider.GetStartTimeFromDataLine(unionCaptureData[i]);
					break;
				}
			}

			if (startTime == 0)
			{
				AddLoggerEntry($"Start time is invalid. Error while evaluating QPCTime start.");
				return Enumerable.Empty<string>().ToList();
			}

			if (!autoTermination)
			{
				for (int i = 0; i < unionCaptureData.Count; i++)
				{
					var currentqpcTime = RecordDataProvider.GetQpcTimeFromDataLine(unionCaptureData[i]);
					var currentTime = RecordDataProvider.GetStartTimeFromDataLine(unionCaptureData[i]);

					if (currentqpcTime >= _qpcTimeStart && currentTime - startTime <= stopwatchTime)
						captureInterval.Add(unionCaptureData[i]);
				}

				if (!captureInterval.Any())
				{
					AddLoggerEntry($"Empty capture interval. Error while evaluating start and end time.");
					return Enumerable.Empty<string>().ToList();
				}
			}
			else
			{
				AddLoggerEntry($"Length captured data QPCTime start to end with buffer in sec: " +
					$"{ Math.Round(unionCaptureDataEndTime - startTime, 2)}");

				for (int i = 0; i < unionCaptureData.Count; i++)
				{
					var currentStartTime = RecordDataProvider.GetStartTimeFromDataLine(unionCaptureData[i]);

					if (currentStartTime >= startTime && currentStartTime - startTime <= definedTime)
						captureInterval.Add(unionCaptureData[i]);
				}
			}

			return captureInterval;
		}
	}
}

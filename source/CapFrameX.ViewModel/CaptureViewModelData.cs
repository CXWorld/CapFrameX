using CapFrameX.Contracts.Data;
using CapFrameX.PresentMonInterface;
using System;
using System.Collections.Generic;
using System.Globalization;
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
				Task.Run(() => WriteExtractedCaptureDataToFileAsync(SelectedProcessToCapture));
			}
			// auto hook with filtered process list
			else
			{
				var filter = CaptureServiceConfiguration.GetProcessIgnoreList();
				var process = ProcessesToCapture.FirstOrDefault();

				Task.Run(() => WriteExtractedCaptureDataToFileAsync(process));
			}
		}

		private async Task WriteExtractedCaptureDataToFileAsync(string processName)
		{
			if (string.IsNullOrWhiteSpace(processName))
				return;

			var adjustedCaptureData = GetAdjustedCaptureData(processName);
			// Skip first line to compensate the first frametime being one frame before original capture start point.
			var normalizedAdjustedCaptureData = NormalizeTimes(adjustedCaptureData.Skip(1));
			var sessionRun = _recordManager.ConvertPresentDataLinesToSessionRun(normalizedAdjustedCaptureData);

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
				await Task.Factory.StartNew(() => _overlayService.AddRunToHistory(sessionRun, processName));
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

			bool checkSave = await _recordManager.SaveSessionRunsToFile(new ISessionRun[] { sessionRun }, processName);

			if (!checkSave)
				AddLoggerEntry("Error while saving capture data.");
			else
				AddLoggerEntry("Capture file is successfully written into directory.");
		}

		private List<string> GetAdjustedCaptureData(string processName)
		{
			if (!_captureData.Any())
				return Enumerable.Empty<string>().ToList();

			var startTimeWithOffset = GetStartTimeFromDataLine(_captureData.First());
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
				var currentProcess = GetProcessNameFromDataLine(filteredCaptureDataLine);
				var currentProcessId = GetProcessIdFromDataLine(filteredCaptureDataLine);

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
					var currentProcess = GetProcessNameFromDataLine(line);
					return currentProcess == processName && uniqueProcessIdDict[currentProcess].Count() == 1;
				}).ToList();
			var filteredCaptureData = _captureData.Where(line =>
				{
					var currentProcess = GetProcessNameFromDataLine(line);
					return currentProcess == processName && uniqueProcessIdDict[currentProcess].Count() == 1;
				}).ToList();

			AddLoggerEntry($"Using archive with {filteredArchive.Count} frames.");

			if (!filteredArchive.Any())
			{
				AddLoggerEntry($"Empty archive. No file will be written.");
				return Enumerable.Empty<string>().ToList();
			}

			// Distinct archive and live stream
			var lastArchiveTime = GetStartTimeFromDataLine(filteredArchive.Last());
			int distinctIndex = 0;
			for (int i = 0; i < filteredCaptureData.Count; i++)
			{
				if (GetStartTimeFromDataLine(filteredCaptureData[i]) <= lastArchiveTime)
					distinctIndex++;
				else
					break;
			}

			if (distinctIndex == 0)
				return null;

			var unionCaptureData = filteredArchive.Concat(filteredCaptureData.Skip(distinctIndex)).ToList();
			var unionCaptureDataStartTime = GetStartTimeFromDataLine(unionCaptureData.First());
			var unionCaptureDataEndTime = GetStartTimeFromDataLine(unionCaptureData.Last());

			AddLoggerEntry($"Length captured data + archive in sec: " +
				$"{ Math.Round(unionCaptureDataEndTime - unionCaptureDataStartTime, 2)}");

			var captureInterval = new List<string>();

			double startTime = 0;

			// find first dataline that fits start of valid interval
			for (int i = 0; i < unionCaptureData.Count - 1; i++)
			{
				var currentQpcTime = GetQpcTimeFromDataLine(unionCaptureData[i + 1]);

				if (currentQpcTime >= _qpcTimeStart)
				{
					startTime = GetStartTimeFromDataLine(unionCaptureData[i]);
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
					var currentqpcTime = GetQpcTimeFromDataLine(unionCaptureData[i]);
					var currentTime = GetStartTimeFromDataLine(unionCaptureData[i]);

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
					var currentStartTime = GetStartTimeFromDataLine(unionCaptureData[i]);

					if (currentStartTime >= startTime && currentStartTime - startTime <= definedTime)
						captureInterval.Add(unionCaptureData[i]);
				}
			}

			return captureInterval;
		}

		private double GetStartTimeFromDataLine(string dataLine)
		{
			if (string.IsNullOrWhiteSpace(dataLine))
				return 0;

			var lineSplit = dataLine.Split(',');
			var startTime = lineSplit[11];

			return Convert.ToDouble(startTime, CultureInfo.InvariantCulture);
		}

		private string GetProcessNameFromDataLine(string dataLine)
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

		private string GetProcessIdFromDataLine(string dataLine)
		{
			if (string.IsNullOrWhiteSpace(dataLine))
				return null;

			var lineSplit = dataLine.Split(',');
			return lineSplit[1];
		}

		private long GetQpcTimeFromDataLine(string dataLine)
		{
			if (string.IsNullOrWhiteSpace(dataLine))
				return 0;

			var lineSplit = dataLine.Split(',');
			var qpcTime = lineSplit[17];

			return Convert.ToInt64(qpcTime, CultureInfo.InvariantCulture);
		}

		private IEnumerable<string> NormalizeTimes(IEnumerable<string> recordLines)
		{
			string firstDataLine = recordLines.First();
			var lines = new List<string>();
			//start time
			var timeStart = GetStartTimeFromDataLine(firstDataLine);

			// normalize time
			var currentLineSplit = firstDataLine.Split(',');
			currentLineSplit[11] = "0";

			lines.Add(string.Join(",", currentLineSplit));

			foreach (var dataLine in recordLines.Skip(1))
			{
				double currentStartTime = GetStartTimeFromDataLine(dataLine);

				// normalize time
				double normalizedTime = currentStartTime - timeStart;

				currentLineSplit = dataLine.Split(',');
				currentLineSplit[11] = normalizedTime.ToString(CultureInfo.InvariantCulture);

				lines.Add(string.Join(",", currentLineSplit));
			}
			return lines;
		}
	}
}

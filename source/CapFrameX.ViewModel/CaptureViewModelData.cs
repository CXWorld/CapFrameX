using CapFrameX.Data;
using CapFrameX.PresentMonInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

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

			var captureData = GetAdjustedCaptureData();
			StartFillArchive();

			if (captureData == null)
			{
				AddLoggerEntry("Error while extracting capture data. No file will be written.");
				return;
			}

			var filePath = GetOutputFilename(processName);
			int captureTime = Convert.ToInt32(CaptureTimeString);
			_recordDataProvider.SavePresentData(captureData, filePath, processName, captureTime);

			AddLoggerEntry("Capture file is successfully written into directory.");
		}

		private List<string> GetAdjustedCaptureData()
		{
			if (!_captureData.Any())
				return Enumerable.Empty<string>().ToList();

			var processName = RecordDataProvider.GetProcessNameFromDataLine(_captureData.First());
			var startTimeWithOffset = RecordDataProvider.GetStartTimeFromDataLine(_captureData.First());
			var stopwatchTime = (_timestampStopCapture - _timestampStartCapture) / 1000d;
			var definedTime = Convert.ToInt32(CaptureTimeString);
			bool autoTermination = Convert.ToInt32(CaptureTimeString) > 0;

			if (autoTermination)
			{
				if (stopwatchTime < definedTime - 0.2 && stopwatchTime > 0)
					autoTermination = false;
			}			

			var filteredArchive = _captureDataArchive.Where(line => RecordDataProvider.GetProcessNameFromDataLine(line) == processName).ToList();

			AddLoggerEntry($"Using archive with {filteredArchive.Count} frames.");

			// Distinct archive and live stream
			var lastArchiveTime = RecordDataProvider.GetStartTimeFromDataLine(filteredArchive.Last());
			int distinctIndex = 0;
			for (int i = 0; i < _captureData.Count; i++)
			{
				if (RecordDataProvider.GetStartTimeFromDataLine(_captureData[i]) <= lastArchiveTime)
					distinctIndex++;
				else
					break;
			}

			if (distinctIndex == 0)
				return null;

			var unionCaptureData = filteredArchive.Concat(_captureData.Skip(distinctIndex)).ToList();
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

			if (!autoTermination)
			{
				for (int i = 0; i < unionCaptureData.Count; i++)
				{
					var currentqpcTime = RecordDataProvider.GetQpcTimeFromDataLine(unionCaptureData[i]);
					var currentTime = RecordDataProvider.GetStartTimeFromDataLine(unionCaptureData[i]);

					if (currentqpcTime >= _qpcTimeStart && currentTime - startTime <= stopwatchTime)
						captureInterval.Add(unionCaptureData[i]);
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

			//if (!autoTermination)
			//{
			//	AddLoggerEntry($"Recording time (manual) in sec: " +
			//				Math.Round(stopwatchTime, 2).ToString(CultureInfo.InvariantCulture));
			//}
			//else
			//{
			//	AddLoggerEntry($"Recording time (auto) in sec: " +
			//				Math.Round(definedTime, 2).ToString(CultureInfo.InvariantCulture));
			//}

			return captureInterval;
		}
	}
}

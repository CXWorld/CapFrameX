using CapFrameX.Data;
using CapFrameX.PresentMonInterface;
using System;
using System.Collections.Generic;
using System.Globalization;
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
			var processName = RecordDataProvider.GetProcessNameFromDataLine(_captureData.First());
			var startTimeWithOffset = RecordDataProvider.GetStartTimeFromDataLine(_captureData.First());
			var captureTime = Convert.ToDouble(CaptureTimeString, CultureInfo.InvariantCulture);
			bool autoTermination = Convert.ToInt32(CaptureTimeString) > 0;

			AddLoggerEntry($"Recording time (free run or time set) in sec: " +
							Math.Round(captureTime, 2).ToString(CultureInfo.InvariantCulture));

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

			var captureInterval = new List<string>();

			if (!autoTermination)
			{
				for (int i = 0; i < unionCaptureData.Count; i++)
				{
					var currentqpcTime = RecordDataProvider.GetQpcTimeFromDataLine(unionCaptureData[i]);

					if (currentqpcTime >= _qcpTimeStart && currentqpcTime <= _qcpTimeStop)
						captureInterval.Add(unionCaptureData[i]);
				}
			}
			else
			{
				double startTime = 0;

				// find first dataline that fits start of valid interval
				for (int i = 0; i < unionCaptureData.Count; i++)
				{
					var currentQpcTime = RecordDataProvider.GetQpcTimeFromDataLine(unionCaptureData[i]);

					if (currentQpcTime >= _qcpTimeStart)
					{
						startTime = RecordDataProvider.GetStartTimeFromDataLine(unionCaptureData[i]);
						break;
					}
				}

				for (int i = 0; i < unionCaptureData.Count; i++)
				{
					var currentStartTime = RecordDataProvider.GetStartTimeFromDataLine(unionCaptureData[i]);

					if (currentStartTime >= startTime && currentStartTime - startTime <= captureTime)
						captureInterval.Add(unionCaptureData[i]);
				}
			}

			return captureInterval;
		}
	}
}

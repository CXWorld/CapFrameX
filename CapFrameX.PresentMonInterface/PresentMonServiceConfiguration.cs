using CapFrameX.Contracts.PresentMonInterface;
using System;
using System.Linq;
using System.Collections.Generic;

namespace CapFrameX.PresentMonInterface
{
	public class PresentMonServiceConfiguration : ICaptureServiceConfiguration
	{
		public string ProcessName { get; set; }

		public string CaptureStartHotkey { get; set; } = "F11";

		/// <summary>
		/// verbose or simple
		/// </summary>
		public string OutputLevelofDetail { get; set; } = "verbose";

		public bool CaptureAllProcesses { get; set; } = false;

		public string OutputFilename { get; set; }

		public int CaptureTimeSeconds { get; set; }

		public int CaptureDelaySeconds { get; set; }

		public List<string> ExcludeProcesses { get; set; }

		public string ConfigParameterToArguments()
		{
			if (string.IsNullOrWhiteSpace(ProcessName))
			{
				throw new ArgumentException("Output filename must be set!");
			}

			if (!CaptureAllProcesses && string.IsNullOrWhiteSpace(ProcessName))
			{
				throw new ArgumentException("Process name must be set!");
			}

			var arguments = string.Empty;
			if (CaptureAllProcesses)
			{
				arguments += "-captureall";
				arguments += " ";
				arguments += "-multi_csv";
				arguments += " ";
				arguments += "-output_file";
				arguments += " ";
				arguments += OutputFilename;
				//if (!string.IsNullOrWhiteSpace(CaptureStartHotkey))
				//{
				//	arguments += " ";
				//	arguments += "-hotkey";
				//	arguments += " ";
				//	arguments += CaptureStartHotkey;
				//}
				if (!string.IsNullOrWhiteSpace(OutputLevelofDetail))
				{
					arguments += " ";
					arguments += "-" + OutputLevelofDetail;
				}
				if (CaptureTimeSeconds > 0)
				{
					arguments += " ";
					arguments += "-timed";
					arguments += " ";
					arguments += CaptureTimeSeconds;
				}
				if (CaptureDelaySeconds > 0)
				{
					arguments += " ";
					arguments += "-delay ";
					arguments += " ";
					arguments += CaptureDelaySeconds;
				}
				if (ExcludeProcesses != null && ExcludeProcesses.Any())
				{
					arguments += " ";
					foreach (var process in ExcludeProcesses)
					{
						arguments += "-exclude";
						arguments += " ";
						arguments += process;
					}
				}
			}
			else
			{
				arguments += "-process_name";
				arguments += " ";
				arguments += ProcessName;
				arguments += " ";
				arguments += "-output_file";
				arguments += " ";
				arguments += OutputFilename;
				//if (!string.IsNullOrWhiteSpace(CaptureStartHotkey))
				//{
				//	arguments += " ";
				//	arguments += "-hotkey";
				//	arguments += " ";
				//	arguments += CaptureStartHotkey;
				//}
				if (!string.IsNullOrWhiteSpace(OutputLevelofDetail))
				{
					arguments += " ";
					arguments += "-" + OutputLevelofDetail;
				}
				if (CaptureTimeSeconds > 0)
				{
					arguments += " ";
					arguments += "-timed";
					arguments += " ";
					arguments += CaptureTimeSeconds;
				}
				if (CaptureDelaySeconds > 0)
				{
					arguments += " ";
					arguments += "-delay ";
					arguments += " ";
					arguments += CaptureDelaySeconds;
				}
			}

			return arguments;
		}
	}
}

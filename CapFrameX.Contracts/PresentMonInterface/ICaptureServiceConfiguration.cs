using System.Collections.Generic;

namespace CapFrameX.Contracts.PresentMonInterface
{
	public interface ICaptureServiceConfiguration
	{
		string ProcessName { get; }

		string CaptureStartHotkey { get; }

		string OutputLevelofDetail { get; }

		bool CaptureAllProcesses { get; }

		string OutputFilename { get; }

		int CaptureTimeSeconds { get; }

		int CaptureDelaySeconds { get; }

		List<string> ExcludeProcesses { get; }

		string ConfigParameterToArguments();
	}
}

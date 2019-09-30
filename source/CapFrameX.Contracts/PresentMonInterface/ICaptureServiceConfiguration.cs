using System.Collections.Generic;

namespace CapFrameX.Contracts.PresentMonInterface
{
	public interface ICaptureServiceConfiguration
	{
		string ProcessName { get; }

		string OutputLevelofDetail { get; }

		bool CaptureAllProcesses { get; }

		string OutputFilename { get; }

        bool RedirectOutputStream { get; }

        List<string> ExcludeProcesses { get; }

		string ConfigParameterToArguments();
	}
}

using System;
using System.Collections.Generic;

namespace CapFrameX.Contracts.PresentMonInterface
{
	public interface ICaptureService
	{
		IObservable<string> RedirectedStandardOutputStream { get; }

		bool StartCaptureService(IServiceStartInfo startinfo);

		bool StopCaptureService();

		IEnumerable<string> GetAllFilteredProcesses(HashSet<string> filter);
	}
}
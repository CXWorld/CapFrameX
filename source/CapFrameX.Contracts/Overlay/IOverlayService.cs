using CapFrameX.Data.Session.Contracts;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace CapFrameX.Contracts.Overlay
{
	public interface IOverlayService
	{
		ISubject<bool> IsOverlayActiveStream { get; }

		string SecondMetric { get; set; }

		string ThirdMetric { get; set; }

		int RunHistoryCount { get; }

		void UpdateNumberOfRuns(int numberOfRuns);

		void SetCaptureTimerValue(int t);

		void StartCountdown(double seconds);

		void StartCaptureTimer();

		void StopCaptureTimer();

		void SetCaptureServiceStatus(string status);

		void SetShowRunHistory(bool showHistory);

		void ResetHistory();

		void AddRunToHistory(ISessionRun captureData, string process, string recordDirectory);

		
	}
}

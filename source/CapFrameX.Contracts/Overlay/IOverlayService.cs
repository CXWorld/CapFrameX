using System.Reactive.Subjects;

namespace CapFrameX.Contracts.Overlay
{
	public interface IOverlayService
	{
		Subject<bool> IsOverlayActiveStream { get; }

		void ShowOverlay();

		void HideOverlay();

		void UpdateRefreshRate(int milliSeconds);

		void SetCaptureTimerValue(int t);

		void StartCountdown(int seconds);

		void StartCaptureTimer();

		void StopCaptureTimer();

		void SetCaptureServiceStatus(string status);

		void SetShowRunHistory(bool showHistory);

		void SetRunHistory(string[] runHistory);

		void ResetHistory();
	}
}

using CapFrameX.Data.Session.Contracts;
using System;
using System.Reactive.Subjects;

namespace CapFrameX.Contracts.Overlay
{
	public interface IOverlayService
	{
		ISubject<bool> IsOverlayActiveStream { get; }

		IObservable<IOverlayEntry[]> OnDictionaryUpdated { get; }

		string SecondMetric { get; set; }

		string ThirdMetric { get; set; }

		int RunHistoryCount { get; }

		void UpdateNumberOfRuns(int numberOfRuns);

		void SetCaptureTimerValue(int t);

		void StartCountdown(double seconds);

		void SetDelayCountdown(double seconds);

		void StartCaptureTimer();

		void StopCaptureTimer();

		void SetCaptureServiceStatus(string status);

		void ResetHistory();

		void AddRunToHistory(ISessionRun captureData, string process, string recordDirectory);

		IOverlayEntry GetSensorOverlayEntry(string identifier);
	}
}

using CapFrameX.Data.Session.Contracts;
using System;
using System.Collections.Generic;
using System.Reactive.Subjects;

namespace CapFrameX.Contracts.Overlay
{
	public interface IOverlayService
	{
		ISubject<bool> IsOverlayActiveStream { get; }

		/// <summary>Published after CurrentOverlayEntries contains the processed display list.</summary>
		IObservable<IOverlayEntry[]> OnDictionaryUpdated { get; }

		void RequestRefresh();

		string SecondMetric { get; set; }

		string ThirdMetric { get; set; }

		int RunHistoryCount { get; }

		IReadOnlyList<string> RunHistory { get; }

		IReadOnlyList<bool> RunHistoryOutlierFlags { get; }

		string RunHistoryAggregation { get; }

		void UpdateNumberOfRuns(int numberOfRuns);

		void SetCaptureTimerValue(int t);

		void StartCountdown(double seconds);

		void SetDelayCountdown(double seconds);

		void CancelDelayCountdown();

		void StartCaptureTimer();

		void StopCaptureTimer();

		void SetCaptureServiceStatus(string status);

		void ResetHistory();

		void AddRunToHistory(ISessionRun captureData, string process, string recordDirectory);

		void ShutdownOverlayService();

		IOverlayEntry GetSensorOverlayEntry(string identifier);
		IOverlayEntry[] CurrentOverlayEntries { get; }
		Action<IOverlayEntry[]> OSDUpdateNotifier { get; set; }
	}
}

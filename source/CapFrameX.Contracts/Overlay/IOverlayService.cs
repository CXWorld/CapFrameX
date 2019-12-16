using System.Collections.Generic;
using System.Reactive.Subjects;

namespace CapFrameX.Contracts.Overlay
{
	public interface IOverlayService
	{
		Subject<bool> IsOverlayActiveStream { get; }

		void ShowOverlay();

		void ReleaseOverlay();

		void SetOverlayHeader(IList<string> entries);

		void StartCountDown(int seconds);

		void StartTimer();

		void StopTimer();
	}
}

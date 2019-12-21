using System.Collections.Generic;
using System.Reactive.Subjects;

namespace CapFrameX.Contracts.Overlay
{
	public interface IOverlayService
	{
		Subject<bool> IsOverlayActiveStream { get; }

		void Refresh();

		void ReleaseOSD();

		void ShowOverlay();

		void HideOverlay();

		void UpdateRefreshRate(int milliSeconds);

		void StartCountdown(int seconds);
	}
}

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
	}
}

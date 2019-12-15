using CapFrameX.Contracts.Overlay;
using System.Reactive.Subjects;

namespace CapFrameX.Overlay
{
	public class OverlayService : RTSSCSharpWrapper, IOverlayService
	{
		public Subject<bool> IsOverlayActiveStream { get; }

		public OverlayService() : base()
		{
			IsOverlayActiveStream = new Subject<bool>();
		}
	}
}

using CapFrameX.Contracts.Overlay;
using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;

namespace CapFrameX.Overlay
{
	public class OverlayService : RTSSCSharpWrapper, IOverlayService
	{
		private IDisposable _disposableHeartBeat;

		public double RefreshPeriod { get; }

		public Subject<bool> IsOverlayActiveStream { get; }

		public OverlayService() : base()
		{
			// default 1 second
			RefreshPeriod = 1;
			IsOverlayActiveStream = new Subject<bool>();
		}

		public void ShowOverlay()
		{
			_disposableHeartBeat = GetOverlayRefreshHeartBeat();
		}

		public void HideOverlay()
		{
			_disposableHeartBeat?.Dispose();
			ReleaseOSD();
		}

		private IDisposable GetOverlayRefreshHeartBeat()
		{
			var context = SynchronizationContext.Current;
			return Observable.Generate(0, // dummy initialState
										x => true, // dummy condition
										x => x, // dummy iterate
										x => x, // dummy resultSelector
										x => TimeSpan.FromSeconds(RefreshPeriod))
										.ObserveOn(context)
										.SubscribeOn(context)
										.Subscribe(x => Refresh());
		}
	}
}

using System;

namespace CapFrameX.Contracts.Overlay
{
	/// <summary>
	/// Tracks whether a remote API client reads the overlay entries (/api/osd, /ws/osd). While one
	/// does, the entries and the sensors they show keep refreshing with the overlay switched off
	/// (ALT+O); the renderers still follow IsOverlayActive alone.
	/// </summary>
	public interface IRemoteOverlayDemand
	{
		bool IsActive { get; }

		/// <summary>The current state on subscription, then every change.</summary>
		IObservable<bool> IsActiveStream { get; }

		/// <summary>An HTTP read. Polling clients never disconnect, so each read holds the demand for a lease period.</summary>
		void RegisterRequest();

		/// <summary>A streaming client holds the demand until the returned handle is disposed.</summary>
		IDisposable AcquireStreamingClient();
	}
}

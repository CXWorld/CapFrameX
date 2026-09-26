using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;

namespace CapFrameX.ViewModel
{
	/// <summary>
	/// The one way the UI switches the overlay on or off: the Overlay items and OSD options
	/// toggles, the status bar toggle and the overlay hotkey. All of them refuse the same
	/// activations and publish through the same stream, which every toggle follows.
	/// </summary>
	internal static class OverlayActivation
	{
		/// <summary>
		/// Missing renderer dependencies may prevent activation, but they must never trap a stale
		/// active state. In particular, a portable configuration can still contain
		/// IsOverlayActive=true after being moved to a machine without RTSS.
		/// </summary>
		internal static bool CanSet(bool requestedActive, bool isRTSSInstalled,
			bool enableHookFreeOverlay, bool enableHookOverlay)
		{
			return !requestedActive || isRTSSInstalled || enableHookFreeOverlay || enableHookOverlay;
		}

		/// <summary>
		/// Stores and publishes the requested state. Returns false when no renderer can show it.
		/// </summary>
		internal static bool TrySet(IAppConfiguration appConfiguration, IOverlayService overlayService,
			bool isRTSSInstalled, bool active)
		{
			if (!CanSet(active, isRTSSInstalled, appConfiguration.EnableHookFreeOverlay,
				appConfiguration.EnableHookOverlay))
				return false;

			appConfiguration.IsOverlayActive = active;
			overlayService.IsOverlayActiveStream.OnNext(active);
			return true;
		}
	}
}

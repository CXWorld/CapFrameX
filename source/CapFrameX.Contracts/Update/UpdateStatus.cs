using CapFrameX.Contracts.Localization;

namespace CapFrameX.Contracts.Update
{
	/// <summary>
	/// Immutable snapshot of the update service, pushed through <see cref="IUpdateService.StatusStream"/>.
	/// </summary>
	public class UpdateStatus
	{
		public EUpdateState State { get; }

		/// <summary>
		/// The offered package. Null unless the state is <see cref="EUpdateState.UpdateAvailable"/>,
		/// <see cref="EUpdateState.Downloading"/> or <see cref="EUpdateState.ReadyToInstall"/>.
		/// </summary>
		public UpdatePackageInfo Package { get; }

		/// <summary>Download progress in the range 0..1. Only meaningful while downloading.</summary>
		public double Progress { get; }

		/// <summary>Human readable detail in English, e.g. why a check failed. May be null.</summary>
		public string Message { get; }

		/// <summary>
		/// The same detail as a catalog key plus arguments, for display in the interface
		/// language. May be null, in which case <see cref="Message"/> is shown as is.
		/// </summary>
		public LocalizedText LocalizedMessage { get; }

		public UpdateStatus(EUpdateState state, UpdatePackageInfo package = null,
			double progress = 0d, string message = null, LocalizedText localizedMessage = null)
		{
			State = state;
			Package = package;
			Progress = progress;
			Message = message;
			LocalizedMessage = localizedMessage;
		}
	}
}

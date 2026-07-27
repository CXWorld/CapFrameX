namespace CapFrameX.Contracts.Update
{
	/// <summary>
	/// Lifecycle of the update service. A staged package keeps the service in
	/// <see cref="ReadyToInstall"/> until the app is restarted.
	/// </summary>
	public enum EUpdateState
	{
		/// <summary>Nothing has been checked yet, or no update server is configured.</summary>
		Unknown,

		/// <summary>A manifest request is in flight.</summary>
		Checking,

		/// <summary>The installed version is the newest one the server offers.</summary>
		UpToDate,

		/// <summary>The server offers a newer package that has not been downloaded yet.</summary>
		UpdateAvailable,

		/// <summary>The package is being downloaded.</summary>
		Downloading,

		/// <summary>The package is downloaded and verified. It is installed on the next app start.</summary>
		ReadyToInstall,

		/// <summary>The check or the download failed; <see cref="UpdateStatus.Message"/> holds the reason.</summary>
		Failed
	}
}

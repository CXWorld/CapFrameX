using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CapFrameX.Contracts.Update
{
	/// <summary>
	/// Queries the CapFrameX update server, downloads the offered installer package and stages it
	/// so the next app start can hand it to the installer.
	/// </summary>
	public interface IUpdateService
	{
		/// <summary>True when an update server URI is configured. Everything is inert without one.</summary>
		bool IsConfigured { get; }

		/// <summary>The oldest version the client will deliberately install.</summary>
		Version MinimumRollbackVersion { get; }

		/// <summary>Validated packages returned by the last successful catalog request, newest first.</summary>
		IReadOnlyList<UpdatePackageInfo> AvailablePackages { get; }

		UpdateStatus CurrentStatus { get; }

		/// <summary>Replays the current status to every new subscriber.</summary>
		IObservable<UpdateStatus> StatusStream { get; }

		/// <summary>
		/// Fetches the manifest and compares it against the running version. Never throws -
		/// failures surface as <see cref="EUpdateState.Failed"/> in the returned status.
		/// </summary>
		Task<UpdateStatus> CheckForUpdateAsync(CancellationToken cancellationToken = default);

		/// <summary>
		/// Selects a version from <see cref="AvailablePackages"/> for installation. Selecting an older
		/// version is an explicit rollback; versions below <see cref="MinimumRollbackVersion"/> and
		/// versions absent from the validated catalog are rejected.
		/// </summary>
		UpdateStatus SelectVersion(Version version);

		/// <summary>
		/// Downloads the package offered by the last check, verifies it and marks it pending, so
		/// that <c>UpdateInstaller.TryStartPendingUpdate</c> picks it up on the next app start.
		/// Never throws - failures surface as <see cref="EUpdateState.Failed"/>.
		/// </summary>
		Task<UpdateStatus> DownloadUpdateAsync(CancellationToken cancellationToken = default);

		/// <summary>Aborts a running download. The partially downloaded file is discarded.</summary>
		void CancelDownload();
	}
}

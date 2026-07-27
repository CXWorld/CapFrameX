using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Update;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace CapFrameX.Updater
{
	/// <summary>
	/// Talks to the CapFrameX update server: reads the manifest, compares it against the running
	/// version and, on demand, downloads the installer package into the updates folder and marks it
	/// pending. Installing it is <see cref="UpdateInstaller"/>'s job on the next app start, because
	/// the installer replaces the files of the running app.
	/// </summary>
	public class UpdateService : IUpdateService
	{
		private static readonly TimeSpan ManifestTimeout = TimeSpan.FromSeconds(15);
		private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(30);
		private const int DownloadBufferSize = 81920;

		private readonly Uri _manifestUri;
		private readonly IAppVersionProvider _appVersionProvider;
		private readonly string _updatesFolder;
		private readonly ILogger<UpdateService> _logger;
		private readonly HttpClient _httpClient;
		private readonly BehaviorSubject<UpdateStatus> _statusSubject
			= new BehaviorSubject<UpdateStatus>(new UpdateStatus(EUpdateState.Unknown));
		// Serializes check and download: a manual check while a download runs would otherwise
		// overwrite the download status.
		private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

		private CancellationTokenSource _downloadCancellation;

		public bool IsConfigured => _manifestUri != null;

		public UpdateStatus CurrentStatus => _statusSubject.Value;

		public IObservable<UpdateStatus> StatusStream => _statusSubject.AsObservable();

		/// <param name="manifestUri">
		/// Absolute URI of the update manifest (see <see cref="UpdateManifest"/>). An empty value
		/// leaves the service inert, which is what ships until an update server is configured.
		/// </param>
		/// <param name="updatesFolder">Folder the package is staged in.</param>
		public UpdateService(string manifestUri, IAppVersionProvider appVersionProvider,
			string updatesFolder, ILogger<UpdateService> logger)
		{
			_appVersionProvider = appVersionProvider;
			_updatesFolder = updatesFolder;
			_logger = logger;

			if (!string.IsNullOrWhiteSpace(manifestUri)
				&& Uri.TryCreate(manifestUri.Trim(), UriKind.Absolute, out var parsedUri))
			{
				_manifestUri = parsedUri;

				if (!string.Equals(_manifestUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
					_logger.LogWarning("The update manifest is served over {scheme}. Use https so the manifest cannot be tampered with in transit.", _manifestUri.Scheme);
			}
			else if (!string.IsNullOrWhiteSpace(manifestUri))
			{
				_logger.LogError("'{manifestUri}' is not a valid update manifest URI. The update service stays disabled.", manifestUri);
			}

			// Downloads have no useful upper bound, so the per-request timeouts are driven by
			// linked cancellation tokens instead of the client-wide timeout.
			_httpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
			_httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CX_Client", "0"));
		}

		public async Task<UpdateStatus> CheckForUpdateAsync(CancellationToken cancellationToken = default)
		{
			if (!IsConfigured)
				return Publish(new UpdateStatus(EUpdateState.Unknown, message: "No update server is configured."));

			// Acquired without the caller's token: a cancelled token has to surface as a status,
			// not as an exception out of a method that promises never to throw.
			if (!await _gate.WaitAsync(0).ConfigureAwait(false))
				return CurrentStatus;

			try
			{
				Publish(new UpdateStatus(EUpdateState.Checking, message: "Looking for updates..."));

				UpdateManifest manifest;
				using (var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
				{
					timeoutCancellation.CancelAfter(ManifestTimeout);
					var json = await _httpClient.GetStringAsync(_manifestUri, timeoutCancellation.Token).ConfigureAwait(false);
					manifest = JsonConvert.DeserializeObject<UpdateManifest>(json);
				}

				var package = ToPackageInfo(manifest, out var rejectReason);

				if (package == null)
				{
					_logger.LogError("Rejected the update manifest from {manifestUri}: {reason}", _manifestUri, rejectReason);
					return Publish(new UpdateStatus(EUpdateState.Failed, message: rejectReason));
				}

				var appVersion = _appVersionProvider.GetAppVersion();

				if (Normalize(package.Version) <= Normalize(appVersion))
				{
					_logger.LogInformation("CapFrameX {appVersion} is up to date (server offers {serverVersion}).", appVersion, package.Version);
					return Publish(new UpdateStatus(EUpdateState.UpToDate, message: "CapFrameX is up to date."));
				}

				_logger.LogInformation("Update {updateVersion} is available (installed: {appVersion}).", package.Version, appVersion);
				return Publish(new UpdateStatus(EUpdateState.UpdateAvailable, package,
					message: $"Version {FormatVersion(package.Version)} is available."));
			}
			catch (OperationCanceledException)
			{
				return Publish(new UpdateStatus(EUpdateState.Unknown, message: "The update check was cancelled."));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Unable to check for updates at {manifestUri}.", _manifestUri);
				return Publish(new UpdateStatus(EUpdateState.Failed, message: "The update server could not be reached."));
			}
			finally
			{
				_gate.Release();
			}
		}

		public async Task<UpdateStatus> DownloadUpdateAsync(CancellationToken cancellationToken = default)
		{
			var package = CurrentStatus.Package;

			if (package == null)
				return Publish(new UpdateStatus(EUpdateState.Failed, message: "There is no update to download."));

			if (CurrentStatus.State == EUpdateState.ReadyToInstall)
				return CurrentStatus;

			if (!await _gate.WaitAsync(0).ConfigureAwait(false))
				return CurrentStatus;

			var fileName = GetPackageFileName(package);
			var targetPath = Path.Combine(_updatesFolder, fileName);
			var partialPath = targetPath + ".part";

			using (var downloadCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
			{
				_downloadCancellation = downloadCancellation;
				downloadCancellation.CancelAfter(DownloadTimeout);

				try
				{
					Directory.CreateDirectory(_updatesFolder);
					// A previously staged package is worthless once we start fetching a new one,
					// and the marker must not survive a download that ends up failing.
					PendingUpdate.Clear(_updatesFolder);
					DeleteIfExists(targetPath);
					DeleteIfExists(partialPath);

					Publish(new UpdateStatus(EUpdateState.Downloading, package, 0d, "Starting the download..."));

					var hash = await DownloadToFileAsync(package, partialPath, downloadCancellation.Token)
						.ConfigureAwait(false);

					if (!string.IsNullOrWhiteSpace(package.Sha256) && !PackageIntegrity.HashMatches(package.Sha256, hash))
					{
						_logger.LogError("The downloaded package has checksum {actual} but the manifest promised {expected}.", hash, package.Sha256);
						DeleteIfExists(partialPath);
						return Publish(new UpdateStatus(EUpdateState.Failed, package,
							message: "The downloaded update failed its checksum check and was discarded."));
					}

					File.Move(partialPath, targetPath);

					new PendingUpdate
					{
						Version = package.Version.ToString(),
						PackageFile = fileName,
						// Fall back to what we computed, so the package is still verified before it
						// is executed even when the manifest carries no checksum.
						Sha256 = string.IsNullOrWhiteSpace(package.Sha256) ? hash : package.Sha256,
						Arguments = package.InstallerArguments,
						StagedUtc = DateTime.UtcNow
					}.Save(_updatesFolder);

					_logger.LogInformation("Staged the update package for CapFrameX {updateVersion} at {targetPath}.", package.Version, targetPath);
					return Publish(new UpdateStatus(EUpdateState.ReadyToInstall, package, 1d,
						$"Version {FormatVersion(package.Version)} is downloaded and will be installed the next time CapFrameX starts."));
				}
				catch (OperationCanceledException)
				{
					DeleteIfExists(partialPath);
					_logger.LogInformation("The update download was cancelled.");
					return Publish(new UpdateStatus(EUpdateState.UpdateAvailable, package,
						message: "The download was cancelled."));
				}
				catch (Exception ex)
				{
					DeleteIfExists(partialPath);
					_logger.LogError(ex, "Unable to download the update package from {packageUri}.", package.PackageUri);
					return Publish(new UpdateStatus(EUpdateState.Failed, package,
						message: "The update package could not be downloaded."));
				}
				finally
				{
					_downloadCancellation = null;
					_gate.Release();
				}
			}
		}

		public void CancelDownload()
		{
			try
			{
				_downloadCancellation?.Cancel();
			}
			catch (ObjectDisposedException)
			{
				// The download finished between the null check and the call - nothing to cancel.
			}
		}

		/// <summary>Streams the package to disk and returns its hex encoded SHA-256.</summary>
		private async Task<string> DownloadToFileAsync(UpdatePackageInfo package, string partialPath,
			CancellationToken cancellationToken)
		{
			using (var response = await _httpClient
				.GetAsync(package.PackageUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
				.ConfigureAwait(false))
			{
				response.EnsureSuccessStatusCode();

				var totalBytes = response.Content.Headers.ContentLength ?? package.SizeInBytes;

				using (var sha256 = SHA256.Create())
				using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
				using (var target = new FileStream(partialPath, FileMode.Create, FileAccess.Write,
					FileShare.None, DownloadBufferSize, useAsync: true))
				{
					var buffer = new byte[DownloadBufferSize];
					long receivedBytes = 0;
					var lastReportedPercent = -1;
					int read;

					while ((read = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
					{
						await target.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
						sha256.TransformBlock(buffer, 0, read, null, 0);
						receivedBytes += read;

						if (totalBytes <= 0)
							continue;

						// One update per whole percent keeps the UI responsive without flooding it.
						var percent = (int)(100L * receivedBytes / totalBytes);

						if (percent == lastReportedPercent)
							continue;

						lastReportedPercent = percent;
						Publish(new UpdateStatus(EUpdateState.Downloading, package, percent / 100d,
							$"Downloading version {FormatVersion(package.Version)}..."));
					}

					sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
					return PackageIntegrity.ToHex(sha256.Hash);
				}
			}
		}

		/// <summary>
		/// Validates the manifest and maps it onto the published contract. Returns null with a
		/// reason when the manifest cannot be acted on.
		/// </summary>
		private UpdatePackageInfo ToPackageInfo(UpdateManifest manifest, out string rejectReason)
		{
			rejectReason = null;

			if (manifest == null)
			{
				rejectReason = "The update server returned an empty manifest.";
				return null;
			}

			if (!Version.TryParse(manifest.Version, out var version))
			{
				rejectReason = $"The update manifest carries an unusable version '{manifest.Version}'.";
				return null;
			}

			if (manifest.Package == null || string.IsNullOrWhiteSpace(manifest.Package.Url))
			{
				rejectReason = "The update manifest names no package.";
				return null;
			}

			// A relative url is resolved against the manifest, so the server can move without
			// every package url having to be rewritten.
			if (!Uri.TryCreate(_manifestUri, manifest.Package.Url, out var packageUri))
			{
				rejectReason = $"The update manifest carries an unusable package url '{manifest.Package.Url}'.";
				return null;
			}

			if (!PackageIntegrity.HasAllowedExtension(packageUri.LocalPath))
			{
				rejectReason = "The update package is neither an .exe nor an .msi installer.";
				return null;
			}

			if (string.IsNullOrWhiteSpace(manifest.Package.Sha256))
				_logger.LogWarning("The update manifest carries no sha256 for {packageUri}. The download cannot be verified against the server.", packageUri);

			DateTime? releaseDate = null;
			if (DateTime.TryParse(manifest.ReleaseDate, out var parsedReleaseDate))
				releaseDate = parsedReleaseDate;

			Uri releaseNotesUri = null;
			if (!string.IsNullOrWhiteSpace(manifest.ReleaseNotesUrl))
				Uri.TryCreate(_manifestUri, manifest.ReleaseNotesUrl, out releaseNotesUri);

			return new UpdatePackageInfo
			{
				Version = version,
				ReleaseDate = releaseDate,
				Summary = manifest.Summary,
				Highlights = manifest.Highlights,
				PackageUri = packageUri,
				Sha256 = manifest.Package.Sha256,
				SizeInBytes = manifest.Package.Size,
				InstallerArguments = manifest.Package.Arguments,
				ReleaseNotesUri = releaseNotesUri
			};
		}

		private string GetPackageFileName(UpdatePackageInfo package)
		{
			var fileName = Path.GetFileName(package.PackageUri.LocalPath);

			// The manifest is remote input; never let it decide where on disk we write.
			foreach (var invalidChar in Path.GetInvalidFileNameChars())
				fileName = fileName.Replace(invalidChar, '_');

			return PackageIntegrity.HasAllowedExtension(fileName)
				? fileName
				: $"CapFrameX-{FormatVersion(package.Version)}-setup.exe";
		}

		private UpdateStatus Publish(UpdateStatus status)
		{
			_statusSubject.OnNext(status);
			return status;
		}

		private void DeleteIfExists(string path)
		{
			try
			{
				if (File.Exists(path))
					File.Delete(path);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Unable to remove {path}.", path);
			}
		}

		/// <summary>
		/// The assembly version has four components while manifests usually name three, and
		/// <see cref="Version"/> sorts an unset component below zero. Comparing on the three
		/// components CapFrameX actually releases on avoids "1.9.1" looking older than "1.9.1.0".
		/// </summary>
		private static Version Normalize(Version version)
			=> version == null
				? new Version(0, 0, 0)
				: new Version(version.Major, version.Minor, Math.Max(version.Build, 0));

		private static string FormatVersion(Version version)
			=> Normalize(version).ToString();
	}
}

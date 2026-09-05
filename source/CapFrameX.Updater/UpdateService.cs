using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Update;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CapFrameX.Updater
{
	/// <summary>
	/// Reads the isolated update server's release catalog, offers explicit version selection and
	/// stages a verified installer for <see cref="UpdateInstaller"/>. The same path handles forward
	/// updates and user-confirmed rollbacks, but never permits a target older than 1.9.0.0.
	/// </summary>
	public class UpdateService : IUpdateService
	{
		private static readonly TimeSpan CatalogTimeout = TimeSpan.FromSeconds(15);
		private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(30);
		private const int DownloadBufferSize = 81920;
		private const int MaximumCatalogBytes = 2 * 1024 * 1024;
		private const long MaximumPackageBytes = 2L * 1024 * 1024 * 1024;

		private readonly Uri _catalogUri;
		private readonly IAppVersionProvider _appVersionProvider;
		private readonly string _updatesFolder;
		private readonly ILogger<UpdateService> _logger;
		private readonly HttpClient _httpClient;
		private readonly BehaviorSubject<UpdateStatus> _statusSubject
			= new BehaviorSubject<UpdateStatus>(new UpdateStatus(EUpdateState.Unknown));
		private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

		private IReadOnlyList<UpdatePackageInfo> _availablePackages = Array.Empty<UpdatePackageInfo>();
		private CancellationTokenSource _downloadCancellation;

		public bool IsConfigured => _catalogUri != null;

		public Version MinimumRollbackVersion => UpdatePolicy.MinimumRollbackVersion;

		public IReadOnlyList<UpdatePackageInfo> AvailablePackages => _availablePackages;

		public UpdateStatus CurrentStatus => _statusSubject.Value;

		public IObservable<UpdateStatus> StatusStream => _statusSubject.AsObservable();

		/// <param name="catalogUri">Absolute HTTPS URI of the release catalog.</param>
		/// <param name="updatesFolder">Folder the verified installer is staged in.</param>
		public UpdateService(string catalogUri, IAppVersionProvider appVersionProvider,
			string updatesFolder, ILogger<UpdateService> logger)
		{
			_appVersionProvider = appVersionProvider;
			_updatesFolder = updatesFolder;
			_logger = logger;

			if (!string.IsNullOrWhiteSpace(catalogUri)
				&& Uri.TryCreate(catalogUri.Trim(), UriKind.Absolute, out var parsedUri)
				&& string.Equals(parsedUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
				&& string.IsNullOrEmpty(parsedUri.UserInfo))
			{
				_catalogUri = parsedUri;
			}
			else if (!string.IsNullOrWhiteSpace(catalogUri))
			{
				_logger.LogError("'{catalogUri}' is not a valid HTTPS update catalog URI. The update service stays disabled.", catalogUri);
			}

			_httpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
			{
				Timeout = Timeout.InfiniteTimeSpan
			};
			_httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CX_Client", "2"));
		}

		public async Task<UpdateStatus> CheckForUpdateAsync(CancellationToken cancellationToken = default)
		{
			if (!IsConfigured)
				return Publish(new UpdateStatus(EUpdateState.Unknown, message: "No update server is configured."));

			if (!await _gate.WaitAsync(0).ConfigureAwait(false))
				return CurrentStatus;

			try
			{
				Publish(new UpdateStatus(EUpdateState.Checking, message: "Loading available versions..."));

				UpdateCatalogManifest catalog;
				using (var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
				{
					timeoutCancellation.CancelAfter(CatalogTimeout);
					catalog = await DownloadCatalogAsync(timeoutCancellation.Token).ConfigureAwait(false);
				}

				var packages = ToPackageInfos(catalog, out var rejectReason);
				if (packages == null)
				{
					_logger.LogError("Rejected the update catalog from {catalogUri}: {reason}", _catalogUri, rejectReason);
					return Publish(new UpdateStatus(EUpdateState.Failed, message: rejectReason));
				}

				_availablePackages = packages;
				var currentVersion = _appVersionProvider.GetAppVersion();
				var currentChannel = _appVersionProvider.GetReleaseChannel();
				var latestPackage = GetLatestPackageForChannel(packages, currentChannel);

				if (latestPackage == null)
				{
					_logger.LogInformation("The catalog contains no {channel} package. {releaseCount} packages remain selectable.",
						currentChannel, packages.Count);
					return Publish(new UpdateStatus(EUpdateState.UpToDate,
						message: $"No {currentChannel} build is currently published. {packages.Count} other versions are available."));
				}

				if (UpdatePolicy.Normalize(latestPackage.Version) <= UpdatePolicy.Normalize(currentVersion))
				{
					_logger.LogInformation("CapFrameX {appVersion} is up to date on the {channel} channel. The catalog contains {releaseCount} selectable versions.",
						currentVersion, currentChannel, packages.Count);
					return Publish(new UpdateStatus(EUpdateState.UpToDate,
						message: $"CapFrameX is up to date on the {currentChannel} channel. {packages.Count} versions are available."));
				}

				_logger.LogInformation("{channel} update {updateVersion} is available (installed: {appVersion}).",
					latestPackage.Channel, latestPackage.Version, currentVersion);
				return Publish(new UpdateStatus(EUpdateState.UpdateAvailable, latestPackage,
					message: $"{latestPackage.Channel} version {UpdatePolicy.Format(latestPackage.Version)} is available."));
			}
			catch (OperationCanceledException)
			{
				return Publish(new UpdateStatus(EUpdateState.Unknown, message: "The update check was cancelled."));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Unable to read the update catalog at {catalogUri}.", _catalogUri);
				return Publish(new UpdateStatus(EUpdateState.Failed, message: "The update server could not be reached."));
			}
			finally
			{
				_gate.Release();
			}
		}

		public UpdateStatus SelectVersion(Version version)
		{
			if (version == null)
				return Publish(new UpdateStatus(EUpdateState.Failed, message: "No target version was selected."));

			if (!_gate.Wait(0))
				return CurrentStatus;

			try
			{
				if (UpdatePolicy.IsBelowRollbackFloor(version))
				{
					return Publish(new UpdateStatus(EUpdateState.Failed,
						message: $"Versions older than {UpdatePolicy.Format(MinimumRollbackVersion)} cannot be installed."));
				}

				var package = _availablePackages.FirstOrDefault(candidate =>
					UpdatePolicy.IsSameVersion(candidate.Version, version));

				if (package == null)
				{
					return Publish(new UpdateStatus(EUpdateState.Failed,
						message: $"Version {UpdatePolicy.Format(version)} is not present in the verified release catalog."));
				}

				var currentVersion = _appVersionProvider.GetAppVersion();
				if (UpdatePolicy.IsSameVersion(package.Version, currentVersion))
				{
					return Publish(new UpdateStatus(EUpdateState.UpToDate,
						message: $"Version {UpdatePolicy.Format(package.Version)} is already installed."));
				}

				var isRollback = UpdatePolicy.IsDowngrade(package.Version, currentVersion);
				return Publish(new UpdateStatus(EUpdateState.UpdateAvailable, package,
					message: isRollback
						? $"Rollback to version {UpdatePolicy.Format(package.Version)} is ready."
						: $"Version {UpdatePolicy.Format(package.Version)} is ready."));
			}
			finally
			{
				_gate.Release();
			}
		}

		public async Task<UpdateStatus> DownloadUpdateAsync(CancellationToken cancellationToken = default)
		{
			var package = CurrentStatus.Package;

			if (CurrentStatus.State == EUpdateState.ReadyToInstall && package != null)
				return CurrentStatus;

			if (CurrentStatus.State != EUpdateState.UpdateAvailable || package == null)
				return Publish(new UpdateStatus(EUpdateState.Failed, message: "There is no selected version to download."));

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
					PendingUpdate.Clear(_updatesFolder);
					DeleteIfExists(targetPath);
					DeleteIfExists(partialPath);

					Publish(new UpdateStatus(EUpdateState.Downloading, package, 0d, "Starting the download..."));

					var hash = await DownloadToFileAsync(package, partialPath, downloadCancellation.Token)
						.ConfigureAwait(false);

					if (!PackageIntegrity.HashMatches(package.Sha256, hash))
					{
						_logger.LogError("The downloaded package has checksum {actual} but the catalog promised {expected}.", hash, package.Sha256);
						DeleteIfExists(partialPath);
						return Publish(new UpdateStatus(EUpdateState.Failed, package,
							message: "The downloaded package failed its checksum check and was discarded."));
					}

					File.Move(partialPath, targetPath);
					var currentVersion = _appVersionProvider.GetAppVersion();
					var isRollback = UpdatePolicy.IsDowngrade(package.Version, currentVersion);

					new PendingUpdate
					{
						Version = UpdatePolicy.Format(package.Version),
						PackageFile = fileName,
						Sha256 = package.Sha256,
						Arguments = package.InstallerArguments,
						AllowDowngrade = isRollback,
						StagedUtc = DateTime.UtcNow
					}.Save(_updatesFolder);

					_logger.LogInformation("Staged CapFrameX {targetVersion} ({operation}) at {targetPath}.",
						package.Version, isRollback ? "rollback" : "update", targetPath);
					return Publish(new UpdateStatus(EUpdateState.ReadyToInstall, package, 1d,
						isRollback
							? $"Rollback to version {UpdatePolicy.Format(package.Version)} is downloaded and will start with the next CapFrameX launch."
							: $"Version {UpdatePolicy.Format(package.Version)} is downloaded and will be installed with the next CapFrameX launch."));
				}
				catch (OperationCanceledException)
				{
					DeleteIfExists(partialPath);
					_logger.LogInformation("The package download was cancelled.");
					return Publish(new UpdateStatus(EUpdateState.UpdateAvailable, package,
						message: "The download was cancelled."));
				}
				catch (Exception ex)
				{
					DeleteIfExists(partialPath);
					_logger.LogError(ex, "Unable to download the package from {packageUri}.", package.PackageUri);
					return Publish(new UpdateStatus(EUpdateState.Failed, package,
						message: "The selected package could not be downloaded."));
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
				// The download finished between the null check and cancellation.
			}
		}

		private async Task<UpdateCatalogManifest> DownloadCatalogAsync(CancellationToken cancellationToken)
		{
			using (var response = await _httpClient
				.GetAsync(_catalogUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
				.ConfigureAwait(false))
			{
				response.EnsureSuccessStatusCode();

				if (response.Content.Headers.ContentLength > MaximumCatalogBytes)
					throw new InvalidDataException("The update catalog exceeds the maximum allowed size.");

				using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
				using (var target = new MemoryStream())
				{
					var buffer = new byte[8192];
					int read;

					while ((read = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
					{
						if (target.Length + read > MaximumCatalogBytes)
							throw new InvalidDataException("The update catalog exceeds the maximum allowed size.");

						await target.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
					}

					var json = Encoding.UTF8.GetString(target.ToArray());
					return JsonConvert.DeserializeObject<UpdateCatalogManifest>(json);
				}
			}
		}

		private async Task<string> DownloadToFileAsync(UpdatePackageInfo package, string partialPath,
			CancellationToken cancellationToken)
		{
			using (var response = await _httpClient
				.GetAsync(package.PackageUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
				.ConfigureAwait(false))
			{
				response.EnsureSuccessStatusCode();

				if (!HasSameOrigin(_catalogUri, response.RequestMessage?.RequestUri))
					throw new InvalidDataException("The package response came from an unexpected server.");

				var headerSize = response.Content.Headers.ContentLength;
				if (headerSize.HasValue && headerSize.Value != package.SizeInBytes)
					throw new InvalidDataException("The package size does not match the release catalog.");

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
						receivedBytes += read;
						if (receivedBytes > package.SizeInBytes || receivedBytes > MaximumPackageBytes)
							throw new InvalidDataException("The package exceeds the size declared by the release catalog.");

						await target.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
						sha256.TransformBlock(buffer, 0, read, null, 0);

						var percent = (int)(100L * receivedBytes / package.SizeInBytes);
						if (percent == lastReportedPercent)
							continue;

						lastReportedPercent = percent;
						Publish(new UpdateStatus(EUpdateState.Downloading, package, percent / 100d,
							$"Downloading version {UpdatePolicy.Format(package.Version)}..."));
					}

					if (receivedBytes != package.SizeInBytes)
						throw new InvalidDataException("The package ended before the declared size was reached.");

					sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
					return PackageIntegrity.ToHex(sha256.Hash);
				}
			}
		}

		internal IReadOnlyList<UpdatePackageInfo> ToPackageInfos(UpdateCatalogManifest catalog,
			out string rejectReason)
		{
			rejectReason = null;

			if (catalog == null)
				return Reject("The update server returned an empty catalog.", out rejectReason);

			if (catalog.SchemaVersion != 2)
				return Reject($"The update catalog schema '{catalog.SchemaVersion}' is not supported.", out rejectReason);

			if (!TryParseCanonicalVersion(catalog.MinimumVersion, out var minimumVersion)
				|| !UpdatePolicy.IsSameVersion(minimumVersion, UpdatePolicy.MinimumRollbackVersion))
			{
				return Reject($"The update catalog must declare minimumVersion {UpdatePolicy.Format(UpdatePolicy.MinimumRollbackVersion)}.",
					out rejectReason);
			}

			if (catalog.Releases == null || catalog.Releases.Length == 0)
				return Reject("The update catalog contains no releases.", out rejectReason);

			var packages = new List<UpdatePackageInfo>(catalog.Releases.Length);
			var versions = new HashSet<string>(StringComparer.Ordinal);

			foreach (var release in catalog.Releases)
			{
				var package = ToPackageInfo(release, out var packageRejectReason);
				if (package == null)
					return Reject(packageRejectReason, out rejectReason);

				var versionText = UpdatePolicy.Format(package.Version);
				if (!versions.Add(versionText))
					return Reject($"Version {versionText} occurs more than once in the update catalog.", out rejectReason);

				packages.Add(package);
			}

			packages.Sort((left, right) => UpdatePolicy.Normalize(right.Version).CompareTo(UpdatePolicy.Normalize(left.Version)));

			if (!LatestVersionMatches(catalog.LatestRelease, EUpdateChannel.Release, packages))
				return Reject("The update catalog's latestRelease value does not match its newest release package.", out rejectReason);

			if (!LatestVersionMatches(catalog.LatestBeta, EUpdateChannel.Beta, packages))
				return Reject("The update catalog's latestBeta value does not match its newest beta package.", out rejectReason);

			return Array.AsReadOnly(packages.ToArray());
		}

		internal static UpdatePackageInfo GetLatestPackageForChannel(
			IReadOnlyList<UpdatePackageInfo> packages, EUpdateChannel channel)
			=> packages?.FirstOrDefault(package => package.Channel == channel);

		private UpdatePackageInfo ToPackageInfo(UpdateManifest manifest, out string rejectReason)
		{
			rejectReason = null;

			if (manifest == null || !TryParseCanonicalVersion(manifest.Version, out var version))
				return RejectPackage($"The update catalog carries an unusable version '{manifest?.Version}'.", out rejectReason);

			if (!TryParseChannel(manifest.Channel, out var channel))
				return RejectPackage($"Version {UpdatePolicy.Format(version)} has an invalid release channel.", out rejectReason);

			if (UpdatePolicy.IsBelowRollbackFloor(version))
				return RejectPackage($"Version {UpdatePolicy.Format(version)} is below the rollback floor.", out rejectReason);

			if (manifest.Package == null || string.IsNullOrWhiteSpace(manifest.Package.Url))
				return RejectPackage($"Version {UpdatePolicy.Format(version)} names no package.", out rejectReason);

			if (!Uri.TryCreate(_catalogUri, manifest.Package.Url, out var packageUri)
				|| !string.Equals(packageUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
				|| !HasSameOrigin(_catalogUri, packageUri))
			{
				return RejectPackage($"Version {UpdatePolicy.Format(version)} carries a package URL outside the update server.", out rejectReason);
			}

			if (!PackageIntegrity.HasAllowedExtension(packageUri.LocalPath))
				return RejectPackage($"Version {UpdatePolicy.Format(version)} is not an .exe or .msi installer.", out rejectReason);

			if (!IsSha256(manifest.Package.Sha256))
				return RejectPackage($"Version {UpdatePolicy.Format(version)} has no valid SHA-256.", out rejectReason);

			if (manifest.Package.Size <= 0 || manifest.Package.Size > MaximumPackageBytes)
				return RejectPackage($"Version {UpdatePolicy.Format(version)} has an invalid package size.", out rejectReason);

			if (manifest.Package.Arguments?.Length > 2048)
				return RejectPackage($"Version {UpdatePolicy.Format(version)} carries oversized installer arguments.", out rejectReason);

			if (manifest.Summary?.Length > 4000
				|| (manifest.Highlights?.Length ?? 0) > 100
				|| (manifest.Highlights?.Any(highlight => string.IsNullOrWhiteSpace(highlight) || highlight.Length > 1000) ?? false))
			{
				return RejectPackage($"Version {UpdatePolicy.Format(version)} carries invalid release text.", out rejectReason);
			}

			DateTime? releaseDate = null;
			if (DateTime.TryParse(manifest.ReleaseDate, out var parsedReleaseDate))
				releaseDate = parsedReleaseDate;

			Uri releaseNotesUri = null;
			if (!string.IsNullOrWhiteSpace(manifest.ReleaseNotesUrl))
			{
				if (!Uri.TryCreate(_catalogUri, manifest.ReleaseNotesUrl, out releaseNotesUri)
					|| !string.Equals(releaseNotesUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
				{
					return RejectPackage($"Version {UpdatePolicy.Format(version)} carries an invalid release notes URL.", out rejectReason);
				}
			}

			return new UpdatePackageInfo
			{
				Version = version,
				Channel = channel,
				ReleaseDate = releaseDate,
				Summary = manifest.Summary,
				Highlights = manifest.Highlights,
				PackageUri = packageUri,
				Sha256 = manifest.Package.Sha256.ToLowerInvariant(),
				SizeInBytes = manifest.Package.Size,
				InstallerArguments = manifest.Package.Arguments,
				ReleaseNotesUri = releaseNotesUri
			};
		}

		private string GetPackageFileName(UpdatePackageInfo package)
		{
			var fileName = Path.GetFileName(package.PackageUri.LocalPath);

			foreach (var invalidChar in Path.GetInvalidFileNameChars())
				fileName = fileName.Replace(invalidChar, '_');

			return PackageIntegrity.HasAllowedExtension(fileName)
				? fileName
				: $"CapFrameX-{UpdatePolicy.Format(package.Version)}-setup.exe";
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

		private static bool TryParseCanonicalVersion(string value, out Version version)
		{
			if (!Version.TryParse(value, out var parsedVersion)
				|| parsedVersion.Build < 0
				|| parsedVersion.Revision < 0
				|| !string.Equals(value, UpdatePolicy.Format(parsedVersion), StringComparison.Ordinal))
			{
				version = null;
				return false;
			}

			version = UpdatePolicy.Normalize(parsedVersion);
			return true;
		}

		private static bool TryParseChannel(string value, out EUpdateChannel channel)
		{
			switch (value)
			{
				case "release":
					channel = EUpdateChannel.Release;
					return true;
				case "beta":
					channel = EUpdateChannel.Beta;
					return true;
				default:
					channel = default;
					return false;
			}
		}

		private static bool LatestVersionMatches(string value, EUpdateChannel channel,
			IReadOnlyList<UpdatePackageInfo> packages)
		{
			var expected = packages.FirstOrDefault(package => package.Channel == channel);

			if (expected == null)
				return string.IsNullOrWhiteSpace(value);

			return TryParseCanonicalVersion(value, out var version)
				&& UpdatePolicy.IsSameVersion(version, expected.Version);
		}

		private static bool IsSha256(string value)
			=> !string.IsNullOrWhiteSpace(value)
				&& value.Length == 64
				&& value.All(Uri.IsHexDigit);

		private static bool HasSameOrigin(Uri expected, Uri actual)
			=> expected != null
				&& actual != null
				&& string.Equals(expected.Scheme, actual.Scheme, StringComparison.OrdinalIgnoreCase)
				&& string.Equals(expected.IdnHost, actual.IdnHost, StringComparison.OrdinalIgnoreCase)
				&& expected.Port == actual.Port;

		private static IReadOnlyList<UpdatePackageInfo> Reject(string reason, out string rejectReason)
		{
			rejectReason = reason;
			return null;
		}

		private static UpdatePackageInfo RejectPackage(string reason, out string rejectReason)
		{
			rejectReason = reason;
			return null;
		}
	}
}

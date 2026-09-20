using System;
using System.Diagnostics;
using System.IO;

namespace CapFrameX.Updater
{
	/// <summary>
	/// Installs an update that a previous session staged. This runs at the very beginning of app
	/// start - before the container is built and before the shell exists - because the installer
	/// replaces the files of the running app, so CapFrameX has to be on its way out when it starts.
	/// </summary>
	public static class UpdateInstaller
	{
		/// <summary>
		/// Hands a staged package to the installer, if there is one.
		/// </summary>
		/// <param name="updatesFolder">Folder holding the staged package and its marker file.</param>
		/// <param name="currentVersion">Version of the running app. Older packages require an explicit rollback marker.</param>
		/// <param name="logInfo">Receives progress messages.</param>
		/// <param name="logError">Receives failures. The exception may be null.</param>
		/// <returns>
		/// True when an installer was started and the caller must shut down immediately. False in
		/// every other case, including every failure - a broken update must never block the app.
		/// </returns>
		public static bool TryStartPendingUpdate(string updatesFolder, Version currentVersion,
			Action<string> logInfo, Action<Exception, string> logError)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(updatesFolder) || !Directory.Exists(updatesFolder))
					return false;

				PendingUpdate pending;
				try
				{
					pending = PendingUpdate.Load(updatesFolder);
				}
				catch (Exception ex)
				{
					logError(ex, "Unreadable pending update marker. Discarding the staged update.");
					Discard(updatesFolder, logError);
					return false;
				}

				if (pending == null)
				{
					// Nothing pending: whatever is left in here is from an install that already ran
					// or that the user cancelled, and it is worth tens of megabytes.
					RemoveStagedFiles(updatesFolder, logInfo, logError);
					return false;
				}

				if (!Version.TryParse(pending.Version, out var pendingVersion))
				{
					logError(null, $"Pending update has an unusable version '{pending.Version}'. Discarding it.");
					Discard(updatesFolder, logError);
					return false;
				}

				if (!IsVersionTransitionAllowed(pendingVersion, currentVersion, pending.AllowDowngrade,
					out var versionRejectReason))
				{
					logInfo(versionRejectReason + " Discarding it.");
					Discard(updatesFolder, logError);
					return false;
				}

				if (string.IsNullOrWhiteSpace(pending.PackageFile)
					|| !PackageIntegrity.HasAllowedExtension(pending.PackageFile))
				{
					logError(null, $"Pending update package '{pending.PackageFile}' is not an installer. Discarding it.");
					Discard(updatesFolder, logError);
					return false;
				}

				// Path.GetFileName strips any directory part a hand-edited marker might carry, so
				// the package can only ever be started from the updates folder.
				var packagePath = Path.Combine(updatesFolder, Path.GetFileName(pending.PackageFile));

				if (!File.Exists(packagePath))
				{
					logError(null, $"Pending update package '{packagePath}' is missing. Discarding it.");
					Discard(updatesFolder, logError);
					return false;
				}

				if (!PackageIntegrity.HashMatches(pending.Sha256, PackageIntegrity.ComputeSha256(packagePath)))
				{
					logError(null, "Pending update package has no valid checksum or failed its checksum check. Discarding it.");
					Discard(updatesFolder, logError);
					return false;
				}

				// Clear the marker before launching: if the installer fails to start, or the user
				// cancels it, the next start must not retry forever.
				PendingUpdate.Clear(updatesFolder);

				var startInfo = new ProcessStartInfo(packagePath)
				{
					// Required to let the installer's own manifest request elevation.
					UseShellExecute = true,
					Arguments = pending.Arguments ?? string.Empty,
					WorkingDirectory = updatesFolder
				};

				Process.Start(startInfo);
				var operation = currentVersion != null && UpdatePolicy.IsDowngrade(pendingVersion, currentVersion)
					? "rollback"
					: "update";
				logInfo($"Started the CapFrameX {operation} installer for version {pendingVersion} and shutting down.");
				return true;
			}
			catch (Exception ex)
			{
				logError(ex, "Unable to install the pending update.");
				return false;
			}
		}

		internal static bool IsVersionTransitionAllowed(Version targetVersion, Version currentVersion,
			bool allowDowngrade, out string rejectReason)
		{
			if (UpdatePolicy.IsBelowRollbackFloor(targetVersion))
			{
				rejectReason = $"Pending version {targetVersion} is older than the supported rollback floor {UpdatePolicy.MinimumRollbackVersion}.";
				return false;
			}

			if (currentVersion != null && UpdatePolicy.IsSameVersion(targetVersion, currentVersion))
			{
				rejectReason = $"Pending version {targetVersion} is already installed.";
				return false;
			}

			if (currentVersion != null
				&& UpdatePolicy.IsDowngrade(targetVersion, currentVersion)
				&& !allowDowngrade)
			{
				rejectReason = $"Pending version {targetVersion} is older than {currentVersion}, but no explicit rollback was requested.";
				return false;
			}

			rejectReason = null;
			return true;
		}

		private static void Discard(string updatesFolder, Action<Exception, string> logError)
		{
			try
			{
				PendingUpdate.Clear(updatesFolder);
				RemoveStagedFiles(updatesFolder, null, logError);
			}
			catch (Exception ex)
			{
				logError(ex, "Unable to discard the staged update.");
			}
		}

		private static void RemoveStagedFiles(string updatesFolder, Action<string> logInfo,
			Action<Exception, string> logError)
		{
			foreach (var file in Directory.GetFiles(updatesFolder))
			{
				if (string.Equals(Path.GetFileName(file), PendingUpdate.FileName, StringComparison.OrdinalIgnoreCase))
					continue;

				try
				{
					File.Delete(file);
					logInfo?.Invoke($"Removed the leftover update package '{Path.GetFileName(file)}'.");
				}
				catch (Exception ex)
				{
					logError(ex, $"Unable to remove the leftover update package '{file}'.");
				}
			}
		}
	}
}

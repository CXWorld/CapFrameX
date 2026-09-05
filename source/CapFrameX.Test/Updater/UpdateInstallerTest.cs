using CapFrameX.Updater;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;

namespace CapFrameX.Test.Updater
{
	/// <summary>
	/// Covers the reject paths of the install-on-restart step. The happy path is deliberately not
	/// exercised: its final act is starting the installer, which a unit test must not do.
	/// </summary>
	[TestClass]
	public class UpdateInstallerTest
	{
		private static readonly Version InstalledVersion = new Version(1, 9, 0, 2);

		private string _updatesFolder;
		private List<string> _infoMessages;
		private List<string> _errorMessages;

		[TestInitialize]
		public void Setup()
		{
			_updatesFolder = Path.Combine(Path.GetTempPath(), "CapFrameXUpdateTest_" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_updatesFolder);
			_infoMessages = new List<string>();
			_errorMessages = new List<string>();
		}

		[TestCleanup]
		public void Cleanup()
		{
			try
			{
				if (Directory.Exists(_updatesFolder))
					Directory.Delete(_updatesFolder, true);
			}
			catch (IOException)
			{
				// A leftover temp folder must not fail the test run.
			}
		}

		[TestMethod]
		public void TryStartPendingUpdate_MissingFolder_ReturnsFalse()
		{
			var missingFolder = Path.Combine(_updatesFolder, "does-not-exist");

			Assert.IsFalse(Run(missingFolder));
		}

		[TestMethod]
		public void TryStartPendingUpdate_NoMarker_RemovesLeftoverPackage()
		{
			var leftoverPackage = WritePackage("CapFrameX-1.9.1.0-setup.exe");

			Assert.IsFalse(Run());
			Assert.IsFalse(File.Exists(leftoverPackage), "An install that already ran must not leave its package behind.");
		}

		[TestMethod]
		public void TryStartPendingUpdate_UnreadableMarker_DiscardsStagedUpdate()
		{
			var package = WritePackage("CapFrameX-1.9.1.0-setup.exe");
			File.WriteAllText(PendingUpdate.GetMarkerPath(_updatesFolder), "{ this is not json");

			Assert.IsFalse(Run());
			Assert.IsFalse(File.Exists(package));
			AssertMarkerIsGone();
		}

		[TestMethod]
		public void TryStartPendingUpdate_VersionNotNewerThanInstalled_DiscardsStagedUpdate()
		{
			var package = WritePackage("CapFrameX-1.9.0.2-setup.exe");
			StageMarker("1.9.0.2", Path.GetFileName(package), PackageSha256(package));

			Assert.IsFalse(Run());
			Assert.IsFalse(File.Exists(package));
			AssertMarkerIsGone();
		}

		[TestMethod]
		public void IsVersionTransitionAllowed_ExplicitRollbackAtFloor_ReturnsTrue()
		{
			var result = UpdateInstaller.IsVersionTransitionAllowed(
				new Version(1, 9, 0, 0), new Version(2, 0, 0, 0), allowDowngrade: true, out var rejectReason);

			Assert.IsTrue(result);
			Assert.IsNull(rejectReason);
		}

		[TestMethod]
		public void IsVersionTransitionAllowed_DowngradeWithoutExplicitMarker_ReturnsFalse()
		{
			var result = UpdateInstaller.IsVersionTransitionAllowed(
				new Version(1, 9, 0, 1), new Version(1, 9, 0, 2), allowDowngrade: false, out var rejectReason);

			Assert.IsFalse(result);
			StringAssert.Contains(rejectReason, "no explicit rollback");
		}

		[TestMethod]
		public void IsVersionTransitionAllowed_NewerFourthComponent_ReturnsTrue()
		{
			var result = UpdateInstaller.IsVersionTransitionAllowed(
				new Version(1, 9, 0, 3), new Version(1, 9, 0, 2),
				allowDowngrade: false, out var rejectReason);

			Assert.IsTrue(result);
			Assert.IsNull(rejectReason);
		}

		[TestMethod]
		public void IsVersionTransitionAllowed_TargetBelowFloor_ReturnsFalse()
		{
			var result = UpdateInstaller.IsVersionTransitionAllowed(
				new Version(1, 8, 9, 9), new Version(2, 0, 0, 0), allowDowngrade: true, out var rejectReason);

			Assert.IsFalse(result);
			StringAssert.Contains(rejectReason, "rollback floor");
		}

		[TestMethod]
		public void TryStartPendingUpdate_UnusableVersion_DiscardsStagedUpdate()
		{
			var package = WritePackage("CapFrameX-setup.exe");
			StageMarker("not-a-version", Path.GetFileName(package), PackageSha256(package));

			Assert.IsFalse(Run());
			Assert.IsFalse(File.Exists(package));
			AssertMarkerIsGone();
		}

		[TestMethod]
		public void TryStartPendingUpdate_PackageIsNotAnInstaller_DiscardsStagedUpdate()
		{
			var package = WritePackage("CapFrameX-1.9.1.0-setup.bat");
			StageMarker("1.9.1.0", Path.GetFileName(package), PackageSha256(package));

			Assert.IsFalse(Run());
			Assert.IsFalse(File.Exists(package));
			AssertMarkerIsGone();
		}

		[TestMethod]
		public void TryStartPendingUpdate_ChecksumMismatch_DiscardsStagedUpdate()
		{
			var package = WritePackage("CapFrameX-1.9.1.0-setup.exe");
			StageMarker("1.9.1.0", Path.GetFileName(package), new string('a', 64));

			Assert.IsFalse(Run());
			Assert.IsFalse(File.Exists(package), "A package that does not match its checksum must not survive.");
			AssertMarkerIsGone();
		}

		[TestMethod]
		public void TryStartPendingUpdate_MissingChecksum_DiscardsStagedUpdate()
		{
			var package = WritePackage("CapFrameX-1.9.1.0-setup.exe");
			StageMarker("1.9.1.0", Path.GetFileName(package), null);

			Assert.IsFalse(Run());
			Assert.IsFalse(File.Exists(package), "A package without a trusted checksum must not survive.");
			AssertMarkerIsGone();
		}

		[TestMethod]
		public void TryStartPendingUpdate_PackagePathOutsideUpdatesFolder_DiscardsStagedUpdate()
		{
			// The marker is written from remote manifest data, so a package name must never be
			// able to point the installer somewhere else on disk.
			var package = WritePackage("CapFrameX-1.9.1.0-setup.exe");
			StageMarker("1.9.1.0", @"..\..\Windows\System32\cmd.exe", PackageSha256(package));

			Assert.IsFalse(Run());
			AssertMarkerIsGone();
		}

		private bool Run(string updatesFolder = null)
			=> UpdateInstaller.TryStartPendingUpdate(
				updatesFolder ?? _updatesFolder,
				InstalledVersion,
				message => _infoMessages.Add(message),
				(exception, message) => _errorMessages.Add(message));

		private string WritePackage(string fileName)
		{
			var path = Path.Combine(_updatesFolder, fileName);
			File.WriteAllText(path, "not a real installer");
			return path;
		}

		private void StageMarker(string version, string packageFile, string sha256)
			=> new PendingUpdate
			{
				Version = version,
				PackageFile = packageFile,
				Sha256 = sha256,
				Arguments = "/passive /norestart",
				StagedUtc = DateTime.UtcNow
			}.Save(_updatesFolder);

		private void AssertMarkerIsGone()
			=> Assert.IsFalse(File.Exists(PendingUpdate.GetMarkerPath(_updatesFolder)),
				"A rejected update must not be retried on the next start.");

		private static string PackageSha256(string path)
		{
			using (var sha256 = System.Security.Cryptography.SHA256.Create())
			using (var stream = File.OpenRead(path))
			{
				return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
			}
		}
	}
}

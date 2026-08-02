using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Update;
using CapFrameX.Updater;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;

namespace CapFrameX.Test.Updater
{
	[TestClass]
	public class UpdateCatalogTest
	{
		private UpdateService _service;

		[TestInitialize]
		public void Setup()
		{
			var versionProvider = new Mock<IAppVersionProvider>();
			versionProvider.Setup(provider => provider.GetAppVersion()).Returns(new Version(1, 9, 0, 0));
			versionProvider.Setup(provider => provider.GetReleaseChannel()).Returns(EUpdateChannel.Release);
			_service = new UpdateService(
				"https://updates.capframex.com/api/v2/releases",
				versionProvider.Object,
				"updates",
				Mock.Of<ILogger<UpdateService>>());
		}

		[TestMethod]
		public void ToPackageInfos_ValidCatalog_SortsEveryBuildAndPreservesChannels()
		{
			var catalog = Catalog("2.0.0.0", "1.9.0.2",
				Release("1.9.0.1"),
				Release("2.0.0.0"),
				Release("1.9.0.2", EUpdateChannel.Beta));

			var packages = _service.ToPackageInfos(catalog, out var rejectReason);

			Assert.IsNotNull(packages);
			Assert.IsNull(rejectReason);
			Assert.AreEqual(new Version(2, 0, 0, 0), packages[0].Version);
			Assert.AreEqual(new Version(1, 9, 0, 2), packages[1].Version);
			Assert.AreEqual(EUpdateChannel.Beta, packages[1].Channel);
			Assert.AreEqual(new Version(1, 9, 0, 1), packages[2].Version);
		}

		[TestMethod]
		public void ToPackageInfos_DifferentFourthComponents_AreDistinctVersions()
		{
			var catalog = Catalog("1.9.0.2", null,
				Release("1.9.0.1"), Release("1.9.0.2"));

			var packages = _service.ToPackageInfos(catalog, out var rejectReason);

			Assert.IsNotNull(packages);
			Assert.IsNull(rejectReason);
			Assert.AreEqual(2, packages.Count);
		}

		[TestMethod]
		public void GetLatestPackageForChannel_ReleaseClientDoesNotAutoSelectNewerBeta()
		{
			var catalog = Catalog("1.9.1.0", "2.0.0.0",
				Release("2.0.0.0", EUpdateChannel.Beta), Release("1.9.1.0"));
			var packages = _service.ToPackageInfos(catalog, out var rejectReason);

			var latestRelease = UpdateService.GetLatestPackageForChannel(packages, EUpdateChannel.Release);

			Assert.IsNull(rejectReason);
			Assert.AreEqual(new Version(1, 9, 1, 0), latestRelease.Version);
			Assert.AreEqual(EUpdateChannel.Release, latestRelease.Channel);
		}

		[TestMethod]
		public void ToPackageInfos_ReleaseBelowRollbackFloor_IsRejected()
		{
			var catalog = Catalog("1.8.9.9", null, Release("1.8.9.9"));

			var packages = _service.ToPackageInfos(catalog, out var rejectReason);

			Assert.IsNull(packages);
			StringAssert.Contains(rejectReason, "rollback floor");
		}

		[TestMethod]
		public void ToPackageInfos_PackageOnDifferentOrigin_IsRejected()
		{
			var release = Release("1.9.0.0");
			release.Package.Url = "https://example.org/CapFrameX-1.9.0.0.exe";

			var packages = _service.ToPackageInfos(Catalog("1.9.0.0", null, release), out var rejectReason);

			Assert.IsNull(packages);
			StringAssert.Contains(rejectReason, "outside the update server");
		}

		[TestMethod]
		public void ToPackageInfos_MissingChannel_IsRejected()
		{
			var release = Release("1.9.0.1");
			release.Channel = null;

			var packages = _service.ToPackageInfos(Catalog("1.9.0.1", null, release), out var rejectReason);

			Assert.IsNull(packages);
			StringAssert.Contains(rejectReason, "release channel");
		}

		[TestMethod]
		public void ToPackageInfos_LatestBetaMustMatchNewestBetaBuild()
		{
			var catalog = Catalog("1.9.0.0", "1.9.0.1",
				Release("1.9.0.0"), Release("1.9.0.2", EUpdateChannel.Beta));

			var packages = _service.ToPackageInfos(catalog, out var rejectReason);

			Assert.IsNull(packages);
			StringAssert.Contains(rejectReason, "latestBeta");
		}

		private static UpdateCatalogManifest Catalog(string latestRelease, string latestBeta,
			params UpdateManifest[] releases)
			=> new UpdateCatalogManifest
			{
				SchemaVersion = 2,
				MinimumVersion = "1.9.0.0",
				LatestRelease = latestRelease,
				LatestBeta = latestBeta,
				Releases = releases
			};

		private static UpdateManifest Release(string version,
			EUpdateChannel channel = EUpdateChannel.Release)
			=> new UpdateManifest
			{
				Version = version,
				Channel = channel == EUpdateChannel.Beta ? "beta" : "release",
				ReleaseDate = "2026-08-02T00:00:00Z",
				Summary = "Release " + version,
				Highlights = Array.Empty<string>(),
				ReleaseNotesUrl = "https://www.capframex.com/news",
				Package = new UpdateManifestPackage
				{
					Url = $"/packages/{version}/CapFrameX-{version}.exe",
					Sha256 = new string('a', 64),
					Size = 100,
					Arguments = "/passive /norestart"
				}
			};
	}
}

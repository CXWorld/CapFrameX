using CapFrameX.Contracts.Data;
using CapFrameX.Data;
using CapFrameX.Updater;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Threading.Tasks;

namespace CapFrameX.Test.Updater
{
	[TestClass]
	public class UpdateCheckTest
	{
		[TestMethod]
		public async Task UpdateCheckTest_UpdateIsAvailable()
		{
			string url = "https://raw.githubusercontent.com/DevTechProfile/CapFrameX/244464bf8f7c8de9c8a79dbbf3b85032f566a56f/version/Version.txt"; // 1.3.1

			var appVersionProviderMock = new Mock<IAppVersionProvider>();
			appVersionProviderMock.Setup(m => m.GetAppVersion()).Returns(new Version(1, 3, 0));

			var updateChecker = new UpdateCheck(appVersionProviderMock.Object, new WebVersionProvider(url));

			var (result, version) = await updateChecker.IsUpdateAvailable();
			Assert.IsTrue(result);
		}

		[TestMethod]
		public async Task UpdateCheckTest_UpdateIsNotAvailable()
		{
			string url = "https://raw.githubusercontent.com/DevTechProfile/CapFrameX/develop/feature/rtss_client_implementation/version/Version.txt";

			var appVersionProviderMock = new Mock<IAppVersionProvider>();
			appVersionProviderMock.Setup(m => m.GetAppVersion()).Returns(new Version(1, 4, 0));

			var updateChecker = new UpdateCheck(appVersionProviderMock.Object, new WebVersionProvider(url));
			var (result, version) = await updateChecker.IsUpdateAvailable();
			Assert.IsFalse(result);
		}
	}
}
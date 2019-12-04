using CapFrameX.Updater;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Updater
{
	[TestClass]
	public class WebCheckTest
	{
		[TestMethod]
		public void CheckWebUpdate_CorrecState()
		{
			string url = "https://raw.githubusercontent.com/DevTechProfile/CapFrameX/develop/feature/rtss_client_implementation/version/Version.txt";
			Assert.IsTrue(WebCheck.IsCXUpdateAvailable(url, () => "1.3.0"));
		}
	}
}
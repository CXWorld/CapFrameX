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
			string url = "https://raw.githubusercontent.com/DevTechProfile/CapFrameX/2d55cc088fa90eb61d1d33c371d65264ce7d3a0a/version/Version.txt";
			Assert.IsTrue(WebCheck.IsCXUpdateAvailable(url, () => "1.3.0"));
		}
	}
}
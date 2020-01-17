using CapFrameX.PresentMonInterface;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace CapFrameX.Test.PresentMonInterface
{
	[TestClass]
	public class SystemInfoTest
	{
		[TestMethod]
		public void GetCPUInfo_CorrectName()
		{
            Console.WriteLine(SystemInfo.GetProcessorName());
		}

		[TestMethod]
		public void GetGPUInfo_CorrectName()
		{
			Console.WriteLine(SystemInfo.GetGraphicCardName());
		}

		[TestMethod]
		public void GetGraphicCardVendor_CorrectVendor()
		{
			Console.WriteLine(SystemInfo.GetGraphicCardVendor());
		}

		[TestMethod]
		public void GetMotherboardInfo_CorrectName()
		{
			Console.WriteLine(SystemInfo.GetMotherboardName());
		}

        [TestMethod]
        public void GetSystemRAMInfo_CorrectInfo()
        {
            Console.WriteLine(SystemInfo.GetSystemRAMInfoName());
        }

        [TestMethod]
        public void GetOSVersion_CorrectInfo()
        {
            Console.WriteLine(SystemInfo.GetOSVersion());
        }

		[TestMethod]
		public void GetGraphicDriverVersion_CorrectVersion()
		{
			Console.WriteLine(SystemInfo.GetGraphicDriverVersion());
		}
	}
}

using CapFrameX.PresentMonInterface;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace CapFrameX.Test.PresentMonInterface
{
	[TestClass]
	public class HardwareInfoTest
	{
		[TestMethod]
		public void GetCPUInfo_CorrectName()
		{
			Console.WriteLine(HardwareInfo.GetProcessorName());
		}

		[TestMethod]
		public void GetGPUInfo_CorrectName()
		{
			Console.WriteLine(HardwareInfo.GetGraphicCardName());
		}

		[TestMethod]
		public void GetMotherboardInfo_CorrectName()
		{
			Console.WriteLine(HardwareInfo.GetMotherboardName());
		}
	}
}

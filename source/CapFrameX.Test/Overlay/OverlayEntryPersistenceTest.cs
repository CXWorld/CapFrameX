using CapFrameX.Overlay;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System.IO;

namespace CapFrameX.Test.Overlay
{
	[TestClass]
	public class OverlayEntryPersistenceTest
	{
		[TestMethod]
		public void CreateJsonFile_InitialFile()
		{
			var persistence = new OverlayEntryPersistence
			{
				OverlayEntries = OverlayUtils.GetOverlayEntryDefaults()
			};

			File.WriteAllText(
				@"..\..\..\..\CapFrameX.Overlay\OverlayConfiguration\OverlayEntryConfiguration.json", 
				JsonConvert.SerializeObject(persistence));
		}
	}
}

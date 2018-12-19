using System.Linq;

namespace CapFrameX.OcatInterface
{
	public class ComparisonRecordInfo
	{
		public string Letter => Game.First().ToString();
		public string Game { get; set; }
		public string InfoText { get; set; }
	}
}

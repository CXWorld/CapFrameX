using CapFrameX.Contracts.Data;
using System.Linq;

namespace CapFrameX.OcatInterface
{
	public class ComparisonRecordInfo
	{
		public string Letter => Game.First().ToString();
		public string Game { get; set; }
		public string InfoText { get; set; }
        public string DateTime { get; set; }
        public Session Session { get; set; }
        public IFileRecordInfo FileRecordInfo { get; set; }
    }
}

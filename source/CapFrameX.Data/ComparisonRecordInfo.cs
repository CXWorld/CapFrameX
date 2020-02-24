using CapFrameX.Contracts.Data;
using System.Linq;

namespace CapFrameX.Data
{
	public class ComparisonRecordInfo
	{
		public string Letter => Game.First().ToString();
		public string Game { get; set; }
		public string InfoText { get; set; }
        public string DateTime { get; set; }
        public ISession Session { get; set; }
		public double FirstMetric { get; set; }
		public double SecondMetric { get; set; }
		public double ThirdMetric { get; set; }
		public IFileRecordInfo FileRecordInfo { get; set; }
    }
}

using CapFrameX.Contracts.Cloud;
using CapFrameX.Contracts.Data;

namespace CapFrameX.Data
{
	public class CloudEntry : ICloudEntry
	{
		public string GameName { get; set; }

		public string CreationDate { get; set; }

		public string CreationTime { get; set; }

		public string Comment { get; set; }

		public IFileRecordInfo FileRecordInfo { get; set; }
	}
}

using CapFrameX.Contracts.Data;

namespace CapFrameX.Contracts.Cloud
{
	public interface ICloudEntry
	{
		string GameName { get; }

		string CreationDate { get; }

		string CreationTime { get; }

		string Comment { get; }

		IFileRecordInfo FileRecordInfo { get; }
	}
}

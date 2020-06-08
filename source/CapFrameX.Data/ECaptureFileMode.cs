using System.ComponentModel;

namespace CapFrameX.Data
{
	public enum ECaptureFileMode
	{
		[Description("JSON")]
		Json = 1,
		[Description("JSON + CSV")]
		JsonCsv = 2
	}
}

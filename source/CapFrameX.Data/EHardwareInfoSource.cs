using System.ComponentModel;

namespace CapFrameX.Data
{
	public enum EHardwareInfoSource
	{
		[Description("No hardware info")]
		None = 1,
		[Description("Automatic detection")]
		Auto = 2,
		[Description("Custom description")]
		Custom = 3
	}
}

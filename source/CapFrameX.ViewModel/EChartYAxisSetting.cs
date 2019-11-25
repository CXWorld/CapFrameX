using System.ComponentModel;

namespace CapFrameX.ViewModel
{
	public enum EChartYAxisSetting
	{
		[Description("Full fit")]
		FullFit = 1,
		[Description("Interquartile range")]
		IQR = 2,
		[Description("0-10ms")]
		Zero_Ten = 3,
		[Description("0-20ms")]
		Zero_Twenty = 4,
		[Description("0-40ms")]
		Zero_Forty = 5,
		[Description("0-60ms")]
		Zero_Sixty = 6,
		[Description("0-80ms")]
		Zero_Eighty = 7,
		[Description("0-100ms")]
		Zero_Hundred = 8
	}
}

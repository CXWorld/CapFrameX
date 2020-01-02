using System.ComponentModel;

namespace CapFrameX.ViewModel
{
    public enum EAggregationMode
    {
		[Description("Ignore outliers")]
		Ignore = 1,
		[Description("Repeat outliers")]
		Repeat = 2,
		[Description("Include outliers")]
		Include = 3
	}
} 

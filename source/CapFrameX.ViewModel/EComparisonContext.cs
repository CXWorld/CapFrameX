﻿using System.ComponentModel;

namespace CapFrameX.ViewModel
{
    public enum EComparisonContext
    {
		[Description("Date and time")]
		DateTime = 1,
		[Description("CPU")]
		CPU = 2,
		[Description("GPU")]
		GPU = 3,
		[Description("RAM")]
		SystemRam = 4,
        [Description("Custom comment")]
        Custom = 5,
        [Description("GPU Driver")]
        GPUDriver = 6,
        [Description("API")]
        API = 7,
		[Description("None")]
		None = 8
	}
}

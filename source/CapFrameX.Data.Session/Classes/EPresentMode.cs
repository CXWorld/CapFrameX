using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace CapFrameX.Data.Session.Classes
{
	public enum EPresentMode
	{
		[Description("Unknown")]
		Unknown = 0,
		[Description("Hardware: Legacy Flip")]
		HardwareLegacyFlip = 1,
		[Description("Hardware: Legacy Copy to front buffer")]
		HardwareLegacyCopyToFrontBuffer = 2,
		[Description("Hardware: Independent Flip")]
		HardwareIndependentFlip = 3,
		[Description("Composed: Flip")]
		ComposedFlip = 4,
		[Description("Composed: Copy with GPU GDI")]
		ComposedCopyWithGPUGDI = 5,
		[Description("Composed: Copy with CPU GDI")]
		ComposedCopyWithCPUGDI = 6,
		[Description("Composed: Composition Atlas")]
		ComposedCompositionAtlas = 7,
		[Description("Hardware Composed: Independent Flip")]
		HardwareComposedIndependentFlip = 8,
		[Description("Other")]
		Other = 9
	}
}

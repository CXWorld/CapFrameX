using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Contracts.Overlay
{
	public interface IOverlayEntryFormatChange
	{
		bool Colors { get; set; }
		bool Limits { get; set; }
		bool Format { get; set; }
	}
}

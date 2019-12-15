using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Contracts.RTSSInterface
{
	public interface IRTSSCSharpWrapper
	{
		void ShowOverlay();

		void ReleaseOverlay();

		void SetOverlayHeader(IList<string> entries);

		void StartCountDown(int seconds);
	}
}

using CapFrameX.Contracts.Overlay;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Overlay
{
	public class OverlayEntryProvider : IOverlayEntryProvider
	{
		public OverlayEntryProvider()
		{

		}

		public IOverlayEntry[] GetOverlayEntries()
		{
			throw new NotImplementedException();
		}

		public IOverlayEntry GetOverlayEntry(string identifier)
		{
			throw new NotImplementedException();
		}

		public void MoveEntry(int sourceIndex, int targetIndex)
		{
			throw new NotImplementedException();
		}
	}
}

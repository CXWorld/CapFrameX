using System.Collections.Generic;

namespace CapFrameX.Contracts.Overlay
{
	public interface IOverlayEntryProvider
	{
		IOverlayEntry[] GetOverlayEntries();

		void MoveEntry(int sourceIndex, int targetIndex);

		IOverlayEntry GetOverlayEntry(string identifier);
	}
}

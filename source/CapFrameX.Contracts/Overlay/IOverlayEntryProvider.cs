using System.Collections.Generic;
using System.Threading.Tasks;

namespace CapFrameX.Contracts.Overlay
{
	public interface IOverlayEntryProvider
	{
		void MoveEntry(int sourceIndex, int targetIndex);

		IOverlayEntry GetOverlayEntry(string identifier);

		Task SaveOverlayEntriesToJson();

		Task SwitchConfigurationTo(int index);

		Task<IOverlayEntry[]> GetOverlayEntries();

		Task<IEnumerable<IOverlayEntry>> GetDefaultOverlayEntries();
	}
}

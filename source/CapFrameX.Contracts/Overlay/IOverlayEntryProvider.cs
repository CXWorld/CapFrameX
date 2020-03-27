using System.Reactive;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace CapFrameX.Contracts.Overlay
{
	public interface IOverlayEntryProvider
	{

		void MoveEntry(int sourceIndex, int targetIndex);

		IOverlayEntry GetOverlayEntry(string identifier);

		bool SaveOverlayEntriesToJson();

		Task SwitchConfigurationTo(int index);
		Task<IOverlayEntry[]> GetOverlayEntries();
	}
}

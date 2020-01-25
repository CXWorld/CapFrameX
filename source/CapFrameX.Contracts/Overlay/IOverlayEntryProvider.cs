using System.Reactive;
using System.Reactive.Subjects;

namespace CapFrameX.Contracts.Overlay
{
	public interface IOverlayEntryProvider
	{
		ISubject<Unit> EntryUpdateStream { get; }

		IOverlayEntry[] GetOverlayEntries();

		void MoveEntry(int sourceIndex, int targetIndex);

		IOverlayEntry GetOverlayEntry(string identifier);

		bool SaveOverlayEntriesToJson();
	}
}

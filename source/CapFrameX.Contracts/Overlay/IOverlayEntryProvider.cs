using System.Collections.Generic;
using System.Threading.Tasks;

namespace CapFrameX.Contracts.Overlay
{
	public interface IOverlayEntryProvider
	{
		void MoveEntry(int sourceIndex, int targetIndex);

		IOverlayEntry GetOverlayEntry(string identifier);

		void SetFormatForGroupName(string groupName, IOverlayEntry selectedEntry);

		void SetFormatForSensorType(string sensorType, IOverlayEntry selectedEntry);

		Task SaveOverlayEntriesToJson();

		Task SwitchConfigurationTo(int index);

		Task<IOverlayEntry[]> GetOverlayEntries();

		Task<IEnumerable<IOverlayEntry>> GetDefaultOverlayEntries();
	}
}

using System.Collections.Generic;
using System.Threading.Tasks;

namespace CapFrameX.Contracts.Overlay
{
	public interface IOverlayEntryProvider
	{
		IOverlayEntry GetOverlayEntry(string identifier);

		void MoveEntry(int sourceIndex, int targetIndex);

		void SetFormatForGroupName(string groupName, IOverlayEntry selectedEntry);

		void SetFormatForSensorType(string sensorType, IOverlayEntry selectedEntry);

		void ResetColorAndLimits(IOverlayEntry selectedEntry);

		Task SaveOverlayEntriesToJson();

		Task SwitchConfigurationTo(int index);

		Task<IOverlayEntry[]> GetOverlayEntries();

		Task<IEnumerable<IOverlayEntry>> GetDefaultOverlayEntries();
	}
}

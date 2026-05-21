using System.Collections.Generic;
using System.Threading.Tasks;

namespace CapFrameX.Contracts.Overlay
{
	public interface IOverlayEntryProvider
	{
		bool HasHardwareChanged { get; }

		IOverlayEntry GetOverlayEntry(string identifier);

		void MoveEntry(int sourceIndex, int targetIndex);

		void ResetColorAndLimits(IOverlayEntry selectedEntry);

		void SetFormatForGroupName(string groupName, IOverlayEntry selectedEntry, IOverlayEntryFormatChange checkboxes);

		void SetFormatForSensorType(string sensorType, IOverlayEntry selectedEntry, IOverlayEntryFormatChange checkboxes);

        void SetFormatForAllGroups(IOverlayEntry selectedEntry, IOverlayEntryFormatChange checkboxes);

        void SetFormatForAllValues(IOverlayEntry selectedEntry, IOverlayEntryFormatChange checkboxes);

        Task SaveOverlayEntriesToJson(int targetConfig);

	    Task SwitchConfigurationTo(int index);

		Task<IOverlayEntry[]> GetOverlayEntries(bool updateFormats = true);

		Task<IEnumerable<IOverlayEntry>> GetDefaultOverlayEntries();

		void SortOverlayEntriesByType();

		void UpdateOverlayEntries(IEnumerable<IOverlayEntry> entries);

        void UpdateOverlayEntryFormats();
    }
}

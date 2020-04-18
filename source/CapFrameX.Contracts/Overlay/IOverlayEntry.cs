namespace CapFrameX.Contracts.Overlay
{
	public interface IOverlayEntry
	{
		IOverlayEntryProvider OverlayEntryProvider { get; set; }

		string Identifier { get; }

		EOverlayEntryType OverlayEntryType { get; }

		string Description { get; }

		object Value { get; set; }

		string ValueFormat { get; set; }

		string FormattedValue { get; }

	    bool ShowOnOverlay { get; set; }

		bool ShowOnOverlayIsEnabled { get; set; }

		string GroupName { get; set; }

		bool ShowGraph { get; set; }

		bool ShowGraphIsEnabled { get; set; }

		string Color { get; set; }
	}
}

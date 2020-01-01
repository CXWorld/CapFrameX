namespace CapFrameX.Contracts.Overlay
{
	public interface IOverlayEntry
	{
		IOverlayEntryProvider OverlayEntryProvider { get; set; }

		string Identifier { get; }

		string Description { get; }

		string FormattedValue { get; }

	    bool ShowOnOverlay { get; set; }

		bool ShowOnOverlayIsEnabled { get; set; }

		string GroupName { get; set; }

		object Value { get; set; }

		bool ShowGraph { get; set; }

		bool ShowGraphIsEnabled { get; set; }

		string Color { get; set; }
	}
}

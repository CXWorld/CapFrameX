namespace CapFrameX.Contracts.Overlay
{
	public interface IOverlayEntry
	{
		string Identifier { get; }

		string FormattedValue { get; }

	    bool ShowOnOverlay { get; set; }

		string GroupName { get; set; }

		object Value { get; set; }

		bool ShowGraph { get; set; }

		string Color { get; set; }
	}
}

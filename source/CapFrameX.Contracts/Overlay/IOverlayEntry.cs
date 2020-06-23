using System;

namespace CapFrameX.Contracts.Overlay
{
    public interface IOverlayEntry
    {
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

        /// <summary>
        /// Value standard color
        /// </summary>
        string Color { get; set; }

        int ValueFontSize { get; set; }

        string FormattedGroupName { get; }

        string GroupNameFormat { get; set; }

        double UpperLimitValue { get; set; }

        double LowerLimitValue { get; set; }

        string GroupColor { get; set; }

        int GroupFontSize { get; set; }

        int GroupSeparators { get; set; }

        string UpperLimitColor { get; set; }

        string LowerLimitColor { get; set; }

        bool FormatChanged { get; set; }

        bool IsNumeric { get; set; }

        Action UpdateGroupName { get; set; }
    }
}

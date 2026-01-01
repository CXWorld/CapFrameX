using System;

namespace CapFrameX.Contracts.Overlay
{
    public enum LimitState
    {
        Lower,
        Upper,
        None,
        Undefined
    }

    public interface IOverlayEntry
    {
        string Identifier { get; }

        EOverlayEntryType OverlayEntryType { get; }

        string Description { get; }
        /// <summary>
        /// Actual value of the entry
        /// </summary>
        object Value { get; set; }
        /// <summary>
        /// Complete value format including color, text size, decimal points and alignment
        /// </summary>
        string ValueFormat { get; set; }
        /// <summary>
        /// Formatted value including color, text size, decimal points and alignment
        /// </summary>
        string FormattedValue { get; }
        /// <summary>
        /// Determines if the entry is enabled and available for use
        /// </summary>
        bool IsEntryEnabled { get; set; }
        /// <summary>
        /// Determines if the entry is shown on the overlay
        /// </summary>
        bool ShowOnOverlay { get; set; }
        /// <summary>
        /// Determines if the checkbox to show this entry on the overlay is enabled in the overlay items list
        /// </summary>
        bool ShowOnOverlayIsEnabled { get; set; }
        /// <summary>
        /// Group name of the entry
        /// </summary>
        string GroupName { get; set; }
        /// <summary>
        /// Determines if a graph is shown for this entry
        /// </summary>
        bool ShowGraph { get; set; }
        /// <summary>
        /// Determines if the graph checkbox is enabled in the overlay items list
        /// </summary>
        bool ShowGraphIsEnabled { get; set; }
        /// <summary>
        /// Value standard color
        /// </summary>
        string Color { get; set; }
        /// <summary>
        /// Font size (subscript/superscript) of the entry's value
        /// </summary>
        int ValueFontSize { get; set; }
        /// <summary>
        /// Formatted group name including color, text size, and alignment
        /// </summary>
        string FormattedGroupName { get; }
        /// <summary>
        /// Complete value format including color, text size, and alignment
        /// </summary>
        string GroupNameFormat { get; set; }
        /// <summary>
        /// Value, above which an entry's color changes to a set color
        /// </summary>
        string UpperLimitValue { get; set; }
        /// <summary>
        /// Value, below which an entry's color changes to a set color
        /// </summary>
        string LowerLimitValue { get; set; }
        /// <summary>
        /// Color of the entry's group name
        /// </summary>
        string GroupColor { get; set; }
        /// <summary>
        /// Font size (subscript/superscript) of the entry's group name
        /// </summary>
        int GroupFontSize { get; set; }
        /// <summary>
        /// Number of empty rows above the entry in RTSS
        /// </summary>
        int GroupSeparators { get; set; }
        /// <summary>
        /// Color the value changes to, when equal or above a set upper limit
        /// </summary>
        string UpperLimitColor { get; set; }
        /// <summary>
        /// Color the value changes to, when equal or below a set lower limit
        /// </summary>
        string LowerLimitColor { get; set; }
        /// <summary>
        /// Indicator, if anything format related has changed since the last update
        /// </summary>
        bool FormatChanged { get; set; }
        /// <summary>
        /// Indicator, if the value of an entry consists only of int or double
        /// </summary>
        bool IsNumeric { get; set; }
        /// <summary>
        /// Decimal points and text alignment of a value. Can be used with string.Format()
        /// </summary>
        string ValueAlignmentAndDigits { get; set; }
        /// <summary>
        /// Format of a values unit. 
        /// </summary>
        string ValueUnitFormat { get; set; }
        /// <summary>
        /// States if the value was below, above or between set upper/lower limits at the last update
        /// </summary>
        LimitState LastLimitState { get; set; }
        /// <summary>
        /// Sort key used to sort the overlay entries in the overlay items list
        /// </summary>
        string SortKey { get; set; }

        Action<string> UpdateGroupName { get; set; }

        Action PropertyChangedAction { set; get; }

        Action<string, bool> UpdateShowOnOverlay { set; get; }

        IOverlayEntry Clone();
    }
}

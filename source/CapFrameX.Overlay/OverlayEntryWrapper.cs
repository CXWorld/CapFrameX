using CapFrameX.Contracts.Overlay;
using Newtonsoft.Json;
using Prism.Mvvm;
using System;
using System.Globalization;

namespace CapFrameX.Overlay
{
    public class OverlayEntryWrapper : BindableBase, IOverlayEntry
    {
        private bool _showOnOverlay;
        private bool _showOnOverlayIsEnabled;
        private string _groupName;
        private bool _showGraph;
        private bool _showGraphIsEnabled;
        private string _color;
        private int _valueFontSize;
        private double _upperLimitValue;
        private double _lowerLimitValue;
        private string _groupColor;
        private int _groupFontSize;
        private int _groupSeparators;
        private string _upperLimitColor;
        private string _lowerLimitColor;
        private string _groupNameFormat;

        public string Identifier { get; }

        public EOverlayEntryType OverlayEntryType { get; set; }

        public string Description { get; set; }

        public object Value { get; set; }

        [JsonIgnore]
        public bool FormatChanged { get; set; }

        [JsonIgnore]
        public bool IsNumeric { get; set; }

        [JsonIgnore]
        public Action UpdateGroupName { get; set; }

        [JsonIgnore]
        public string FormattedValue
            => string.IsNullOrWhiteSpace(ValueFormat) ?
            (Value == null ? string.Empty : Value.ToString())
            : string.Format(CultureInfo.InvariantCulture, ValueFormat, Value);

        public string ValueFormat { get; set; }

        public bool ShowOnOverlay
        {
            get { return _showOnOverlay; }
            set
            {
                _showOnOverlay = value;
                RaisePropertyChanged();
            }
        }

        public bool ShowOnOverlayIsEnabled
        {
            get { return _showOnOverlayIsEnabled; }
            set
            {
                _showOnOverlayIsEnabled = value;
                RaisePropertyChanged();
            }
        }

        public string GroupName
        {
            get { return _groupName; }
            set
            {
                _groupName = value;
                RaisePropertyChanged();
                UpdateGroupName?.Invoke();
            }
        }

        public bool ShowGraph
        {
            get { return _showGraph; }
            set
            {
                _showGraph = value;
                RaisePropertyChanged();
            }
        }

        public bool ShowGraphIsEnabled
        {
            get { return _showGraphIsEnabled; }
            set
            {
                _showGraphIsEnabled = value;
                RaisePropertyChanged();
            }
        }

        /// <summary>
        /// value display color in hex format
        /// </summary>
        public string Color
        {
            get
            {
                return string.IsNullOrWhiteSpace(_color)
                  ? GetDefaultValueColor() : _color;
            }
            set
            {
                FormatChanged = _color != value;
                _color = value;
                RaisePropertyChanged();
            }
        }

        public int ValueFontSize
        {
            get { return _valueFontSize == 0 ? 50 : _valueFontSize; }
            set
            {
                FormatChanged = _valueFontSize != value;
                _valueFontSize = value;
                RaisePropertyChanged();
            }
        }

        public string GroupNameFormat
        {
            get { return _groupNameFormat; }
            set
            {
                _groupNameFormat = value;
                RaisePropertyChanged();
            }
        }

        [JsonIgnore]
        public string FormattedGroupName
        => string.IsNullOrWhiteSpace(GroupNameFormat) ?
        (GroupName == null ? string.Empty : GroupName.ToString())
        : string.Format(CultureInfo.InvariantCulture, GroupNameFormat, GroupName);

        public double UpperLimitValue
        {
            get { return _upperLimitValue; }
            set
            {
                _upperLimitValue = value;
                RaisePropertyChanged();
            }
        }

        public double LowerLimitValue
        {
            get { return _lowerLimitValue; }
            set
            {
                _lowerLimitValue = value;
                RaisePropertyChanged();
            }
        }

        public string GroupColor
        {
            get { return string.IsNullOrWhiteSpace(_groupColor) 
                    ? GetDefaultGroupColor() : _groupColor; }
            set
            {
                FormatChanged = _groupColor != value;
                _groupColor = value;
                RaisePropertyChanged();
            }
        }

        public int GroupFontSize
        {
            get { return _groupFontSize == 0 ? 50 : _groupFontSize; }
            set
            {
                FormatChanged = _groupFontSize != value;
                _groupFontSize = value;
                RaisePropertyChanged();
            }
        }

        public int GroupSeparators
        {
            get { return _groupSeparators; }
            set
            {
                FormatChanged = _groupSeparators != value;
                _groupSeparators = value;
                RaisePropertyChanged();
            }
        }

        public string UpperLimitColor
        {
            get { return _upperLimitColor; }
            set
            {
                FormatChanged = _upperLimitColor != value;
                _upperLimitColor = value;
                RaisePropertyChanged();
            }
        }

        public string LowerLimitColor
        {
            get { return _lowerLimitColor; }
            set
            {
                FormatChanged = _lowerLimitColor != value;
                _lowerLimitColor = value;
                RaisePropertyChanged();
            }
        }

        public OverlayEntryWrapper(string identifier)
        {
            Identifier = identifier;
        }

        private string GetDefaultValueColor()
        {
            //strOSD += "<C1=AEEA00>"; //CX Green
            //strOSD += "<C2=FFFFFF>"; // White
            //strOSD += "<C3=2297F3>"; //CX Blue
            //strOSD += "<C4=F17D20>"; //CX Orange

            return Identifier == "RunHistory" ? "2297F3"
                : Identifier == "CaptureServiceStatus" ? "2297F3"
                : Identifier == "CaptureTimer" ? "F17D20"
                : Identifier == "Framerate" ? "AEEA00"
                : Identifier == "Frametime" ? "AEEA00"
                // all other items
                : "F17D20";
        }

        private string GetDefaultGroupColor()
        {
            //strOSD += "<C1=AEEA00>"; //CX Green
            //strOSD += "<C2=FFFFFF>"; // White
            //strOSD += "<C3=2297F3>"; //CX Blue
            //strOSD += "<C4=F17D20>"; //CX Orange

            return Identifier == "RunHistory" ? "FFFFFF"
                : Identifier == "CaptureServiceStatus" ? "FFFFFF"
                : Identifier == "CaptureTimer" ? "FFFFFF"
                : Identifier == "Framerate" ? "AEEA00"
                : Identifier == "Frametime" ? "AEEA00"
                // all other items
                : "FFFFFF";
        }
    }
}

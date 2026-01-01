using CapFrameX.Contracts.Overlay;
using Newtonsoft.Json;
using Prism.Mvvm;
using System;
using System.Globalization;

namespace CapFrameX.Overlay
{
    public class OverlayEntryWrapper : BindableBase, IOverlayEntry
    {
        private const int DEFAULT_FONTSIZE = 100;
        private readonly object _fieldLock = new object();
        private readonly object _highDynamicFieldLock = new object();
        private readonly object _valueLock = new object();
        private readonly object _limitsLock = new object();

        private bool _isEntryEnabled = true;
        private bool _showOnOverlay;
        private bool _showOnOverlayIsEnabled;
        private string _groupName;
        private bool _showGraph;
        private bool _showGraphIsEnabled;
        private string _color;
        private int _valueFontSize;
        private string _upperLimitValue = string.Empty;
        private string _lowerLimitValue = string.Empty;
        private string _groupColor;
        private int _groupFontSize;
        private int _groupSeparators;
        private string _upperLimitColor;
        private string _lowerLimitColor;
        private string _groupNameFormat;
        private string _valueFormat;
        private LimitState _lastLimitState = LimitState.Undefined;
        private string _valueUnitFormat;
        private string _valueAlignmentAndDigits;
        private bool _isNumeric;
        private bool _formatChanged;
        private object _value;

        public string Identifier { get; }

        public string SortKey { get; set; } = "0_0_0_0_0";

        public EOverlayEntryType OverlayEntryType { get; set; }

        public string Description { get; set; }

        [JsonIgnore]
        public Action PropertyChangedAction { set; get; }

        [JsonIgnore]
        public Action<string, bool> UpdateShowOnOverlay { set; get; }

        [JsonIgnore]
        public object Value
        {
            get
            {
                lock (_valueLock)
                    return _value;
            }
            set
            {
                lock (_valueLock)
                    _value = value;
            }
        }

        [JsonIgnore]
        public bool FormatChanged
        {
            get
            {
                lock (_highDynamicFieldLock)
                    return _formatChanged;
            }
            set
            {
                lock (_highDynamicFieldLock)
                    _formatChanged = value;
            }
        }

        [JsonIgnore]
        public bool IsNumeric
        {
            get
            {
                lock (_fieldLock)
                    return _isNumeric;
            }
            set
            {
                lock (_fieldLock)
                    _isNumeric = value;
            }
        }

        [JsonIgnore]
        public Action<string> UpdateGroupName { get; set; }

        [JsonIgnore]
        public string ValueAlignmentAndDigits
        {
            get
            {
                lock (_highDynamicFieldLock)
                    return _valueAlignmentAndDigits;
            }
            set
            {
                lock (_highDynamicFieldLock)
                    _valueAlignmentAndDigits = value;
            }
        }

        [JsonIgnore]
        public string ValueUnitFormat
        {
            get
            {
                lock (_highDynamicFieldLock)
                    return _valueUnitFormat;
            }
            set
            {
                lock (_highDynamicFieldLock)
                    _valueUnitFormat = value;
            }
        }

        [JsonIgnore]
        public LimitState LastLimitState
        {
            get
            {
                lock (_highDynamicFieldLock)
                    return _lastLimitState;
            }
            set
            {
                lock (_highDynamicFieldLock)
                    _lastLimitState = value;
            }
        }

        [JsonIgnore]
        public string FormattedValue
            => string.IsNullOrWhiteSpace(ValueFormat) ?
            (Value == null ? string.Empty : Value.ToString())
            : string.Format(CultureInfo.InvariantCulture, ValueFormat, Value);

        public string ValueFormat
        {
            get
            {
                lock (_highDynamicFieldLock)
                    return _valueFormat;
            }
            set
            {
                lock (_highDynamicFieldLock)
                    _valueFormat = value;
            }
        }

        public bool IsEntryEnabled { get; set; }

        public bool ShowOnOverlay
        {
            get
            {
                lock (_fieldLock)
                    return _showOnOverlay;
            }
            set
            {
                lock (_fieldLock)
                    _showOnOverlay = value;
                UpdateShowOnOverlay?.Invoke(Identifier, value);
                RaisePropertyChanged();
            }
        }

        public bool ShowOnOverlayIsEnabled
        {
            get
            {
                lock (_fieldLock)
                    return _showOnOverlayIsEnabled;
            }
            set
            {
                lock (_fieldLock)
                    _showOnOverlayIsEnabled = value;
                RaisePropertyChanged();
            }
        }

        public string GroupName
        {
            get
            {
                lock (_fieldLock)
                    return _groupName;
            }
            set
            {
                lock (_fieldLock)
                    _groupName = value;
                RaisePropertyChanged();
                UpdateGroupName?.Invoke(value);
            }
        }

        public bool ShowGraph
        {
            get
            {
                lock (_fieldLock)
                    return _showGraph;
            }
            set
            {
                lock (_fieldLock)
                    _showGraph = value;
                RaisePropertyChanged();
            }
        }

        public bool ShowGraphIsEnabled
        {
            get
            {
                lock (_fieldLock)
                    return _showGraphIsEnabled;
            }
            set
            {
                lock (_fieldLock)
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
                lock (_fieldLock)
                    return string.IsNullOrWhiteSpace(_color)
                        ? GetDefaultValueColor() : _color;
            }
            set
            {
                FormatChanged = _color != value;
                lock (_fieldLock)
                    _color = value;
                RaisePropertyChanged();
            }
        }

        public int ValueFontSize
        {
            get
            {
                lock (_fieldLock)
                    return _valueFontSize == 0 ? DEFAULT_FONTSIZE : _valueFontSize;
            }
            set
            {
                FormatChanged = _valueFontSize != value;
                lock (_fieldLock)
                    _valueFontSize = value;
                RaisePropertyChanged();
            }
        }

        public string GroupNameFormat
        {
            get
            {
                lock (_highDynamicFieldLock)
                    return _groupNameFormat;
            }
            set
            {
                lock (_highDynamicFieldLock)
                    _groupNameFormat = value;
                RaisePropertyChanged();
            }
        }

        [JsonIgnore]
        public string FormattedGroupName
        => string.IsNullOrWhiteSpace(GroupNameFormat) ?
            (GroupName == null ? string.Empty : GroupName.ToString())
            : string.Format(CultureInfo.InvariantCulture, GroupNameFormat, GroupName);

        public string UpperLimitValue
        {
            get
            {
                lock (_limitsLock)
                    return _upperLimitValue;
            }
            set
            {
                FormatChanged = _upperLimitValue != value;               
                lock (_limitsLock)
                    _upperLimitValue = value;
                RaisePropertyChanged();
            }
        }

        public string LowerLimitValue
        {
            get
            {
                lock (_limitsLock)
                    return _lowerLimitValue;
            }
            set
            {
                FormatChanged = _lowerLimitValue != value;
                lock (_limitsLock)
                    _lowerLimitValue = value;                   
                RaisePropertyChanged();
            }
        }

        public string GroupColor
        {
            get
            {
                lock (_fieldLock)
                    return string.IsNullOrWhiteSpace(_groupColor)
                        ? GetDefaultGroupColor() : _groupColor;
            }
            set
            {
                FormatChanged = _groupColor != value;
                lock (_fieldLock)
                    _groupColor = value;
                RaisePropertyChanged();
            }
        }

        public int GroupFontSize
        {
            get
            {
                lock (_fieldLock)
                    return _groupFontSize == 0 ? DEFAULT_FONTSIZE : _groupFontSize;
            }
            set
            {
                FormatChanged = _groupFontSize != value;
                lock (_fieldLock)
                    _groupFontSize = value;
                RaisePropertyChanged();
            }
        }

        public int GroupSeparators
        {
            get
            {
                lock (_fieldLock)
                    return _groupSeparators;
            }
            set
            {
                FormatChanged = _groupSeparators != value;
                lock (_fieldLock)
                    _groupSeparators = value;
                RaisePropertyChanged();
            }
        }

        public string UpperLimitColor
        {
            get
            {
                lock (_fieldLock)
                    return string.IsNullOrWhiteSpace(_upperLimitColor)
                        ? GetDefaultLimitColor() : _upperLimitColor;
            }
            set
            {
                FormatChanged = _upperLimitColor != value;
                lock (_fieldLock)
                    _upperLimitColor = value;
                RaisePropertyChanged();
            }
        }

        public string LowerLimitColor
        {
            get
            {
                lock (_fieldLock)
                    return string.IsNullOrWhiteSpace(_lowerLimitColor)
                      ? GetDefaultLimitColor() : _lowerLimitColor;
            }
            set
            {
                FormatChanged = _lowerLimitColor != value;
                lock (_fieldLock)
                    _lowerLimitColor = value;
                RaisePropertyChanged();
            }
        }

        public OverlayEntryWrapper(string identifier)
        {
            Identifier = identifier;
            PropertyChanged += (s, e) => PropertyChangedAction?.Invoke();
        }

        public IOverlayEntry Clone()
        {
            return new OverlayEntryWrapper(Identifier)
            {
                SortKey = SortKey,
                OverlayEntryType = OverlayEntryType,
                Description = Description,
                ValueFormat = ValueFormat,
                IsEntryEnabled = IsEntryEnabled,
                ShowOnOverlay = ShowOnOverlay,
                ShowOnOverlayIsEnabled = ShowOnOverlayIsEnabled,
                GroupName = GroupName,
                ShowGraph = ShowGraph,
                ShowGraphIsEnabled = ShowGraphIsEnabled,
                Color = Color,
                ValueFontSize = ValueFontSize,
                GroupNameFormat = GroupNameFormat,
                UpperLimitValue = UpperLimitValue,
                LowerLimitValue = LowerLimitValue,
                GroupColor = GroupColor,
                GroupFontSize = GroupFontSize,
                GroupSeparators = GroupSeparators,
                UpperLimitColor = UpperLimitColor,
                LowerLimitColor = LowerLimitColor
            };
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
                : Identifier == "SystemTime" ? "2297F3"
                : Identifier == "Framerate" ? "AEEA00"
                : Identifier == "Frametime" ? "AEEA00"
                // all other items
                : "F17D20";
        }

        private string GetDefaultLimitColor()
        {
            return "FFC80000";
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

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

        private readonly System.ComponentModel.PropertyChangedEventHandler _propertyChangedHandler;
        private volatile bool _disposed;
        private volatile bool _showOnOverlay;
        private volatile bool _showOnOverlayIsEnabled;
        private volatile string _groupName;
        private volatile bool _showGraph;
        private volatile bool _showGraphIsEnabled;
        private volatile string _color;
        private volatile int _valueFontSize;
        private volatile string _upperLimitValue = string.Empty;
        private volatile string _lowerLimitValue = string.Empty;
        private volatile string _groupColor;
        private volatile int _groupFontSize;
        private volatile int _groupSeparators;
        private volatile string _upperLimitColor;
        private volatile string _lowerLimitColor;
        private volatile string _groupNameFormat;
        private volatile string _valueFormat;
        private volatile LimitState _lastLimitState = LimitState.Undefined;
        private volatile string _valueUnitFormat;
        private volatile string _valueAlignmentAndDigits;
        private volatile bool _isNumeric;
        private volatile bool _formatChanged;
        private volatile object _value;

        public string Identifier { get; }

        public string StableIdentifier { get; set; }

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
            get => _value;
            set => _value = value;
        }

        [JsonIgnore]
        public bool FormatChanged
        {
            get => _formatChanged;
            set => _formatChanged = value;
        }

        [JsonIgnore]
        public bool IsNumeric
        {
            get => _isNumeric;
            set => _isNumeric = value;
        }

        [JsonIgnore]
        public Action<string> UpdateGroupName { get; set; }

        [JsonIgnore]
        public string ValueAlignmentAndDigits
        {
            get => _valueAlignmentAndDigits;
            set => _valueAlignmentAndDigits = value;
        }

        [JsonIgnore]
        public string ValueUnitFormat
        {
            get => _valueUnitFormat;
            set => _valueUnitFormat = value;
        }

        [JsonIgnore]
        public LimitState LastLimitState
        {
            get => _lastLimitState;
            set => _lastLimitState = value;
        }

        [JsonIgnore]
        public string FormattedValue
            => string.IsNullOrWhiteSpace(ValueFormat) ?
            (Value == null ? string.Empty : Value.ToString())
            : string.Format(CultureInfo.InvariantCulture, ValueFormat, Value);

        public string ValueFormat
        {
            get => _valueFormat;
            set => _valueFormat = value;
        }

        public bool IsEntryEnabled { get; set; } = true;

        public bool ShowOnOverlay
        {
            get => _showOnOverlay;
            set
            {
                _showOnOverlay = value;
                UpdateShowOnOverlay?.Invoke(Identifier, value);
                RaisePropertyChanged();
            }
        }

        public bool ShowOnOverlayIsEnabled
        {
            get => _showOnOverlayIsEnabled;
            set
            {
                _showOnOverlayIsEnabled = value;
                RaisePropertyChanged();
            }
        }

        public string GroupName
        {
            get => _groupName;
            set
            {
                _groupName = value;
                RaisePropertyChanged();
                UpdateGroupName?.Invoke(value);
            }
        }

        public bool ShowGraph
        {
            get => _showGraph;
            set
            {
                _showGraph = value;
                RaisePropertyChanged();
            }
        }

        public bool ShowGraphIsEnabled
        {
            get => _showGraphIsEnabled;
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
            get => string.IsNullOrWhiteSpace(_color) ? GetDefaultValueColor() : _color;
            set
            {
                FormatChanged = _color != value;
                _color = value;
                RaisePropertyChanged();
            }
        }

        public int ValueFontSize
        {
            get => _valueFontSize == 0 ? DEFAULT_FONTSIZE : _valueFontSize;
            set
            {
                FormatChanged = _valueFontSize != value;
                _valueFontSize = value;
                RaisePropertyChanged();
            }
        }

        public string GroupNameFormat
        {
            get => _groupNameFormat;
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

        public string UpperLimitValue
        {
            get => _upperLimitValue;
            set
            {
                FormatChanged = _upperLimitValue != value;
                _upperLimitValue = value;
                RaisePropertyChanged();
            }
        }

        public string LowerLimitValue
        {
            get => _lowerLimitValue;
            set
            {
                FormatChanged = _lowerLimitValue != value;
                _lowerLimitValue = value;
                RaisePropertyChanged();
            }
        }

        public string GroupColor
        {
            get => string.IsNullOrWhiteSpace(_groupColor) ? GetDefaultGroupColor() : _groupColor;
            set
            {
                FormatChanged = _groupColor != value;
                _groupColor = value;
                RaisePropertyChanged();
            }
        }

        public int GroupFontSize
        {
            get => _groupFontSize == 0 ? DEFAULT_FONTSIZE : _groupFontSize;
            set
            {
                FormatChanged = _groupFontSize != value;
                _groupFontSize = value;
                RaisePropertyChanged();
            }
        }

        public int GroupSeparators
        {
            get => _groupSeparators;
            set
            {
                FormatChanged = _groupSeparators != value;
                _groupSeparators = value;
                RaisePropertyChanged();
            }
        }

        public string UpperLimitColor
        {
            get => string.IsNullOrWhiteSpace(_upperLimitColor) ? GetDefaultLimitColor() : _upperLimitColor;
            set
            {
                FormatChanged = _upperLimitColor != value;
                _upperLimitColor = value;
                RaisePropertyChanged();
            }
        }

        public string LowerLimitColor
        {
            get => string.IsNullOrWhiteSpace(_lowerLimitColor) ? GetDefaultLimitColor() : _lowerLimitColor;
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
            _propertyChangedHandler = (s, e) => PropertyChangedAction?.Invoke();
            PropertyChanged += _propertyChangedHandler;
        }

        public IOverlayEntry Clone()
        {
            return new OverlayEntryWrapper(Identifier)
            {
                StableIdentifier = StableIdentifier,
                SortKey = SortKey,
                Value = Value,
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
                LowerLimitColor = LowerLimitColor,
                IsNumeric = IsNumeric,
                ValueAlignmentAndDigits = ValueAlignmentAndDigits,
                ValueUnitFormat = ValueUnitFormat,
                LastLimitState = LastLimitState
            };
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            PropertyChanged -= _propertyChangedHandler;
            PropertyChangedAction = null;
            UpdateShowOnOverlay = null;
            UpdateGroupName = null;

            _disposed = true;
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

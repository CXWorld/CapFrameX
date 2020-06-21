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
		private object _value;
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

		[JsonIgnore]
		public IOverlayEntryProvider OverlayEntryProvider { get; set; }

		public string Identifier { get; }

		public EOverlayEntryType OverlayEntryType { get; set; }

		public string Description { get; set; }

		[JsonIgnore]
		public Action<string, string> UpdateGroupName { get; set; }

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
				UpdateGroupName?.Invoke(_groupName, value);
				_groupName = value;
				RaisePropertyChanged();
			}
		}

		public object Value
		{
			get { return _value; }
			set
			{
				_value = value;
				RaisePropertyChanged();
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
			get { return _color; }
			set
			{
				_color = value;
				RaisePropertyChanged();
			}
		}

		public int ValueFontSize
		{
			get { return _valueFontSize; }
			set
			{
				_valueFontSize = value;
				RaisePropertyChanged();
			}
		}

		public string GroupNameFormat { get; set; }

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
			get { return _groupColor; }
			set
			{
				_groupColor = value;
				RaisePropertyChanged();
			}
		}

		public int GroupFontSize
		{
			get { return _groupFontSize; }
			set
			{
				_groupFontSize = value;
				RaisePropertyChanged();
			}
		}

		public int GroupSeparators
		{
			get { return _groupSeparators; }
			set
			{
				_groupSeparators = value;
				RaisePropertyChanged();
			}
		}

		public string UpperLimitColor
		{
			get { return _upperLimitColor; }
			set
			{
				_upperLimitColor = value;
				RaisePropertyChanged();
			}
		}

		public string LowerLimitColor
		{
			get { return _lowerLimitColor; }
			set
			{
				_lowerLimitColor = value;
				RaisePropertyChanged();
			}
		}

		public OverlayEntryWrapper(string identifier)
		{
			Identifier = identifier;
		}
	}
}

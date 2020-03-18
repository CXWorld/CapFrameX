using CapFrameX.Contracts.Overlay;
using Newtonsoft.Json;
using Prism.Mvvm;
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

		[JsonIgnore]
		public IOverlayEntryProvider OverlayEntryProvider { get; set; }

		public string Identifier { get; }

		public EOverlayEntryType OverlayEntryType { get; set; }

		public string Description { get; set; }	

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
				OverlayEntryProvider?.EntryUpdateStream.OnNext(default);
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
				OverlayEntryProvider?.EntryUpdateStream.OnNext(default);
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
				OverlayEntryProvider?.EntryUpdateStream.OnNext(default);
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
		/// Display color in hex format
		/// </summary>
		public string Color
		{
			get { return _color; }
			set
			{
				_color = value;
				OverlayEntryProvider?.EntryUpdateStream.OnNext(default);
				RaisePropertyChanged();
			}
		}

		public OverlayEntryWrapper(string identifier)
		{
			Identifier = identifier;
		}
	}
}

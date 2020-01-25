using CapFrameX.Contracts.Overlay;
using Newtonsoft.Json;
using Prism.Mvvm;
using System.Reactive;

namespace CapFrameX.Overlay
{
	public class OverlayEntryWrapper : BindableBase, IOverlayEntry
	{
		private string _valueFormat;
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

		public string Description { get; set; }	

		[JsonIgnore]
		public string FormattedValue
			=> _valueFormat == null ?
			(Value == null ? string.Empty : Value.ToString())
			: string.Format(_valueFormat, Value);

		public bool ShowOnOverlay
		{
			get { return _showOnOverlay; }
			set
			{
				_showOnOverlay = value;
				OverlayEntryProvider?.EntryUpdateStream.OnNext(default(Unit));
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
				OverlayEntryProvider?.EntryUpdateStream.OnNext(default(Unit));
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
				OverlayEntryProvider?.EntryUpdateStream.OnNext(default(Unit));
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
				OverlayEntryProvider?.EntryUpdateStream.OnNext(default(Unit));
				RaisePropertyChanged();
			}
		}

		public OverlayEntryWrapper(string identifier, string format = null)
		{
			Identifier = identifier;
			_valueFormat = format;
		}
	}
}

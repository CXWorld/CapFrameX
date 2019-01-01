using CapFrameX.Extensions;
using CapFrameX.OcatInterface;
using CapFrameX.ViewModel;
using Prism.Events;
using System;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Windows.Controls;
using System.Windows.Data;

namespace CapFrameX.View
{
	/// <summary>
	/// Interaktionslogik für ControlView.xaml
	/// </summary>
	public partial class ControlView : UserControl
	{
		private const int SEARCH_REFRESH_DELAY_MS = 100;
		private readonly CollectionViewSource _recordInfoCollection;

		public ControlView()
		{
			InitializeComponent();

			_recordInfoCollection = (CollectionViewSource)Resources["RecordInfoListKey"];
			Observable.FromEventPattern<TextChangedEventArgs>(RecordSearchBox, "TextChanged")
				.Throttle(TimeSpan.FromMilliseconds(SEARCH_REFRESH_DELAY_MS))
				.ObserveOnDispatcher()
				.Subscribe(t => _recordInfoCollection.View.Refresh());

			// Design time!
			if (DesignerProperties.GetIsInDesignMode(this))
			{
				DataContext = new ControlViewModel(new RecordDirectoryObserver(), new EventAggregator());
			}
		}

		private void RecordInfoListOnFilter(object sender, FilterEventArgs e)
		{
			e.FilterCollectionByText<OcatRecordInfo>(RecordSearchBox.Text,
										(record, word) => record.FullPath.NullSafeContains(word, true));
		}

		private void DataGridRow_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			(DataContext as ControlViewModel).OnRecordSelectByDoubleClick();
		}
	}
}

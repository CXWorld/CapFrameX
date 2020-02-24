using CapFrameX.Configuration;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Data;
using CapFrameX.Extensions;
using CapFrameX.ViewModel;
using Microsoft.Extensions.Logging;
using Prism.Events;
using System;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Controls;
using System.Windows.Data;

namespace CapFrameX.View
{
	/// <summary>
	/// Interaction logic for ControlView.xaml
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
				var appConfiguration = new CapFrameXConfiguration();
				var loggerFactory = new LoggerFactory();

				var recordDirectoryObserver = new RecordDirectoryObserver(appConfiguration,
					loggerFactory.CreateLogger<RecordDirectoryObserver>());

                DataContext = new ControlViewModel(new RecordDirectoryObserver(appConfiguration,
					loggerFactory.CreateLogger<RecordDirectoryObserver>()), new EventAggregator(), 
                    new CapFrameXConfiguration(), new RecordManager(loggerFactory.CreateLogger<RecordManager>(), appConfiguration, recordDirectoryObserver, new AppVersionProvider()));
			}

			SetSortSettings((DataContext as ControlViewModel).AppConfiguration);
		}

		private void SetSortSettings(IAppConfiguration appConfiguration)
		{
			string sortMemberPath = appConfiguration.RecordingListSortMemberPath;
			var direction = appConfiguration.RecordingListSortDirection.ConvertToEnum<ListSortDirection>();
			var collectionView = CollectionViewSource.GetDefaultView(RecordDataGrid.ItemsSource);

			collectionView.SortDescriptions.Clear();
			AddSortColumn(RecordDataGrid, sortMemberPath, direction);
			AddSortColumnsByMemberPath(RecordDataGrid, direction, sortMemberPath);
		}

		private void RecordDataGrid_Sorting(object sender, DataGridSortingEventArgs e)
		{
			var dataGrid = (DataGrid)sender;
			var appConfiguration = (DataContext as ControlViewModel).AppConfiguration;
			var collectionView = CollectionViewSource.GetDefaultView(dataGrid.ItemsSource);

			ListSortDirection direction = ListSortDirection.Ascending;
			if (collectionView.SortDescriptions.FirstOrDefault().PropertyName == e.Column.SortMemberPath)
				direction = collectionView.SortDescriptions.FirstOrDefault().Direction ==
					ListSortDirection.Descending ? ListSortDirection.Ascending : ListSortDirection.Descending;

			collectionView.SortDescriptions.Clear();
			AddSortColumn((DataGrid)sender, e.Column.SortMemberPath, direction);
			AddSortColumnsByMemberPath((DataGrid)sender, direction, e.Column.SortMemberPath);

			appConfiguration.RecordingListSortMemberPath = e.Column.SortMemberPath;
			appConfiguration.RecordingListSortDirection = direction.ConvertToString();

			e.Handled = true;
		}

		private void AddSortColumn(DataGrid sender, string sortColumn, ListSortDirection direction)
		{
			var collectionView = CollectionViewSource.GetDefaultView(sender.ItemsSource);
			collectionView.SortDescriptions.Add(new SortDescription(sortColumn, direction));

			foreach (var col in sender.Columns.Where(x => x.SortMemberPath == sortColumn))
			{
				col.SortDirection = direction;
			}
		}

		private void AddSortColumnsByMemberPath(DataGrid dataGrid, ListSortDirection sortDirection, string sortMemberPath)
		{
			if (sortMemberPath == "GameName")
			{
				AddSortColumn(dataGrid, "CreationDate", sortDirection);
				AddSortColumn(dataGrid, "CreationTime", sortDirection);
			}

			if (sortMemberPath == "CreationDate")
			{
				AddSortColumn(dataGrid, "CreationTime", sortDirection);
			}
		}

		private void RecordInfoListOnFilter(object sender, FilterEventArgs e)
		{
			e.FilterCollectionByText<IFileRecordInfo>(RecordSearchBox.Text,
										(record, word) => record.CombinedInfo.NullSafeContains(word, true));
		}

		private void DataGridRow_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			(DataContext as ControlViewModel).OnRecordSelectByDoubleClick();
		}
	}
}

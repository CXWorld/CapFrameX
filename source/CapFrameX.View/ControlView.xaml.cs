using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Extensions;
using CapFrameX.ViewModel;
using System;
using System.IO;
using System.Windows;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace CapFrameX.View
{
	/// <summary>
	/// Interaction logic for ControlView.xaml
	/// </summary>
	public partial class ControlView : UserControl
	{
		private const int SEARCH_REFRESH_DELAY_MS = 100;
		private readonly CollectionViewSource _recordInfoCollection;

		private string ObservedDirectory
			=> (DataContext as ControlViewModel).AppConfiguration.ObservedDirectory;
		private string CaptureRootDirectory
			=> (DataContext as ControlViewModel).AppConfiguration.CaptureRootDirectory;

		public ControlView()
		{
			InitializeComponent();

			_recordInfoCollection = (CollectionViewSource)Resources["RecordInfoListKey"];
			Observable.FromEventPattern<TextChangedEventArgs>(RecordSearchBox, "TextChanged")
				.Throttle(TimeSpan.FromMilliseconds(SEARCH_REFRESH_DELAY_MS))
				.ObserveOnDispatcher()
				.Subscribe(t => _recordInfoCollection.View.Refresh());

			(DataContext as ControlViewModel).TreeViewUpdateStream.Subscribe(_ => BuildTreeView());

			BuildTreeView();
			SetSortSettings((DataContext as ControlViewModel).AppConfiguration);
		}

		private void BuildTreeView()
		{
			var root = CreateTreeViewRoot();
			CreateTreeViewRecursive(trvStructure.Items[0] as TreeViewItem);
			JumpToObservedDirectoryItem(root);

			if ((CaptureRootDirectory == ObservedDirectory) || (!(DataContext as ControlViewModel).HasValidSource))
				root.IsSelected = true;
		}

		private TreeViewItem CreateTreeViewRoot()
		{
			trvStructure.Items.Clear();
			var mainfoldername = new DirectoryInfo(ExtractFullPath(CaptureRootDirectory));
			var rootNode = CreateTreeItem(mainfoldername, mainfoldername.Name);
			trvStructure.Items.Add(rootNode);
			rootNode.IsExpanded = true;
			return rootNode;
		}

		private TreeViewItem CreateTreeItem(object o, string name)
		{
			TreeViewItem item = new TreeViewItem
			{
				Header = name,
				Tag = o
			};
			item.Items.Add("Loading...");
			return item;
		}

		private void CreateTreeViewRecursive(TreeViewItem item)
		{
			if ((item.Items.Count == 1) && (item.Items[0] is string))
			{
				item.Items.Clear();

				DirectoryInfo expandedDir = null;
				if (item.Tag is DriveInfo)
					expandedDir = (item.Tag as DriveInfo).RootDirectory;
				if (item.Tag is DirectoryInfo)
					expandedDir = (item.Tag as DirectoryInfo);
				try
				{
					foreach (DirectoryInfo subDir in expandedDir.GetDirectories())
					{
						var subItem = CreateTreeItem(subDir, subDir.ToString());
						item.Items.Add(subItem);
						CreateTreeViewRecursive(subItem);
					}
				}
				catch { }
			}
		}

		void JumpToObservedDirectoryItem(TreeViewItem tvi)
		{
			if (tvi == null)
				return;

			if ((tvi.Tag as DirectoryInfo).FullName == ObservedDirectory)
			{
				tvi.BringIntoView();
				tvi.IsSelected = true;
				return;
			}
			else
				tvi.IsExpanded = false;

			if (tvi.HasItems)
			{
				foreach (var item in tvi.Items)
				{
					TreeViewItem temp = item as TreeViewItem;
					JumpToObservedDirectoryItem(temp);
				}
			}
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
				AddSortColumn(dataGrid, "CreationTimestamp", sortDirection);
			}

		}

		private void RecordInfoListOnFilter(object sender, FilterEventArgs e)
		{
			e.FilterCollectionByText<IFileRecordInfo>(RecordSearchBox.Text,
										(record, word) => record.CombinedInfo.NullSafeContains(word, true));
		}

		private void DataGridRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			(DataContext as ControlViewModel).OnRecordSelectByDoubleClick();
		}

		private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
		{
			ScrollViewer scv = (ScrollViewer)sender;
			scv.ScrollToVerticalOffset(scv.VerticalOffset - e.Delta / 10);
			e.Handled = true;
		}



		private void TreeViewItem_Selected(object sender, RoutedEventArgs e)
		{
			TreeViewItem item = e.Source as TreeViewItem;
			(DataContext as ControlViewModel).RecordObserver.ObserveDirectory((item.Tag as DirectoryInfo).FullName);
		}

		private string ExtractFullPath(string path)
		{
			if (path.Contains(@"MyDocuments\CapFrameX\Captures"))
			{
				var documentFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
				path = Path.Combine(documentFolder, @"CapFrameX\Captures");
			}

			return path;
		}

		private void RootFolder_PreviewMouseDown(object sender, MouseButtonEventArgs e)
		{
			var result = (DataContext as ControlViewModel).OnSelectRootFolder();
			if (result)
			{
				BuildTreeView();
			}
		}

		private void TreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
		{
			TreeViewItem treeViewItem = VisualUpwardSearch(e.OriginalSource as DependencyObject);

			if (treeViewItem != null)
			{
				treeViewItem.Focus();
				e.Handled = true;
			}
		}

		static TreeViewItem VisualUpwardSearch(DependencyObject source)
		{
			while (source != null && !(source is TreeViewItem))
				source = VisualTreeHelper.GetParent(source);

			return source as TreeViewItem;
		}

		private void TextBox_KeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key != Key.Enter)
				return;

			Keyboard.ClearFocus();
			(DataContext as ControlViewModel).SaveDescriptions();
			e.Handled = true;
		}

		private void Expander_MouseLeave(object sender, MouseEventArgs e)
		{
			var result = (DataContext as ControlViewModel).CreateFolderDialogIsOpen;
			{
				if (Expander.IsExpanded && !result && !trvStructure.ContextMenu.IsOpen)
				Expander.IsExpanded = false;
			}
		}

		private void DescriptionGrid_GotFocus(object sender, RoutedEventArgs e)
		{
			Expander.IsExpanded = false;
		}
	}
}

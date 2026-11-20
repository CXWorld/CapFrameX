using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Extensions;
using CapFrameX.Extensions.NetStandard;
using CapFrameX.View.Controls;
using CapFrameX.ViewModel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows;
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
        private ControlViewModel _viewModel => DataContext as ControlViewModel;

        private const int SEARCH_REFRESH_DELAY_MS = 100;
        private readonly CollectionViewSource _recordInfoCollection;

        private bool RecordInfoExpanderinitialPosition => _viewModel.AppConfiguration.IsRecordInfoExpanded;

        private string ObservedDirectory
            => _viewModel.AppConfiguration.ObservedDirectory;
        private string CaptureRootDirectory
            => _viewModel.AppConfiguration.CaptureRootDirectory;

        private bool IsDragDropActive;
        private TreeViewItem ObservedTreeViewItem;

        public ControlView()
        {
            InitializeComponent();
            SetHeaders();

            if (RecordInfoExpanderinitialPosition)
                RecordInfoExpander.IsExpanded = true;

            _recordInfoCollection = (CollectionViewSource)Resources["RecordInfoListKey"];
            Observable.FromEventPattern<TextChangedEventArgs>(RecordSearchBox, "TextChanged")
                .Throttle(TimeSpan.FromMilliseconds(SEARCH_REFRESH_DELAY_MS))
                .ObserveOnDispatcher()
                .Subscribe(t => _recordInfoCollection.View.Refresh());

            _viewModel.TreeViewUpdateStream.Subscribe(_ => BuildTreeView());

            // The create-folder dialog is shown by a view-level DialogHost, so the
            // folder popup closes when the dialog opens (it would float above it).
            _viewModel.CreateFolderdialogIsOpenStream
                .Where(isOpen => isOpen)
                .ObserveOnDispatcher()
                .Subscribe(_ => FolderPopup.IsOpen = false);

            BuildTreeView();
            SetSortSettings(_viewModel.AppConfiguration);
        }

        private void BuildTreeView()
        {
            // The tree holds the capture root and the folders below it. A root that is gone is
            // replaced before the tree is built, an observed folder the tree does not hold after:
            // one that was deleted or moved outside CapFrameX, or that came with the settings of
            // another PC, user or installation. Until 2026-11-20 this method called itself until
            // the observed folder was found, and with an existing root it changed nothing in
            // between: the stack overflowed at every start (issue 485).
            var root = RecordFolderTree.Build(TrvStructure, _viewModel.EnsureRootDirectory());
            var observed = RecordFolderTree.Find(root, ExtractFullPath(ObservedDirectory));

            if (observed == null)
            {
                _viewModel.ObserveRootDirectory();
                observed = root;
            }

            // Selecting an item makes its folder the observed one (TreeViewItem_Selected).
            RecordFolderTree.Select(observed);
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
            var appConfiguration = _viewModel.AppConfiguration;
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
            else if (sortMemberPath != "CreationTimestamp")
            {
                AddSortColumn(dataGrid, "GameName", sortDirection);
                AddSortColumn(dataGrid, "CreationTimestamp", sortDirection);
            }
        }

        private void RecordInfoListOnFilter(object sender, FilterEventArgs e)
        {
            e.FilterCollectionByText<IFileRecordInfo>(RecordSearchBox.Text,
                                        (record, word) => record.CombinedInfo.NullSafeContains(word, true)
                                        || record.GameName.NullSafeContains(word, true)
                                        || record.ProcessorName.NullSafeContains(word, true)
                                        || record.GraphicCardName.NullSafeContains(word, true)
                                        || record.SystemRamInfo.NullSafeContains(word, true)
                                        || record.Comment.NullSafeContains(word, true));
        }

        private void DataGridRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            _viewModel.OnRecordSelectByDoubleClick();
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            ScrollViewer scv = (ScrollViewer)sender;
            scv.ScrollToVerticalOffset(scv.VerticalOffset - e.Delta / 10);
            e.Handled = true;
        }



        private void TreeViewItem_Selected(object sender, RoutedEventArgs e)
        {
            if (!IsDragDropActive) // When using D&D, select items to highlight them, but don't change directory
            {
                TreeViewItem item = e.Source as TreeViewItem;
                _viewModel.RecordObserver.ObserveDirectory((item.Tag as DirectoryInfo).FullName);

                // Save the TreeviewItem of the observed directory
                ObservedTreeViewItem = item;
            }
        }

        private string ExtractFullPath(string path)
            => _viewModel.ResolveDocumentsPath(path);

        private void RootFolder_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var result = _viewModel.OnSelectRootFolder();
            if (result)
            {
                BuildTreeView();
            }
        }

        private void FolderChipButton_Click(object sender, RoutedEventArgs e)
        {
            FolderPopup.IsOpen = !FolderPopup.IsOpen;
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
            _viewModel.SaveDescriptions();
            e.Handled = true;
        }

        private void RecordDataGrid_PreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (_viewModel.SelectedRecordInfo == null)
                return;

            var myCell = (sender as MultiSelectionDataGrid).CurrentCell;
            // CurrentCell.Column is null when no cell ever had keyboard focus (e.g. a
            // row was only selected and focus then moves to a button elsewhere)
            if (myCell.Column == null)
                return;

            if (myCell.Column == CommentColumn)
            {
                if (_viewModel.CustomComment != _viewModel.SelectedRecordInfo.Comment)
                {
                    _viewModel.SelectedRecordInfo.Comment = _viewModel.CustomComment;
                }

            }
            else if (myCell.Column == CpuColumn)
            {
                if (_viewModel.CustomCpuDescription != _viewModel.SelectedRecordInfo.ProcessorName)
                {
                    _viewModel.SelectedRecordInfo.ProcessorName = _viewModel.CustomCpuDescription;
                }

            }
            else if (myCell.Column == GpuColumn)
            {
                if (_viewModel.CustomGpuDescription != _viewModel.SelectedRecordInfo.GraphicCardName)
                {
                    _viewModel.SelectedRecordInfo.GraphicCardName = _viewModel.CustomGpuDescription;
                }

            }
            else if (myCell.Column == RamColumn)
            {
                if (_viewModel.CustomRamDescription != _viewModel.SelectedRecordInfo.SystemRamInfo)
                {
                    _viewModel.SelectedRecordInfo.SystemRamInfo = _viewModel.CustomRamDescription;
                }

            }
        }

        private void RecordDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            if (_viewModel.SelectedRecordInfo == null)
                return;

            var myCell = (sender as MultiSelectionDataGrid).CurrentCell;
            if (myCell.Column == null)
                return;

            if (myCell.Column == CommentColumn)
            {
                if (_viewModel.CustomComment != _viewModel.SelectedRecordInfo.Comment)
                {
                    _viewModel.CustomComment = _viewModel.SelectedRecordInfo.Comment;
                    _viewModel.SaveDescriptions();
                }
            }
            else if (myCell.Column == CpuColumn)
            {
                if (_viewModel.CustomCpuDescription != _viewModel.SelectedRecordInfo.ProcessorName)
                {
                    _viewModel.CustomCpuDescription = _viewModel.SelectedRecordInfo.ProcessorName;
                    _viewModel.SaveDescriptions();
                }
            }
            else if (myCell.Column == GpuColumn)
            {
                if (_viewModel.CustomGpuDescription != _viewModel.SelectedRecordInfo.GraphicCardName)
                {
                    _viewModel.CustomGpuDescription = _viewModel.SelectedRecordInfo.GraphicCardName;
                    _viewModel.SaveDescriptions();
                }
            }
            else if (myCell.Column == RamColumn)
            {
                if (_viewModel.CustomRamDescription != _viewModel.SelectedRecordInfo.SystemRamInfo)
                {
                    _viewModel.CustomRamDescription = _viewModel.SelectedRecordInfo.SystemRamInfo;
                    _viewModel.SaveDescriptions();
                }
            }
        }

        private void RecordDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            var myCell = (sender as MultiSelectionDataGrid).CurrentCell;
            // The TextBlock control doesn't support vertical text alignment.
            // Must be a child of a Grid or a Border to be able to center the text.
            // myCell.Column.GetCellContent(myCell.Item).VerticalAlignment = VerticalAlignment.Center;
            RecordDataGrid.SelectedItem = myCell.Item;
        }

        private void RecordInfoExpander_Change(object sender, RoutedEventArgs e)
        {
            _viewModel.AppConfiguration.IsRecordInfoExpanded = RecordInfoExpander.IsExpanded;
        }

        private void RecordDataGrid_ColumnDisplayIndexChanged(object sender, DataGridColumnEventArgs e)
        {
            _viewModel.AppConfiguration.RecordListHeaderOrder = new int[7]
            {
                 GameColumn.DisplayIndex,
                 DateColumn.DisplayIndex,
                 CommentColumn.DisplayIndex,
                 AggregatedColumn.DisplayIndex,
                 CpuColumn.DisplayIndex,
                 GpuColumn.DisplayIndex,
                 RamColumn.DisplayIndex
            };
        }

        private void SetHeaders()
        {
            var indices = _viewModel.AppConfiguration.RecordListHeaderOrder;
            if (indices == null || indices.Length < 7)
                return;

            GameColumn.DisplayIndex = indices[0];
            DateColumn.DisplayIndex = indices[1];
            CommentColumn.DisplayIndex = indices[2];
            AggregatedColumn.DisplayIndex = indices[3];
            CpuColumn.DisplayIndex = indices[4];
            GpuColumn.DisplayIndex = indices[5];
            RamColumn.DisplayIndex = indices[6];
        }

        // TreeView Drag & Drop from record list

        private async void TrvStructure_DragOver(object sender, DragEventArgs e)
        {
            var item = e.Source as TreeViewItem;

            if (item != null)
            {
                // select items to highlight them, expand when possible. Set bool to stop directory from changing
                IsDragDropActive = true;
                item.IsSelected = true;

                await Task.Delay(TimeSpan.FromMilliseconds(500));
                if (TrvStructure.SelectedItem == item)
                    item.IsExpanded = true;
            }
            else
            {
                IsDragDropActive = false;
                ObservedTreeViewItem.IsSelected = true;
            }
        }

        private void TrvStructure_MouseEnter(object sender, MouseEventArgs e)
        {
            // return to default state when canceling D&D
            IsDragDropActive = false;
            ObservedTreeViewItem.IsSelected = true;
        }

        private void TrvStructure_Drop(object sender, DragEventArgs e)
        {
            var item = e.Source as TreeViewItem;
            var path = GetFullPath(item);
            _viewModel.OnMoveRecordFile(path);
            IsDragDropActive = false;

            // Keep the original observed directory after drop
            ObservedTreeViewItem.IsSelected = true;

            // Switch observed directory after drop
            //ObservedTreeViewItem = item;
            //_viewModel.RecordObserver.ObserveDirectory((item.Tag as DirectoryInfo).FullName);
        }

        public string GetFullPath(TreeViewItem node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));

            string rootName = new DirectoryInfo(CaptureRootDirectory).Name;

            // Collect path segments from node up to root
            var segments = new Stack<string>();
            for (TreeViewItem i = node; i != null; i = GetParentItem(i))
            {
                var name = Convert.ToString(i.Header);
                if (!string.IsNullOrWhiteSpace(name))
                    segments.Push(name);
            }

            // If the TreeView path already includes the root folder name, drop it once.
            if (segments.Count > 0 &&
                string.Equals(segments.Peek(), rootName, StringComparison.OrdinalIgnoreCase))
            {
                segments.Pop();
            }

            // Combine safely
            return Path.Combine(CaptureRootDirectory, Path.Combine(segments.ToArray()));
        }

        static TreeViewItem GetParentItem(TreeViewItem item)
        {
            for (var i = VisualTreeHelper.GetParent(item); i != null; i = VisualTreeHelper.GetParent(i))
                if (i is TreeViewItem)
                    return (TreeViewItem)i;

            return null;
        }
    }
}

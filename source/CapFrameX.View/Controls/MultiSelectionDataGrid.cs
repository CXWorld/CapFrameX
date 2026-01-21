using System.Collections;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace CapFrameX.View.Controls
{
    public class MultiSelectionDataGrid : DataGrid
    {
        private readonly List<object> _orderedSelection = new List<object>();

        public MultiSelectionDataGrid()
        {
            SelectionChanged += CustomDataGrid_SelectionChanged;
        }

        void CustomDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Remove deselected items from our ordered list
            foreach (var item in e.RemovedItems)
            {
                _orderedSelection.Remove(item);
            }

            // Append newly selected items to maintain selection order
            foreach (var item in e.AddedItems)
            {
                if (!_orderedSelection.Contains(item))
                {
                    _orderedSelection.Add(item);
                }
            }

            // Expose the ordered list
            SelectedItemsList = new List<object>(_orderedSelection);
        }

        public IList SelectedItemsList
        {
            get { return (IList)GetValue(SelectedItemsListProperty); }
            set { SetValue(SelectedItemsListProperty, value); }
        }

        public static readonly DependencyProperty SelectedItemsListProperty =
            DependencyProperty.Register("SelectedItemsList", typeof(IList), typeof(MultiSelectionDataGrid), new PropertyMetadata(null));

    }
}
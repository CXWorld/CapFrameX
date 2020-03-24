using CapFrameX.Contracts.Aggregation;
using CapFrameX.ViewModel;
using System;
using System.Reactive.Linq;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CapFrameX.View
{
    /// <summary>
    /// Interaction logic for  AggregationView.xaml
    /// </summary>
    public partial class AggregationView : UserControl
    {
        public AggregationView()
        {
            InitializeComponent();

            var context = SynchronizationContext.Current;
            (DataContext as AggregationViewModel)
                .OutlierFlagStream
                .Throttle(TimeSpan.FromMilliseconds(100))
                .ObserveOn(context)
                .SubscribeOn(context)
                .Subscribe(OnOutlierFlagsChanged);
        }

        private void AggregationItemDataGrid_MouseLeave(object sender, MouseEventArgs e)
        {
            (DataContext as AggregationViewModel).SelectedAggregationEntryIndex = -1;
            Keyboard.ClearFocus();
        }

        private void OnOutlierFlagsChanged(bool[] flags)
        {
            int count = 0;
            foreach (var item in AggregationItemDataGrid.ItemsSource)
            {
                DataGridRow row = (DataGridRow)AggregationItemDataGrid.ItemContainerGenerator.ContainerFromIndex(count);

                if (row != null)
                {
                    row.Background = flags[count] ? new SolidColorBrush(Color.FromArgb(80, 200, 0, 0)) : Brushes.Transparent;
                }
                count++;
            }
        }

        private void AggregationItemDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                if (AggregationItemDataGrid.SelectedItem != null)
                {
                    var selectedItem = AggregationItemDataGrid.SelectedItem as IAggregationEntry;
                    (DataContext as AggregationViewModel).RemoveAggregationEntry(selectedItem);
                }

                e.Handled = true;
            }
        }
    }
}

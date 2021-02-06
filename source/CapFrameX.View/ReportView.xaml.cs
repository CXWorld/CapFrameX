using CapFrameX.Data;
using CapFrameX.ViewModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CapFrameX.View
{
    /// <summary>
    /// Interaktionslogik für ReportView.xaml
    /// </summary>
    public partial class ReportView : UserControl
    {
        public ReportView()
        {
            InitializeComponent();
        }

        private void OnAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
            => e.Column.Header = ((PropertyDescriptor)e.PropertyDescriptor).DisplayName;

        private void ReportDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                if (ReportDataGrid.SelectedItem != null)
                {
                    if (ReportDataGrid.SelectedItem is ReportInfo selectedItem)
                        (DataContext as ReportViewModel).RemoveReportEntry(selectedItem);
                }

                e.Handled = true;
            }
        }

        private void ReportDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if(e.Row.Item is ReportInfo reportInfo && reportInfo.Game.Equals("Averaged values"))
            {
                e.Row.Background = new SolidColorBrush(Color.FromArgb(150, 34, 151, 243));
            }
        }
    }
}

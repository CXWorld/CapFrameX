using CapFrameX.Data;
using CapFrameX.ViewModel;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;

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
    }
}

using CapFrameX.Configuration;
using CapFrameX.Statistics;
using CapFrameX.ViewModel;
using Prism.Events;
using System.ComponentModel;
using System.Windows.Controls;

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

            // Design time!
            if (DesignerProperties.GetIsInDesignMode(this))
            {
				var appConfiguration = new CapFrameXConfiguration();
				DataContext = new ReportViewModel(new FrametimeStatisticProvider(), new EventAggregator(), appConfiguration);
            }
        }

		private void OnAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
			=> e.Column.Header = ((PropertyDescriptor)e.PropertyDescriptor).DisplayName;
	}
}

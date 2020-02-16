using CapFrameX.Configuration;
using CapFrameX.Data;
using CapFrameX.Statistics;
using CapFrameX.ViewModel;
using Microsoft.Extensions.Logging;
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
				DataContext = new ReportViewModel(new FrametimeStatisticProvider(appConfiguration), new EventAggregator(), appConfiguration, new RecordManager(new LoggerFactory().CreateLogger<RecordManager>()));
            }
        }

		private void OnAutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
			=> e.Column.Header = ((PropertyDescriptor)e.PropertyDescriptor).DisplayName;
	}
}

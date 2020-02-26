using CapFrameX.Configuration;
using CapFrameX.Contracts.Aggregation;
using CapFrameX.Contracts.Cloud;
using CapFrameX.Data;
using CapFrameX.Statistics;
using CapFrameX.ViewModel;
using Microsoft.Extensions.Logging;
using Prism.Events;
using System;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CapFrameX.View
{
	/// <summary>
	/// Interaction logic for  AggregationView.xaml
	/// </summary>
	public partial class CloudView : UserControl
	{
		public CloudView()
		{
			InitializeComponent();

			// Design time!
			if (DesignerProperties.GetIsInDesignMode(this))
			{
				var appConfiguration = new CapFrameXConfiguration();
				var loggerFactory = new LoggerFactory();
				var recordManager = new RecordManager(loggerFactory.CreateLogger<RecordManager>(), appConfiguration, new RecordDirectoryObserver(appConfiguration, loggerFactory.CreateLogger<RecordDirectoryObserver>()), new AppVersionProvider());
				DataContext = new CloudViewModel(new FrametimeStatisticProvider(appConfiguration), recordManager, new EventAggregator(), appConfiguration, loggerFactory.CreateLogger<CloudViewModel>(), new AppVersionProvider());
			}

		}

		private void CloudItemDataGrid_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
		{
			(DataContext as CloudViewModel).SelectedCloudEntryIndex = -1;
			Keyboard.ClearFocus();
		}


		private void CloudItemDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Delete)
			{
				if (CloudItemDataGrid.SelectedItem != null)
				{
					var selectedItem = CloudItemDataGrid.SelectedItem as ICloudEntry;
					(DataContext as CloudViewModel).RemoveCloudEntry(selectedItem);
				}

				e.Handled = true;
			}
		}

		private void ShareUrlFocusHandler(object sender, RoutedEventArgs e)
		{
			TextBox textBox = (TextBox)sender;
			textBox.Dispatcher.BeginInvoke(new Action(() => textBox.SelectAll()));
		}
	}
}

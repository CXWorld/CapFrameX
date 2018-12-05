using System;
using System.Windows;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel;
using System.Windows.Controls.Primitives;
using CapFrameX.ViewModel;
using CapFrameX.OcatInterface;
using System.Windows.Data;
using System.Reactive.Linq;
using CapFrameX.Extensions;
using CapFrameX.Statistics;

namespace CapFrameX.View
{
	/// <summary>
	/// Interaktionslogik für MainView.xaml
	/// </summary>
	public partial class MainView : UserControl
	{
		private const int SEARCH_REFRESH_DELAY_MS = 100;
		private readonly CollectionViewSource _recordInfoCollection;

		public MainView()
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
				DataContext = new MainViewModel(new RecordDirectoryObserver(), new FrametimeStatisticProvider(), new FrametimeAnalyzer());
			}
		}

		private void UIElement_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			//until we had a StaysOpen glag to Drawer, this will help with scroll bars
			var dependencyObject = Mouse.Captured as DependencyObject;
			while (dependencyObject != null)
			{
				if (dependencyObject is ScrollBar) return;
				dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
			}

			MenuToggleButton.IsChecked = false;
		}

		private async void MenuPopupButton_OnClick(object sender, RoutedEventArgs e)
		{
			//var sampleMessageDialog = new SampleMessageDialog
			//{
			//    Message = { Text = ((ButtonBase)sender).Content.ToString() }
			//};

			//await DialogHost.Show(sampleMessageDialog, "RootDialog");
		}

		private void OnCopy(object sender, ExecutedRoutedEventArgs e)
		{
			if (e.Parameter is string stringValue)
			{
				try
				{
					Clipboard.SetDataObject(stringValue);
				}
				catch (Exception ex)
				{
					Trace.WriteLine(ex.ToString());
				}
			}
		}

		private void ResetZoomOnClick(object sender, RoutedEventArgs e)
		{
			//Use the axis MinValue/MaxValue properties to specify the values to display.
			//use double.Nan to clear it.

			FrametimesX.MinValue = double.NaN;
			FrametimesX.MaxValue = double.NaN;
			FrametimesY.MinValue = double.NaN;
			FrametimesY.MaxValue = double.NaN;

			LShapeX.MinValue = double.NaN;
			LShapeX.MaxValue = double.NaN;
			LShapeY.MinValue = double.NaN;
			LShapeY.MaxValue = double.NaN;
		}

		private void RecordInfoListOnFilter(object sender, FilterEventArgs e)
		{
			e.FilterCollectionByText<OcatRecordInfo>(RecordSearchBox.Text,
										(record, word) => record.FullPath.NullSafeContains(word, true));
		}
	}
}

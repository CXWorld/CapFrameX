using CapFrameX.ViewModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace CapFrameX.View
{
	/// <summary>
	/// Interaktionslogik für ColorbarView.xaml
	/// </summary>
	public partial class ColorbarView : UserControl
	{
		public ColorbarView()
		{
			InitializeComponent();

			// Design time!
			if (DesignerProperties.GetIsInDesignMode(this))
			{
				DataContext = new ColorbarViewModel();
			}
		}

		private async void MenuPopupButton_OnClick(object sender, RoutedEventArgs e)
		{
			//var sampleMessageDialog = new SampleMessageDialog
			//{
			//    Message = { Text = ((ButtonBase)sender).Content.ToString() }
			//};

			//await DialogHost.Show(sampleMessageDialog, "RootDialog");
		}
	}
}

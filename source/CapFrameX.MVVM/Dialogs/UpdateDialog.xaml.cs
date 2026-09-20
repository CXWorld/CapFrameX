using System.Windows.Controls;

namespace CapFrameX.MVVM.Dialogs
{
	/// <summary>
	/// Interaction logic for UpdateDialog.xaml. The DataContext is inherited from the hosting
	/// DialogHost, which the shell points at the UpdateViewModel.
	/// </summary>
	public partial class UpdateDialog : UserControl
	{
		public UpdateDialog()
		{
			InitializeComponent();
		}
	}
}

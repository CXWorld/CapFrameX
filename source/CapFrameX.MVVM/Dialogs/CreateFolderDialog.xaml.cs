using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace CapFrameX.MVVM.Dialogs
{
	/// <summary>
	/// Interaktionslogik für CreateFolderDialog.xaml
	/// </summary>
	public partial class CreateFolderDialog : UserControl
	{
        private readonly Action createDirectoryAction;

        public CreateFolderDialog(Action createDirectoryAction)
		{
			InitializeComponent();
            this.createDirectoryAction = createDirectoryAction;
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
			if (e.Key != Key.Enter)
				return;

			createDirectoryAction();
			e.Handled = true;
		}
    }
}

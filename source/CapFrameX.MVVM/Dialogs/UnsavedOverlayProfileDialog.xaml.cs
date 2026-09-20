using System.Windows.Controls;

namespace CapFrameX.MVVM.Dialogs
{
    public enum UnsavedOverlayProfileDialogResult
    {
        SaveAndExit,
        ExitWithoutSaving,
        Cancel
    }

    /// <summary>
    /// Interaction logic for UnsavedOverlayProfileDialog.xaml.
    /// </summary>
    public partial class UnsavedOverlayProfileDialog : UserControl
    {
        public UnsavedOverlayProfileDialog()
        {
            InitializeComponent();
        }
    }
}

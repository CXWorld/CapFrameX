using System.Windows;

namespace CapFrameX
{
    /// <summary>
    /// Interaction logic for Shell.xaml
    /// </summary>
    public partial class Shell : Window
	{
        public Shell()
        {
            InitializeComponent();

			// Start tracking the Window instance.
			WindowStatServices.Tracker.Track(this);
		}
	}
}

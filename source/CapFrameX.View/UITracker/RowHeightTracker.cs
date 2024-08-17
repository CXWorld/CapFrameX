using Jot;
using System.Windows;
using System.Windows.Controls;

namespace CapFrameX.View.UITracker
{
    public class RowHeightTracker
	{
		// expose the tracker instance
		public Tracker Tracker = new Tracker();

        public RowHeightTracker(Window window)
		{
			Tracker.Configure<RowDefinition>()
				.Id(row => row.Name)
				.Properties(row => new { row.Height })
				.PersistOn(nameof(Window.Closed), window);
		}
    }
}

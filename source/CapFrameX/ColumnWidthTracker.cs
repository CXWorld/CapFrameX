using Jot;
using System.Windows;
using System.Windows.Controls;

namespace CapFrameX
{
    public class ColumnWidthTracker
	{
		// expose the tracker instance
		public Tracker Tracker = new Tracker();

		public ColumnWidthTracker(Window window)
		{
			Tracker.Configure<ColumnDefinition>()
				.Id(col => col.Name)
				.Properties(col => new { col.Width })
				.PersistOn(nameof(Window.Closed), window);
		}
	}
}

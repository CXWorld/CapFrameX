using Jot;
using Jot.Storage;
using System.Windows;
using System.Windows.Controls;

namespace CapFrameX.View.UITracker
{
    public class ColumnWidthTracker
	{
		// expose the tracker instance
		public Tracker Tracker { get; }

		public ColumnWidthTracker(Window window, string folderPath)
		{
			Tracker = new Tracker(new JsonFileStore(folderPath));

			Tracker.Configure<ColumnDefinition>()
				.Id(col => col.Name)
				.Properties(col => new { col.Width })
				.PersistOn(nameof(Window.Closed), window);
		}
	}
}

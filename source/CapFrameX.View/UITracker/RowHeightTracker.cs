using Jot;
using Jot.Storage;
using System.Windows;
using System.Windows.Controls;

namespace CapFrameX.View.UITracker
{
    public class RowHeightTracker
	{
		// expose the tracker instance
		public Tracker Tracker { get; }

        public RowHeightTracker(Window window, string folderPath)
		{
			Tracker = new Tracker(new JsonFileStore(folderPath));

			Tracker.Configure<RowDefinition>()
				.Id(row => row.Name)
				.Properties(row => new { row.Height })
				.PersistOn(nameof(Window.Closed), window);
		}
    }
}

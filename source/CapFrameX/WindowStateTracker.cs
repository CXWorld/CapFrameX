using Jot;
using Jot.Storage;
using System.Windows;

namespace CapFrameX
{
	class WindowStateTracker
	{
		// expose the tracker instance
		public Tracker Tracker { get; }

		public WindowStateTracker(string folderPath)
		{
			Tracker = new Tracker(new JsonFileStore(folderPath));

			Tracker.Configure<Window>()
				.Id(w => w.Name)
				.Properties(w => new { w.Height, w.Width, w.Left, w.Top, w.WindowState })
				.PersistOn(nameof(Window.Closed));
		}
	}
}

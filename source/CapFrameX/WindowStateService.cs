using Jot;
using System.Windows;

namespace CapFrameX
{
	static class WindowStatServices
	{
		// expose the tracker instance
		public static Tracker Tracker = new Tracker();

		static WindowStatServices()
		{
			Tracker.Configure<Window>()
				.Id(w => w.Name)
				.Properties(w => new { w.Height, w.Width, w.Left, w.Top, w.WindowState })
				.PersistOn(nameof(Window.Closed));
		}
	}
}

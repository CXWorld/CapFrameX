using Jot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CapFrameX
{
	static class WindowStatServices
	{
		// expose the tracker instance
		public static Tracker Tracker = new Tracker();

		static WindowStatServices()
		{
			// tell Jot how to track Window objects
			Tracker.Configure<Window>()
				.Id(w => w.Name)
				.Properties(w => new { w.Height, w.Width, w.Left, w.Top, w.WindowState })
				.PersistOn(nameof(Window.Closed));
		}
	}
}

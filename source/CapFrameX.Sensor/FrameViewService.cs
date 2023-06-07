using CapFrameX.Contracts.Sensor;
using Microsoft.Extensions.Logging;
using System;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CapFrameX.Sensor
{
	public class FrameViewService : IFrameViewService
	{
		[HandleProcessCorruptedStateExceptions]
		[DllImport("CapFrameX.FrameView.dll")]
		private static extern bool IntializeFrameViewSession();

		[HandleProcessCorruptedStateExceptions]
		[DllImport("CapFrameX.FrameView.dll")]
		private static extern bool CloseFrameViewSession();

		private readonly ILogger<FrameViewService> _logger;

		public bool IsFrameViewAvailable { get; private set; }

		public FrameViewService(ILogger<FrameViewService> logger)
		{
			_logger = logger;
		}

		public async Task IntializeFrameViewService()
		{
			await Task.Run(() =>
			{
				try
				{
					IsFrameViewAvailable = IntializeFrameViewSession();
					if (!IsFrameViewAvailable) _logger.LogError("Error while intializing FrameView session.");
				}
				catch (Exception ex) { _logger.LogError(ex, $"Error while accessing CapFrameX.FrameView.dll."); }
			});
		}

		public void CloseFrameViewService()
		{
			try
			{
				// Close FrameView session
				bool check = CloseFrameViewSession();
				if (!check) _logger.LogError("Error while closing FrameView session.");
			}
			catch (Exception ex) { _logger.LogError(ex, $"Error while accessing CapFrameX.FrameView.dll."); }
		}
	}
}

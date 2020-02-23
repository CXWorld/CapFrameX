using CapFrameX.Contracts.Statistics;
using System.Collections.Generic;
using System.Windows;

namespace CapFrameX.Contracts.Data
{
	public interface ISession
	{
		List<bool> AppMissed { get; set; }
		List<double> DisplayTimes { get; set; }
		string Filename { get; set; }
		List<double> FrameEnd { get; set; }
		List<double> FrameStart { get; set; }
		List<double> FrameTimes { get; set; }
		List<double> InPresentAPITimes { get; set; }
		bool IsVR { get; set; }
		double LastFrameTime { get; set; }
		string Path { get; set; }
		List<double> QPCTimes { get; set; }
		List<double> ReprojectionEnd { get; set; }
		List<double> ReprojectionStart { get; set; }
		List<double> ReprojectionTimes { get; set; }
		List<double> UntilDisplayedTimes { get; set; }
		int ValidReproFrames { get; set; }
		List<double> VSync { get; set; }
		List<bool> WarpMissed { get; set; }
		int WarpMissesCount { get; set; }

		IList<double> GetApproxInputLagTimes();
		IList<Point> GetFrametimePointsSampleWindow(int startIndex, double endIndex, ERemoveOutlierMethod eRemoveOutlierMethod = ERemoveOutlierMethod.None);
		IList<Point> GetFrametimePointsTimeWindow(double startTime, double endTime, ERemoveOutlierMethod eRemoveOutlierMethod = ERemoveOutlierMethod.None);
		IList<double> GetFrametimeSampleWindow(int startIndex, double endIndex, ERemoveOutlierMethod eRemoveOutlierMethod = ERemoveOutlierMethod.None);
		IList<double> GetFrametimeTimeWindow(double startTime, double endTime, ERemoveOutlierMethod eRemoveOutlierMethod = ERemoveOutlierMethod.None);
		double GetSyncRangePercentage(int syncRangeLower, int syncRangeUpper);
	}
}
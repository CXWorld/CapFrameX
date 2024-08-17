using CapFrameX.Data.Session.Contracts;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using System.Collections.Generic;

namespace CapFrameX.Data
{
	public interface IRecordDataServer
	{
		bool IsActive { get; set; }

		double WindowLength { get; set; }

		double CurrentTime { get; set; }

		ISession CurrentSession { get; set; }

		ERemoveOutlierMethod RemoveOutlierMethod { get; set; }

		EFilterMode FilterMode { get; set; }

		IList<double> GetFrametimeTimeWindow();

		IList<double> GetGpuActiveTimeTimeWindow();

		IList<Point> GetFrametimePointTimeWindow();

		IList<Point> GetGpuActiveTimePointTimeWindow();


        IList<double> GetFpsTimeWindow();

		IList<double> GetGpuActiveFpsTimeWindow();

        IList<Point> GetFpsPointTimeWindow();

        IList<Point> GetGpuActiveFpsPointTimeWindow();

		double GetGpuActiveDeviationPercentage();

        void SetTimeWindow(double currentTime, double windowLength);
	}
}

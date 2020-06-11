using CapFrameX.Data.Session.Contracts;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using System;
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

		IObservable<IList<double>> FrametimeDataStream { get; }

		IObservable<IList<Point>> FrametimePointDataStream { get; }

		IObservable<IList<double>> FpsDataStream { get; }

		IObservable<IList<Point>> FpsPointDataStream { get; }

		IList<double> GetFrametimeTimeWindow();

		IList<Point> GetFrametimePointTimeWindow();

		IList<double> GetFpsTimeWindow();

		IList<Point> GetFpsPointTimeWindow();

		void SetTimeWindow(double currentTime, double windowLength);
	}
	
}

using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Statistics;
using CapFrameX.Statistics;
using System;
using System.Collections.Generic;
using System.Windows;

namespace CapFrameX.Data
{
	public interface IRecordDataServer
	{
		bool IsActive { get; set; }

		double WindowLength { get; set; }

		double CurrentTime { get; set; }

		ISession CurrentSession { get; set; }

		ERemoveOutlierMethod RemoveOutlierMethod { get; set; }

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

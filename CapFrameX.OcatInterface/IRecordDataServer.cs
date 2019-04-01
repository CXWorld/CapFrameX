using CapFrameX.Statistics;
using System;
using System.Collections.Generic;
using System.Windows;

namespace CapFrameX.OcatInterface
{
	public interface IRecordDataServer
	{
		int StartIndex { get; set; }

		int EndIndex { get; set; }

		double WindowLength { get; set; }

		double CurrentTime { get; set; }

		Session CurrentSession { get; set; }

		ERemoveOutlierMethod RemoveOutlierMethod { get; set; }

		IObservable<IList<double>> FrametimeDataStream { get; }

		IObservable<IList<Point>> FrametimePointDataStream { get; }

		IObservable<IList<double>> FpsDataStream { get; }

		IObservable<IList<Point>> FpsPointDataStream { get; }

		IList<double> GetFrametimeTimeWindow();

		IList<double> GetFrametimeSampleWindow();

		IList<Point> GetFrametimePointTimeWindow();

		IList<Point> GetFrametimePointSampleWindow();

		IList<double> GetFpsTimeWindow();

		IList<double> GetFpsSampleWindow();

		IList<Point> GetFpsPointTimeWindow();

		IList<Point> GetFpsPointSampleWindow();

	}
}

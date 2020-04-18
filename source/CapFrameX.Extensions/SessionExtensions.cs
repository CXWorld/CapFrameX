using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CapFrameX.Data.Session.Contracts;

namespace CapFrameX.Extensions
{
	public static class SessionExtensions
	{
		public static bool HasValidSensorData(this ISession session)
		{
			return session.Runs.All(run => run.SensorData != null && run.SensorData.MeasureTime.Any());
		}
	}
}

using CapFrameX.Contracts.Statistics;
using System.Collections.Generic;
using System.Windows;

namespace CapFrameX.Contracts.Data
{
	public interface ISession
	{
		ISessionInfo Header { get; set; }
		List<ISessionRun> Runs { get; set; }
	}
}
using CapFrameX.Contracts.Statistics;
using System.Collections.Generic;
using System.Windows;

namespace CapFrameX.Contracts.Data
{
	public interface ISession
	{
		ISessionInfo Info { get; set; }
		IList<ISessionRun> Runs { get; set; }
	}
}
using System.Collections.Generic;

namespace CapFrameX.Data.Session.Contracts
{
	public interface ISession
	{
		string Hash { get; set; }
		ISessionInfo Info { get; set; }
		IList<ISessionRun> Runs { get; set; }
	}
}
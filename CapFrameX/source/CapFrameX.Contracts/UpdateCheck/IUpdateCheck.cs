using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Contracts.UpdateCheck
{
	public interface IUpdateCheck
	{
		Task<(bool, Version)> IsUpdateAvailable();
	}
}

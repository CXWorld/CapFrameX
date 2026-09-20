using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CapFrameX.Contracts.Update;

namespace CapFrameX.Contracts.Data
{
	public interface IAppVersionProvider
	{
		Version GetAppVersion();

		EUpdateChannel GetReleaseChannel();
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Contracts.Data
{
	public interface IWebVersionProvider
	{
		Version GetWebVersion();
	}
}

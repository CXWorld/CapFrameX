using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Contracts.MVVM
{
	public interface IShell
	{
		System.Windows.Controls.ContentControl GlobalScreenshotArea { get; }
	}
}

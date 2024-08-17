using System;
using System.Threading.Tasks;

namespace CapFrameX.Hardware.Controller
{
	public interface IThreadAffinityController
	{
		AffinityState CpuAffinityState { get; }
		void ToggleAffinity();
	}
}

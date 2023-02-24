using System;
namespace CapFrameX.Hardware.Controller
{
	public interface IThreadAffinityController
	{
		AffinityState CpuAffinityState { get; }
		void ToggleAffinity(int processId);
	}
}

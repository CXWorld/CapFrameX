using System.Threading.Tasks;

namespace CapFrameX.Contracts.Sensor
{
	public interface IFrameViewService
	{
		bool IsFrameViewAvailable { get; }

		Task IntializeFrameViewService();

		void CloseFrameViewService();

		double GetAveragePcLatency(int pid);
	}
}
